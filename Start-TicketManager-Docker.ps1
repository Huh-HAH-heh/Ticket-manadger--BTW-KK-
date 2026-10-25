#requires -Version 5.1
$ErrorActionPreference = 'Stop'
$Root = $PSScriptRoot
Set-Location -LiteralPath $Root

function New-HexSecret {
    return ([guid]::NewGuid().ToString('N') + [guid]::NewGuid().ToString('N'))
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw 'Docker CLI was not found. Install and start Docker Desktop first.'
}
& docker info *> $null
if ($LASTEXITCODE -ne 0) {
    throw 'Docker Desktop is not running. Start Docker Desktop and retry.'
}
& docker compose version *> $null
if ($LASTEXITCODE -ne 0) {
    throw 'Docker Compose v2 is required: update Docker Desktop.'
}

$EnvPath = Join-Path $Root '.env'
if (-not (Test-Path -LiteralPath $EnvPath)) {
    $Lines = @(
        'POSTGRES_DB=ticket_db'
        'POSTGRES_USER=ticket_app'
        ('POSTGRES_PASSWORD=Ticket' + (New-HexSecret))
        ('TICKET_API_TOKEN_SECRET=' + (New-HexSecret))
        ('TICKET_BOOTSTRAP_KEY=' + (New-HexSecret))
        'TICKET_API_PORT=5080'
    )
    [System.IO.File]::WriteAllLines($EnvPath, [string[]]$Lines, [System.Text.UTF8Encoding]::new($false))
    Write-Host 'Created local .env with generated secrets. It is excluded from Git.' -ForegroundColor Green
}

$EnvLines = Get-Content -LiteralPath $EnvPath
$PortLine = $EnvLines | Where-Object { $_ -match '^TICKET_API_PORT=' } | Select-Object -First 1
$ApiPort = 5080
if ($PortLine) {
    $ParsedPort = 0
    if ([int]::TryParse($PortLine.Substring('TICKET_API_PORT='.Length), [ref]$ParsedPort) -and $ParsedPort -ge 1 -and $ParsedPort -le 65535) {
        $ApiPort = $ParsedPort
    }
}

$BootstrapLine = $EnvLines | Where-Object { $_.StartsWith('TICKET_BOOTSTRAP_KEY=') } | Select-Object -First 1
if ($BootstrapLine -and (Get-Command Set-Clipboard -ErrorAction SilentlyContinue)) {
    $BootstrapKey = $BootstrapLine.Substring('TICKET_BOOTSTRAP_KEY='.Length).Trim()
    if ($BootstrapKey) {
        Set-Clipboard -Value $BootstrapKey
        Write-Host 'One-time administrator setup key copied to clipboard. Do not send it to anyone.' -ForegroundColor Yellow
    }
}

$ExistingApi = @()
try { $ExistingApi = @(docker compose ps --status running --services 2>$null) } catch {}
if ($ExistingApi -notcontains 'api') {
    $Listener = Get-NetTCPConnection -LocalPort $ApiPort -State Listen -ErrorAction SilentlyContinue |
        Where-Object { $_.LocalAddress -in @('127.0.0.1', '0.0.0.0', '::1', '::') } |
        Select-Object -First 1
    if ($Listener) {
        throw "Host port $ApiPort is already in use. Stop the old API process or set TICKET_API_PORT in .env to another free port."
    }
}

Write-Host 'Building API image and starting PostgreSQL + API containers...' -ForegroundColor Cyan
& docker compose up --build -d
if ($LASTEXITCODE -ne 0) {
    & docker compose logs --tail 80 api
    throw 'Docker Compose failed. Review the logs above.'
}

$Healthy = $false
for ($Attempt = 1; $Attempt -le 120; $Attempt++) {
    try {
        $Result = Invoke-RestMethod -Uri "http://127.0.0.1:$ApiPort/health" -TimeoutSec 3
        if ($Result.status -eq 'ok' -and $Result.database -eq 'connected' -and $Result.migrations -eq 'current') {
            $Healthy = $true
            break
        }
    } catch {}
    if (($Attempt % 10) -eq 0) {
        Write-Host "Waiting for API and database readiness... ($Attempt/120)"
    }
    Start-Sleep -Seconds 2
}

if (-not $Healthy) {
    & docker compose ps
    & docker compose logs --tail 100 api
    & docker compose logs --tail 50 db
    throw "API did not become healthy. Inspect the logs above. Expected URL: http://127.0.0.1:$ApiPort/health"
}

Write-Host ''
Write-Host 'Ticket Manager API is running.' -ForegroundColor Green
Write-Host "Health: http://127.0.0.1:$ApiPort/health"
Write-Host 'PostgreSQL is running in Docker with a persistent named volume.'
Write-Host 'To create the first administrator, run Create-TicketManager-Admin.cmd.'
Write-Host 'To stop the services without deleting the database, run: docker compose down'
