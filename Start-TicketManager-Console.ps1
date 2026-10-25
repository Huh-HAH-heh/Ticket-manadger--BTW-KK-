#requires -Version 5.1
$ErrorActionPreference = 'Stop'
$Root = $PSScriptRoot
Set-Location -LiteralPath $Root
$EnvPath = Join-Path $Root '.env'
if (-not (Test-Path -LiteralPath $EnvPath)) {
    throw 'Run Start-TicketManager-Docker.cmd first.'
}
$Lines = Get-Content -LiteralPath $EnvPath
$PortLine = $Lines | Where-Object { $_ -match '^TICKET_API_PORT=' } | Select-Object -First 1
$ApiPort = 5080
if ($PortLine) {
    $ParsedPort = 0
    if ([int]::TryParse($PortLine.Substring('TICKET_API_PORT='.Length), [ref]$ParsedPort)) { $ApiPort = $ParsedPort }
}
$env:TICKET_API_URL = "http://127.0.0.1:$ApiPort"
try {
    $Health = Invoke-RestMethod -Uri "$($env:TICKET_API_URL)/health" -TimeoutSec 4
    if ($Health.status -ne 'ok' -or $Health.database -ne 'connected' -or $Health.migrations -ne 'current') {
        throw 'API health check did not confirm a connected database and current migrations.'
    }
} catch {
    throw "Ticket Manager API is not ready. Start it with Start-TicketManager-Docker.cmd. Details: $($_.Exception.Message)"
}
Write-Host "Connected to Ticket Manager API at $env:TICKET_API_URL" -ForegroundColor Green
dotnet run --project "./Ticket manadger/Ticket manadger.csproj"
if ($LASTEXITCODE -ne 0) {
    throw 'The console client exited with an error.'
}
