#requires -Version 5.1
$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $RepoRoot

function Invoke-ProjectStep([string]$ScriptPath) {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $ScriptPath
    if ($LASTEXITCODE -ne 0) {
        throw ("Шаг завершился с кодом {0}: {1}" -f $LASTEXITCODE, $ScriptPath)
    }
}

Write-Host ''
Write-Host 'Ticket Manager — запуск локального демонстрационного окружения' -ForegroundColor Cyan

$Scripts = $PSScriptRoot
Invoke-ProjectStep (Join-Path $Scripts 'Start-TicketManager-Docker.ps1')

$EnvPath = Join-Path $RepoRoot '.env'
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
    Invoke-ProjectStep (Join-Path $Scripts 'Create-TicketManager-Admin.ps1')
}

Invoke-ProjectStep (Join-Path $Scripts 'Start-TicketManager-Console.ps1')
