#requires -Version 5.1
$ErrorActionPreference = 'Stop'
$Root = $PSScriptRoot
Set-Location -LiteralPath $Root

function Invoke-ProjectStep([string]$ScriptPath) {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $ScriptPath
    if ($LASTEXITCODE -ne 0) {
        throw ("Шаг завершился с кодом {0}: {1}" -f $LASTEXITCODE, $ScriptPath)
    }
}

Write-Host ''
Write-Host 'Ticket Manager — запуск локального демонстрационного окружения' -ForegroundColor Cyan

Invoke-ProjectStep (Join-Path $Root 'Start-TicketManager-Docker.ps1')

$EnvPath = Join-Path $Root '.env'
if (-not (Test-Path -LiteralPath $EnvPath)) {
    throw 'Файл .env не появился после запуска Docker-сценария.'
}

$BootstrapLine = Get-Content -LiteralPath $EnvPath |
    Where-Object { $_.StartsWith('TICKET_BOOTSTRAP_KEY=') } |
    Select-Object -First 1

$BootstrapKey = if ($BootstrapLine) {
    $BootstrapLine.Substring('TICKET_BOOTSTRAP_KEY='.Length).Trim()
} else {
    ''
}

if (-not [string]::IsNullOrWhiteSpace($BootstrapKey)) {
    Write-Host ''
    Write-Host 'Первичная настройка: создай учётную запись администратора.' -ForegroundColor Yellow
    Invoke-ProjectStep (Join-Path $Root 'Create-TicketManager-Admin.ps1')
}

Invoke-ProjectStep (Join-Path $Root 'Start-TicketManager-Console.ps1')
