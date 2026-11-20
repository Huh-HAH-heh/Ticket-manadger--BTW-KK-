#requires -Version 5.1
[CmdletBinding()]
param([string]$OutputDirectory = (Join-Path $env:USERPROFILE 'TicketManager-Backups'))

$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $RepoRoot

try {
    $containerId = (& docker compose ps -q db 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($containerId)) {
        throw 'Docker Compose service db is not running. Start Ticket Manager before creating a backup.'
    }

    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $insidePath = "/tmp/ticketmanager-$stamp.dump"
    $dumpPath = Join-Path $OutputDirectory "TicketManager-Docker-$stamp.dump"

    & docker compose exec -T db sh -c "pg_dump -U ticket_app -Fc ticket_db > $insidePath"
    if ($LASTEXITCODE -ne 0) {
        throw 'pg_dump in the Docker PostgreSQL service failed.'
    }

    try {
        & docker cp "$($containerId):$insidePath" $dumpPath
        if ($LASTEXITCODE -ne 0) {
            throw 'Could not copy the backup out of the PostgreSQL container.'
        }
    }
    finally {
        & docker compose exec -T db rm -f $insidePath | Out-Null
    }

    $hash = Get-FileHash -LiteralPath $dumpPath -Algorithm SHA256
    Set-Content -LiteralPath "$dumpPath.sha256.txt" -Value "$($hash.Hash)  $(Split-Path -Leaf $dumpPath)" -Encoding ASCII

    Write-Host "Docker PostgreSQL backup saved: $dumpPath" -ForegroundColor Green
    Write-Host "SHA-256: $($hash.Hash)"
    Write-Warning 'The dump may contain personal data and password hashes. Do not upload it to public GitHub.'
}
finally {
    Pop-Location
}
