#requires -Version 5.1
$ErrorActionPreference = 'Stop'
$Root = $PSScriptRoot
Set-Location -LiteralPath $Root

$EnvPath = Join-Path $Root '.env'
if (-not (Test-Path -LiteralPath $EnvPath)) {
    throw 'Missing .env. Run Start-TicketManager-Docker.cmd first.'
}
$Lines = Get-Content -LiteralPath $EnvPath
$KeyLine = $Lines | Where-Object { $_.StartsWith('TICKET_BOOTSTRAP_KEY=') } | Select-Object -First 1
if (-not $KeyLine) {
    throw 'Bootstrap key is already disabled or missing. If an administrator exists, this is expected.'
}
$BootstrapKey = $KeyLine.Substring('TICKET_BOOTSTRAP_KEY='.Length).Trim()
if ([string]::IsNullOrWhiteSpace($BootstrapKey)) {
    throw 'TICKET_BOOTSTRAP_KEY is empty in .env.'
}

$PortLine = $Lines | Where-Object { $_ -match '^TICKET_API_PORT=' } | Select-Object -First 1
$ApiPort = 5080
if ($PortLine) {
    $ParsedPort = 0
    if ([int]::TryParse($PortLine.Substring('TICKET_API_PORT='.Length), [ref]$ParsedPort)) { $ApiPort = $ParsedPort }
}

$env:TICKET_API_URL = "http://127.0.0.1:$ApiPort"
$env:TICKET_BOOTSTRAP_KEY = $BootstrapKey
Write-Host 'Creating the first Ticket Manager administrator. Enter the account details in the console.' -ForegroundColor Cyan
dotnet run --project "./Ticket manadger/Ticket manadger.csproj" -- --bootstrap-admin
$ClientExitCode = $LASTEXITCODE
Remove-Item Env:TICKET_BOOTSTRAP_KEY -ErrorAction SilentlyContinue

if ($ClientExitCode -ne 0) {
    throw 'Console client exited with an error. Bootstrap key was not changed.'
}
$Answer = Read-Host 'If the console confirmed that the first administrator was created, type Y to disable the bootstrap endpoint'
if ($Answer -match '^[YyДд]$') {
    $UpdatedLines = @($Lines | Where-Object { -not $_.StartsWith('TICKET_BOOTSTRAP_KEY=') })
    [System.IO.File]::WriteAllLines($EnvPath, [string[]]$UpdatedLines, [System.Text.UTF8Encoding]::new($false))
    & docker compose up -d --force-recreate api
    if ($LASTEXITCODE -ne 0) {
        throw 'Bootstrap key was removed from .env, but API recreation failed. Run docker compose up -d --force-recreate api.'
    }
    Write-Host 'Bootstrap endpoint disabled. The database and normal login remain available.' -ForegroundColor Green
} else {
    Write-Host 'Bootstrap key remains enabled. After successful administrator creation, rerun this script and confirm disabling it.'
}
