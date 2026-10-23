#requires -Version 5.1
$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$ApiPort = 5081
$ApiUrl = "http://127.0.0.1:$ApiPort"
$ApiDll = Join-Path $RepoRoot 'Ticket Manager API\bin\Release\net10.0\Ticket Manager API.dll'
$ConsoleDll = Join-Path $RepoRoot 'Ticket manadger\bin\Release\net10.0\Ticket manadger.dll'
$StatePath = Join-Path $env:LOCALAPPDATA 'TicketManager-Native\state.json'

function Unprotect-LocalText([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $null }
    $secure = ConvertTo-SecureString -String $Value
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
}
function Test-LocalApi {
    try {
        $health = Invoke-RestMethod -Uri "$ApiUrl/health" -TimeoutSec 3
        return ($health.status -eq 'ok' -and $health.database -eq 'connected' -and $health.migrations -eq 'current')
    } catch { return $false }
}

if (-not (Test-Path -LiteralPath $StatePath)) {
    throw "Локальная БД ещё не настроена. Сначала запусти Install-TicketManager-Native.ps1 из корня репозитория."
}
if (-not (Test-Path -LiteralPath $ApiDll) -or -not (Test-Path -LiteralPath $ConsoleDll)) {
    throw 'Не найдена сборка Release. Запусти Install-TicketManager-Native.ps1 ещё раз.'
}
$state = Get-Content -LiteralPath $StatePath -Raw | ConvertFrom-Json
$pgPort = [int]$state.PgPort
$appPassword = Unprotect-LocalText ([string]$state.AppPasswordProtected)
$apiTokenSecret = Unprotect-LocalText ([string]$state.ApiTokenSecretProtected)
$service = Get-Service -Name ([string]$state.ServiceName) -ErrorAction SilentlyContinue
if (-not $service) { throw "Служба PostgreSQL '$($state.ServiceName)' не найдена." }
if ($service.Status -ne 'Running') {
    try {
        Start-Service -Name $service.Name
        (Get-Service -Name $service.Name).WaitForStatus('Running', [TimeSpan]::FromSeconds(60))
    }
    catch { throw 'PostgreSQL остановлен. Запусти этот сценарий от имени администратора или запусти службу PostgreSQL вручную.' }
}
$dotnetCommand = Get-Command 'dotnet.exe' -ErrorAction SilentlyContinue
if (-not $dotnetCommand) { $dotnetCommand = Get-Command 'dotnet' -ErrorAction SilentlyContinue }
if (-not $dotnetCommand) { throw 'Не найден .NET SDK/runtime. Запусти Install-TicketManager-Native.ps1.' }
$dotnet = $dotnetCommand.Source
$env:TICKET_DB_CONNECTION = "Host=127.0.0.1;Port=$pgPort;Database=ticket_db;Username=ticket_app;Password=$appPassword;Timeout=15;Command Timeout=60"
$env:TICKET_API_TOKEN_SECRET = $apiTokenSecret
$env:ASPNETCORE_URLS = $ApiUrl
$env:ASPNETCORE_ENVIRONMENT = 'Production'
$env:TICKET_API_URL = $ApiUrl
Remove-Item Env:\TICKET_BOOTSTRAP_KEY -ErrorAction SilentlyContinue

if (-not (Test-LocalApi)) {
    $apiArguments = '"' + $ApiDll + '"'
    $apiProcess = Start-Process -FilePath $dotnet -ArgumentList $apiArguments -WorkingDirectory $RepoRoot -PassThru -WindowStyle Normal
    $ready = $false
    for ($i = 0; $i -lt 45; $i++) {
        Start-Sleep -Seconds 1
        if (Test-LocalApi) { $ready = $true; break }
        $apiProcess.Refresh()
        if ($apiProcess.HasExited) { throw "API завершился с кодом $($apiProcess.ExitCode). Проверь его окно." }
    }
    if (-not $ready) { throw "API не ответил на $ApiUrl. Проверь окно API и службу PostgreSQL." }
}
Write-Host "Ticket Manager подключён к локальной базе. API: $ApiUrl" -ForegroundColor Green
& $dotnet $ConsoleDll
