#requires -Version 5.1
[CmdletBinding()]
param([string]$OutputDirectory = (Join-Path $env:USERPROFILE 'TicketManager-Backups'))
$ErrorActionPreference = 'Stop'
$StatePath = Join-Path $env:LOCALAPPDATA 'TicketManager-Native\state.json'
if (-not (Test-Path -LiteralPath $StatePath)) { throw 'Не найдено состояние локальной установки Ticket Manager.' }
function Unprotect-LocalText([string]$Value) {
    $secure = ConvertTo-SecureString -String $Value
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
}
$state = Get-Content -LiteralPath $StatePath -Raw | ConvertFrom-Json
$port = [int]$state.PgPort
$appPassword = Unprotect-LocalText ([string]$state.AppPasswordProtected)
$pgDump = Join-Path $env:ProgramFiles 'PostgreSQL\16\bin\pg_dump.exe'
if (-not (Test-Path -LiteralPath $pgDump)) { throw "Не найден pg_dump.exe: $pgDump" }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$dumpPath = Join-Path $OutputDirectory "TicketManager-$stamp.dump"
$env:PGPASSWORD = $appPassword
try {
    & $pgDump -Fc -h 127.0.0.1 -p $port -U ticket_app -d ticket_db -f $dumpPath
    if ($LASTEXITCODE -ne 0) { throw 'pg_dump завершился с ошибкой.' }
} finally { Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue }
$hash = Get-FileHash -LiteralPath $dumpPath -Algorithm SHA256
$hashPath = "$dumpPath.sha256.txt"
Set-Content -LiteralPath $hashPath -Value "$($hash.Hash)  $(Split-Path -Leaf $dumpPath)" -Encoding ASCII
Write-Host "Резервная копия создана: $dumpPath" -ForegroundColor Green
Write-Host "SHA-256: $($hash.Hash)"
Write-Warning 'Дамп содержит данные пользователей, заявки и историю. Не коммить его в публичный GitHub; храни отдельно от исходного кода.'
