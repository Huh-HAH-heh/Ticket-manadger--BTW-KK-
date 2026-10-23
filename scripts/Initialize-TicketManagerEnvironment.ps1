# Dot-source this file so the environment variables remain in the current PowerShell:
# . .\scripts\Initialize-TicketManagerEnvironment.ps1

if ($MyInvocation.InvocationName -ne '.') {
    throw "Run this script by dot-sourcing it: . .\scripts\Initialize-TicketManagerEnvironment.ps1"
}

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $repoRoot 'Ticket Manager API\Ticket Manager API.csproj'
$consoleProject = Join-Path $repoRoot 'Ticket manadger\Ticket manadger.csproj'

if (-not (Test-Path $apiProject)) {
    throw "API project was not found. Switch to feature/stage-5-db-setup and run this from the repository."
}
if (-not (Test-Path $consoleProject)) {
    throw "Console project was not found. Open the repository root and try again."
}

$pgBin = Join-Path $env:ProgramFiles 'PostgreSQL\18\bin'
$psqlPath = Join-Path $pgBin 'psql.exe'
$readyPath = Join-Path $pgBin 'pg_isready.exe'

if (-not (Test-Path $psqlPath)) {
    $psqlCommand = Get-Command 'psql.exe' -ErrorAction SilentlyContinue
    if ($null -eq $psqlCommand) {
        throw "psql.exe was not found. Expected '$psqlPath'. Check the PostgreSQL installation."
    }
    $psqlPath = $psqlCommand.Source
    $readyPath = Join-Path (Split-Path -Parent $psqlPath) 'pg_isready.exe'
}

$service = Get-Service -Name 'postgresql-x64-18' -ErrorAction SilentlyContinue
if ($null -ne $service -and $service.Status -ne 'Running') {
    throw "PostgreSQL service is stopped. Start service '$($service.Name)' and run this script again."
}

if (Test-Path $readyPath) {
    & $readyPath -h localhost -p 5432
    if ($LASTEXITCODE -ne 0) {
        throw 'PostgreSQL is not accepting connections at localhost:5432.'
    }
}

$securePassword = Read-Host 'Password for PostgreSQL user postgres' -AsSecureString
$passwordPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
try {
    $dbPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordPointer)
}
finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPointer)
}

$connectionBuilder = New-Object System.Data.Common.DbConnectionStringBuilder
$connectionBuilder['Host'] = 'localhost'
$connectionBuilder['Port'] = 5432
$connectionBuilder['Database'] = 'ticket_db'
$connectionBuilder['Username'] = 'postgres'
$connectionBuilder['Password'] = $dbPassword
$env:TICKET_DB_CONNECTION = $connectionBuilder.ConnectionString

# Check credentials and the existing database without putting the password in command history.
$previousPgPassword = $env:PGPASSWORD
try {
    $env:PGPASSWORD = $dbPassword
    $checkResult = & $psqlPath -h localhost -p 5432 -U postgres -d ticket_db -w -v ON_ERROR_STOP=1 -t -A -c 'SELECT 1;'
    $psqlExitCode = $LASTEXITCODE
}
finally {
    if ($null -eq $previousPgPassword) {
        Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    }
    else {
        $env:PGPASSWORD = $previousPgPassword
    }
    $dbPassword = $null
    $securePassword.Dispose()
}

if ($psqlExitCode -ne 0 -or (($checkResult -join '').Trim() -ne '1')) {
    throw "Could not connect to database 'ticket_db' as postgres. Check the password, port 5432, and that the database exists."
}

function New-TicketManagerSecret([int]$length) {
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $bytes = New-Object byte[] $length
        $rng.GetBytes($bytes)
        return [Convert]::ToBase64String($bytes)
    }
    finally {
        $rng.Dispose()
    }
}

if ([string]::IsNullOrWhiteSpace($env:TICKET_API_TOKEN_SECRET)) {
    $env:TICKET_API_TOKEN_SECRET = New-TicketManagerSecret 48
}
if ([string]::IsNullOrWhiteSpace($env:TICKET_BOOTSTRAP_KEY)) {
    $env:TICKET_BOOTSTRAP_KEY = New-TicketManagerSecret 32
}

$env:TICKET_API_URL = 'http://localhost:5080'

Write-Host ''
Write-Host 'PostgreSQL connection verified: localhost:5432 / ticket_db.' -ForegroundColor Green
Write-Host 'Environment configured in this PowerShell session.' -ForegroundColor Green
Write-Host "Bootstrap key (copy this to the console's second terminal): $env:TICKET_BOOTSTRAP_KEY" -ForegroundColor Yellow
Write-Host ''
Write-Host 'Next: dotnet ef database update --project "Ticket Manager API/Ticket Manager API.csproj"'
Write-Host 'Then: dotnet run --project "Ticket Manager API/Ticket Manager API.csproj"'
