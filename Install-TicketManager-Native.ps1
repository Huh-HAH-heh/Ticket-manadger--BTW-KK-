#requires -Version 5.1
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$RepoRoot = $PSScriptRoot
$ApiProject = Join-Path $RepoRoot 'Ticket Manager API\Ticket Manager API.csproj'
$ConsoleProject = Join-Path $RepoRoot 'Ticket manadger\Ticket manadger.csproj'
$StateDirectory = Join-Path $env:LOCALAPPDATA 'TicketManager-Native'
$StatePath = Join-Path $StateDirectory 'state.json'
$ApiPort = 5081
$ApiUrl = "http://127.0.0.1:$ApiPort"

function Write-Step([string]$Message) {
    Write-Host ""
    Write-Host "============================================================" -ForegroundColor DarkCyan
    Write-Host $Message -ForegroundColor Cyan
    Write-Host "============================================================" -ForegroundColor DarkCyan
}

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-IsAdministrator)) {
    Write-Host 'Для установки PostgreSQL требуется запуск от имени администратора. Открываю повышенное окно...' -ForegroundColor Yellow
    $arguments = '-NoProfile -ExecutionPolicy Bypass -File "' + $PSCommandPath + '"'
    Start-Process -FilePath 'powershell.exe' -ArgumentList $arguments -Verb RunAs
    exit
}

if (-not (Test-Path -LiteralPath $ApiProject) -or -not (Test-Path -LiteralPath $ConsoleProject)) {
    throw "Не найдены проекты. Запусти этот скрипт из корня полного репозитория Ticket Manager; ожидаемый корень: $RepoRoot"
}

New-Item -ItemType Directory -Path $StateDirectory -Force | Out-Null

function Protect-LocalText([string]$Value) {
    $secure = ConvertTo-SecureString -String $Value -AsPlainText -Force
    return ConvertFrom-SecureString -SecureString $secure
}

function Unprotect-LocalText([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $null }
    $secure = ConvertTo-SecureString -String $Value
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
}

function Save-LocalState($State) {
    $json = $State | ConvertTo-Json -Depth 5
    Set-Content -LiteralPath $StatePath -Value $json -Encoding UTF8
}

function New-RandomHex([int]$GuidCount = 2) {
    $parts = @()
    for ($i = 0; $i -lt $GuidCount; $i++) { $parts += [guid]::NewGuid().ToString('N') }
    return ($parts -join '')
}

function Test-ListeningPort([int]$Port) {
    try {
        $found = Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue | Select-Object -First 1
        return ($null -ne $found)
    } catch { return $false }
}

function Get-DotNetCommand {
    $command = Get-Command 'dotnet.exe' -ErrorAction SilentlyContinue
    if (-not $command) { $command = Get-Command 'dotnet' -ErrorAction SilentlyContinue }
    if (-not $command) { throw '.NET SDK не найден. Установи .NET 10 SDK и повтори запуск.' }
    return $command.Source
}

function Test-DotNet10Sdk {
    $sdkOutput = & (Get-DotNetCommand) --list-sdks 2>$null
    return [bool]($sdkOutput | Where-Object { $_ -match '^10\.' })
}

function Install-DotNet10Sdk {
    if (-not (Get-Command 'winget.exe' -ErrorAction SilentlyContinue)) {
        throw 'Не найден winget. Установи .NET 10 SDK вручную: https://dotnet.microsoft.com/download/dotnet/10.0'
    }
    Write-Step 'Установка .NET 10 SDK'
    & winget install --id Microsoft.DotNet.SDK.10 --exact --accept-source-agreements --accept-package-agreements
    if ($LASTEXITCODE -ne 0) { throw 'winget не смог установить .NET 10 SDK.' }
    $env:Path = "$env:ProgramFiles\dotnet;$env:USERPROFILE\.dotnet\tools;$env:Path"
    if (-not (Test-DotNet10Sdk)) { throw 'SDK установлен, но не найден текущим процессом. Закрой PowerShell и запусти скрипт снова.' }
}

function Get-PostgresService([string]$Name) {
    if (-not [string]::IsNullOrWhiteSpace($Name)) {
        return Get-Service -Name $Name -ErrorAction SilentlyContinue
    }
    return Get-Service -ErrorAction SilentlyContinue | Where-Object { $_.Name -match '^postgresql-x64-16$' } | Select-Object -First 1
}

function Start-PostgresService([string]$Name) {
    $service = Get-PostgresService $Name
    if (-not $service) { throw "Не найдена служба PostgreSQL '$Name'. Не удаляй данные; проверь установку PostgreSQL 16." }
    if ($service.Status -ne 'Running') {
        Start-Service -Name $service.Name
        $service = Get-Service -Name $service.Name
        $service.WaitForStatus('Running', [TimeSpan]::FromSeconds(90))
    }
    return $service
}

function Invoke-Psql([string]$PsqlPath, [int]$Port, [string]$Database, [string]$Sql) {
    $output = & $PsqlPath -X -w -h 127.0.0.1 -p $Port -U postgres -d $Database -v ON_ERROR_STOP=1 -c $Sql 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Команда PostgreSQL завершилась с ошибкой: $($output -join [Environment]::NewLine)" }
    return $output
}

Write-Step 'ЭТАП 1/7 — Проверка среды'
$dotnetCommand = Get-Command 'dotnet.exe' -ErrorAction SilentlyContinue
if (-not $dotnetCommand) { $dotnetCommand = Get-Command 'dotnet' -ErrorAction SilentlyContinue }
if (-not $dotnetCommand) { Install-DotNet10Sdk }
$dotnet = Get-DotNetCommand
if (-not (Test-DotNet10Sdk)) { Install-DotNet10Sdk; $dotnet = Get-DotNetCommand }
Write-Host "Найден .NET SDK 10; проект: $RepoRoot" -ForegroundColor Green

$state = $null
if (Test-Path -LiteralPath $StatePath) {
    try { $state = Get-Content -LiteralPath $StatePath -Raw | ConvertFrom-Json } catch { throw "Не удалось прочитать настройки $StatePath. Не удаляй файл: он содержит защищённые параметры локальной установки." }
}

if ($null -eq $state) {
    Write-Step 'ЭТАП 2/7 — Установка PostgreSQL 16 без Docker'
    $existingPg = Get-Service -ErrorAction SilentlyContinue | Where-Object { $_.Name -match '^postgresql-x64-' } | Select-Object -First 1
    if ($existingPg) {
        throw "Обнаружена существующая служба '$($existingPg.Name)', но нет локального состояния скрипта. Скрипт остановлен, чтобы случайно не менять существующую БД. Сначала сделай резервную копию и уточни текущие параметры PostgreSQL."
    }
    if (-not (Get-Command 'winget.exe' -ErrorAction SilentlyContinue)) {
        throw 'Не найден winget. Установи App Installer из Microsoft Store либо PostgreSQL 16 с https://www.postgresql.org/download/windows/ и повтори.'
    }

    $availablePort = @([int]5432, [int]5434, [int]5435, [int]5436) | Where-Object { -not (Test-ListeningPort $_) } | Select-Object -First 1
    if ($null -eq $availablePort) { throw 'Не найден свободный порт PostgreSQL среди 5432, 5434, 5435, 5436.' }
    $pgPort = [int]$availablePort
    $pgSuperPassword = New-RandomHex 2
    $pgServicePassword = 'Aa9!' + (New-RandomHex 2) + 'zZ'
    $appPassword = New-RandomHex 3
    $apiTokenSecret = New-RandomHex 3
    $bootstrapKey = New-RandomHex 3

    $installerArguments = "--mode unattended --unattendedmodeui minimal --superpassword $pgSuperPassword --servicepassword $pgServicePassword --serverport $pgPort --disable-components pgAdmin,stackbuilder"
    Write-Host "Устанавливаю PostgreSQL 16 на порт $pgPort. Установка может скачать несколько сотен мегабайт." -ForegroundColor Yellow
    & winget install --id PostgreSQL.PostgreSQL.16 --exact --override $installerArguments --accept-source-agreements --accept-package-agreements
    if ($LASTEXITCODE -ne 0) { throw "winget завершился с кодом $LASTEXITCODE при установке PostgreSQL." }

    $service = $null
    for ($attempt = 0; $attempt -lt 60 -and -not $service; $attempt++) {
        Start-Sleep -Seconds 2
        $service = Get-PostgresService 'postgresql-x64-16'
    }
    if (-not $service) { throw 'Установщик завершился, но служба postgresql-x64-16 не найдена.' }

    $state = [pscustomobject]@{
        SchemaVersion = 1
        PgVersion = 16
        PgPort = $pgPort
        ServiceName = $service.Name
        PgSuperPasswordProtected = Protect-LocalText $pgSuperPassword
        AppPasswordProtected = Protect-LocalText $appPassword
        ApiTokenSecretProtected = Protect-LocalText $apiTokenSecret
        BootstrapKeyProtected = Protect-LocalText $bootstrapKey
        InstalledAt = (Get-Date).ToString('o')
    }
    Save-LocalState $state
    Write-Host "Состояние установки сохранено в $StatePath (секреты зашифрованы Windows DPAPI для текущей учётной записи)." -ForegroundColor Green
} else {
    Write-Step 'ЭТАП 2/7 — Повторное использование локального PostgreSQL'
    if ([int]$state.PgVersion -ne 16) { throw 'Версия PostgreSQL в локальном состоянии не поддерживается этим скриптом.' }
    $pgPort = [int]$state.PgPort
    $service = Start-PostgresService ([string]$state.ServiceName)
    $pgSuperPassword = Unprotect-LocalText ([string]$state.PgSuperPasswordProtected)
    $appPassword = Unprotect-LocalText ([string]$state.AppPasswordProtected)
    $apiTokenSecret = Unprotect-LocalText ([string]$state.ApiTokenSecretProtected)
    $bootstrapKey = Unprotect-LocalText ([string]$state.BootstrapKeyProtected)
}

$pgSuperPassword = if ($state.PgSuperPasswordProtected) { Unprotect-LocalText ([string]$state.PgSuperPasswordProtected) } else { $pgSuperPassword }
$appPassword = if ($state.AppPasswordProtected) { Unprotect-LocalText ([string]$state.AppPasswordProtected) } else { $appPassword }
$apiTokenSecret = if ($state.ApiTokenSecretProtected) { Unprotect-LocalText ([string]$state.ApiTokenSecretProtected) } else { $apiTokenSecret }
$bootstrapKey = if ($state.BootstrapKeyProtected) { Unprotect-LocalText ([string]$state.BootstrapKeyProtected) } else { $null }
$service = Start-PostgresService ([string]$state.ServiceName)

$pgBin = Join-Path $env:ProgramFiles 'PostgreSQL\16\bin'
$psql = Join-Path $pgBin 'psql.exe'
$createdb = Join-Path $pgBin 'createdb.exe'
if (-not (Test-Path -LiteralPath $psql)) { throw "Не найден psql.exe: $psql" }
if (-not (Test-Path -LiteralPath $createdb)) { throw "Не найден createdb.exe: $createdb" }

Write-Step 'ЭТАП 3/7 — Создание/проверка локальной базы'
$env:PGPASSWORD = $pgSuperPassword
try {
    $roleSqlTemplate = @'
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'ticket_app') THEN
        CREATE ROLE ticket_app LOGIN PASSWORD '__APP_PASSWORD__';
    ELSE
        ALTER ROLE ticket_app WITH LOGIN PASSWORD '__APP_PASSWORD__';
    END IF;
END
$$;
'@
    $roleSql = $roleSqlTemplate.Replace('__APP_PASSWORD__', $appPassword)
    $sqlFile = Join-Path $StateDirectory 'provision-role.sql'
    Set-Content -LiteralPath $sqlFile -Value $roleSql -Encoding ASCII
    $sqlOutput = & $psql -X -w -h 127.0.0.1 -p $pgPort -U postgres -d postgres -v ON_ERROR_STOP=1 -f $sqlFile 2>&1
    $sqlExitCode = $LASTEXITCODE
    Remove-Item -LiteralPath $sqlFile -Force -ErrorAction SilentlyContinue
    if ($sqlExitCode -ne 0) { throw "Не удалось создать роль ticket_app: $($sqlOutput -join [Environment]::NewLine)" }

    $dbExists = [string]((& $psql -X -w -h 127.0.0.1 -p $pgPort -U postgres -d postgres -At -c "SELECT 1 FROM pg_database WHERE datname = 'ticket_db';" 2>&1 | Out-String).Trim())
    if ($LASTEXITCODE -ne 0) { throw "Не удалось проверить ticket_db: $dbExists" }
    if ($dbExists -ne '1') {
        $createOutput = & $createdb -h 127.0.0.1 -p $pgPort -U postgres -O ticket_app ticket_db 2>&1
        if ($LASTEXITCODE -ne 0) { throw "Не удалось создать ticket_db: $($createOutput -join [Environment]::NewLine)" }
    }
    Invoke-Psql $psql $pgPort 'postgres' 'ALTER DATABASE ticket_db OWNER TO ticket_app;'
    Invoke-Psql $psql $pgPort 'postgres' 'GRANT CONNECT, TEMPORARY ON DATABASE ticket_db TO ticket_app;'
} finally {
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
}

$connectionString = "Host=127.0.0.1;Port=$pgPort;Database=ticket_db;Username=ticket_app;Password=$appPassword;Timeout=15;Command Timeout=60"
$env:TICKET_DB_CONNECTION = $connectionString
$env:TICKET_API_TOKEN_SECRET = $apiTokenSecret
$env:ASPNETCORE_URLS = $ApiUrl
$env:ASPNETCORE_ENVIRONMENT = 'Production'
$env:TICKET_API_URL = $ApiUrl

Write-Step 'ЭТАП 4/7 — Зависимости и миграции EF Core'
Write-Host 'Восстанавливаю зависимости API и консоли...' -ForegroundColor Yellow
& $dotnet restore $ApiProject
if ($LASTEXITCODE -ne 0) { throw 'dotnet restore API завершился с ошибкой.' }
& $dotnet restore $ConsoleProject
if ($LASTEXITCODE -ne 0) { throw 'dotnet restore клиента завершился с ошибкой.' }

$env:Path = "$env:USERPROFILE\.dotnet\tools;$env:Path"
$efVersion = (& $dotnet ef --version 2>$null | Out-String).Trim()
if ($efVersion -notmatch '10\.0\.13') {
    & $dotnet tool update --global dotnet-ef --version 10.0.13
    if ($LASTEXITCODE -ne 0) {
        & $dotnet tool install --global dotnet-ef --version 10.0.13
        if ($LASTEXITCODE -ne 0) { throw 'Не удалось установить dotnet-ef 10.0.13.' }
    }
    $env:Path = "$env:USERPROFILE\.dotnet\tools;$env:Path"
}

& $dotnet build $ApiProject -c Debug --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Сборка API (Debug) завершилась с ошибкой.' }
& $dotnet ef database update --project $ApiProject --no-build --configuration Debug
if ($LASTEXITCODE -ne 0) { throw 'Миграции EF Core завершились с ошибкой.' }
& $dotnet build $ApiProject -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Сборка API (Release) завершилась с ошибкой.' }
& $dotnet build $ConsoleProject -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Сборка консоли (Release) завершилась с ошибкой.' }

$env:PGPASSWORD = $appPassword
try {
    $adminSql = 'SELECT COUNT(*) FROM users WHERE role = ''Admin'' AND NOT "isDeleted";'
    $adminCountText = [string]((& $psql -X -w -h 127.0.0.1 -p $pgPort -U ticket_app -d ticket_db -At -c $adminSql 2>&1 | Out-String).Trim())
    if ($LASTEXITCODE -ne 0) { throw "Не удалось проверить первого администратора: $adminCountText" }
    $adminCount = [int]$adminCountText
} finally {
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
}

$apiDll = Join-Path $RepoRoot 'Ticket Manager API\bin\Release\net10.0\Ticket Manager API.dll'
$consoleDll = Join-Path $RepoRoot 'Ticket manadger\bin\Release\net10.0\Ticket manadger.dll'
if (-not (Test-Path -LiteralPath $apiDll)) { throw "Не найден собранный API: $apiDll" }
if (-not (Test-Path -LiteralPath $consoleDll)) { throw "Не найден собранный клиент: $consoleDll" }

function Test-LocalApi {
    try {
        $health = Invoke-RestMethod -Uri "$ApiUrl/health" -TimeoutSec 3
        return ($health.status -eq 'ok' -and $health.database -eq 'connected' -and $health.migrations -eq 'current')
    } catch { return $false }
}

function Start-LocalApi {
    Write-Host "Запускаю API по адресу $ApiUrl ..." -ForegroundColor Yellow
    $apiArguments = '"' + $apiDll + '"'
    $apiProcess = Start-Process -FilePath $dotnet -ArgumentList $apiArguments -WorkingDirectory $RepoRoot -PassThru -WindowStyle Normal
    for ($attempt = 0; $attempt -lt 45; $attempt++) {
        Start-Sleep -Seconds 1
        if (Test-LocalApi) { return $apiProcess }
        $apiProcess.Refresh()
        if ($apiProcess.HasExited) { throw "API завершился с кодом $($apiProcess.ExitCode). Смотри его окно с логом." }
    }
    throw "API не ответил на $ApiUrl за 45 секунд. Проверь окно API."
}

Write-Step 'ЭТАП 5/7 — Запуск и проверка API'
$apiProcess = $null
if (Test-LocalApi) {
    Write-Host 'API уже работает и отвечает корректно.' -ForegroundColor Green
} else {
    if ($adminCount -eq 0 -and [string]::IsNullOrWhiteSpace($bootstrapKey)) {
        $bootstrapKey = New-RandomHex 3
        $state.BootstrapKeyProtected = Protect-LocalText $bootstrapKey
        Save-LocalState $state
    }
    if ($adminCount -gt 0) {
        Remove-Item Env:\TICKET_BOOTSTRAP_KEY -ErrorAction SilentlyContinue
    } else {
        $env:TICKET_BOOTSTRAP_KEY = $bootstrapKey
    }
    $apiProcess = Start-LocalApi
}

if ($adminCount -eq 0) {
    if ([string]::IsNullOrWhiteSpace($bootstrapKey)) { throw 'Нет активного администратора и отсутствует ключ первичной настройки. Запуск остановлен.' }
    $env:TICKET_BOOTSTRAP_KEY = $bootstrapKey
    Write-Step 'ЭТАП 6/7 — Создание первого администратора'
    Write-Host 'Создай аккаунт администратора в открывшейся консоли. Пароль не записывается в скрипт.' -ForegroundColor Yellow
    & $dotnet $consoleDll --bootstrap-admin
    if ($LASTEXITCODE -ne 0) { throw 'Консоль завершилась с ошибкой во время первичной настройки администратора.' }

    $env:PGPASSWORD = $appPassword
    try {
        $adminCountText = [string]((& $psql -X -w -h 127.0.0.1 -p $pgPort -U ticket_app -d ticket_db -At -c $adminSql 2>&1 | Out-String).Trim())
        if ($LASTEXITCODE -ne 0 -or [int]$adminCountText -lt 1) {
            throw 'В базе не найден активный администратор после первичной настройки. Ключ оставлен для повтора; проверь результат в консоли.'
        }
    } finally { Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue }

    $state.BootstrapKeyProtected = $null
    Save-LocalState $state
    Remove-Item Env:\TICKET_BOOTSTRAP_KEY -ErrorAction SilentlyContinue
    if ($apiProcess -and -not $apiProcess.HasExited) {
        Stop-Process -Id $apiProcess.Id -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
        $apiProcess = Start-LocalApi
    }
} else {
    Remove-Item Env:\TICKET_BOOTSTRAP_KEY -ErrorAction SilentlyContinue
}

Write-Step 'ЭТАП 7/7 — Запуск консольного клиента'
$env:TICKET_API_URL = $ApiUrl
Remove-Item Env:\TICKET_BOOTSTRAP_KEY -ErrorAction SilentlyContinue
Write-Host 'Локальная установка готова. Клиент работает с API и PostgreSQL на этом же компьютере.' -ForegroundColor Green
Write-Host "API: $ApiUrl/health"
Write-Host "Настройки (DPAPI): $StatePath"
Write-Host 'Для повторного запуска используй Start-TicketManager-Native.cmd в корне репозитория.'
Write-Host 'API останется в отдельном окне после выхода из клиента.' -ForegroundColor Yellow
& $dotnet $consoleDll
