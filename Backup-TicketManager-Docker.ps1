#requires -Version 5.1
[CmdletBinding()]
param([string]$OutputDirectory = (Join-Path $env:USERPROFILE 'TicketManager-Backups'))
$ErrorActionPreference = 'Stop'
$container = 'ticket-postgres'
$check = & docker inspect -f '{{.State.Running}}' $container 2>&1
if ($LASTEXITCODE -ne 0 -or [string]$check -ne 'true') {
    throw "Docker-контейнер '$container' не найден или не запущен. Исходная БД не изменялась."
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$insidePath = "/tmp/ticketmanager-$stamp.dump"
$dumpPath = Join-Path $OutputDirectory "TicketManager-Docker-$stamp.dump"
& docker exec --user postgres $container pg_dump -Fc -U postgres -d ticket_db -f $insidePath
if ($LASTEXITCODE -ne 0) { throw 'pg_dump в контейнере завершился с ошибкой.' }
try {
    $sourcePath = $container + ':' + $insidePath
    & docker cp $sourcePath $dumpPath
    if ($LASTEXITCODE -ne 0) { throw 'Не удалось скопировать дамп из контейнера на компьютер.' }
} finally {
    & docker exec $container rm -f $insidePath | Out-Null
}
$hash = Get-FileHash -LiteralPath $dumpPath -Algorithm SHA256
Set-Content -LiteralPath "$dumpPath.sha256.txt" -Value "$($hash.Hash)  $(Split-Path -Leaf $dumpPath)" -Encoding ASCII
Write-Host "Резервная копия Docker PostgreSQL сохранена: $dumpPath" -ForegroundColor Green
Write-Host "SHA-256: $($hash.Hash)"
Write-Warning 'Дамп может содержать личные данные и хэши паролей. Не загружай его в публичный GitHub.'
