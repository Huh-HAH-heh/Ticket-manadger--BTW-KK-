# Сценарий A: Docker PostgreSQL дома, клиент в колледже

## Когда выбирать

Выбирай этот сценарий, если домашний ПК можно оставить включённым, а в колледже есть интернет. PostgreSQL и API работают дома; на компьютере колледжа запускается только консольный клиент. VPS, покупка облачной базы и настройка роутера не нужны.

## Схема

    ПК колледжа: TicketManager-College.zip
             │ HTTPS
             ▼
    Cloudflare Quick Tunnel
             │
             ▼
    Домашний ПК: Ticket Manager API на 127.0.0.1:5080
             │
             ▼
    Docker PostgreSQL на 127.0.0.1:5433

Публичным становится только API через HTTPS-туннель. PostgreSQL не публикуй наружу. Не меняй привязку порта на 0.0.0.0 и не открывай порт 5432/5433 в роутере.

## Требования на домашнем ПК

- Windows 10/11 x64.
- Запущенный Docker Desktop.
- .NET 10 SDK.
- dotnet-ef версии 10.0.x, желательно 10.0.13.
- cloudflared.
- Полный проект Ticket Manager.

Проверь команды:

\`\`\`powershell
docker info
dotnet --version
dotnet ef --version
cloudflared --version
\`\`\`

## Вариант запуска автоматизатором

Если у тебя уже есть пакет TicketManager-Defense-Automation.zip из чата, распакуй его. Запусти PowerShell и выполни:

\`\`\`powershell
Set-ExecutionPolicy -Scope Process Bypass -Force
& "$env:USERPROFILE\Downloads\Start-TicketManager-Defense.ps1"
\`\`\`

Путь предполагает, что скрипт лежит в Downloads. Скрипт ожидает проект в D:\focx\Новая папка\Ticket-manadger--BTW-KK- либо рядом со скриптом. Он проверяет Docker, использует контейнер ticket-postgres, применяет миграции, запускает API, поднимает Quick Tunnel, проверяет публичный /health и создаёт TicketManager-College.zip.

Если автоматизатора нет, выполни ручные шаги ниже.

## Ручной запуск на домашнем ПК

### 1. PostgreSQL

Если контейнер ticket-postgres уже есть, не создавай второй и не удаляй том. Проверь его:

\`\`\`powershell
docker ps -a
docker logs --tail 20 ticket-postgres
\`\`\`

Если контейнер остановлен, запусти его:

\`\`\`powershell
docker start ticket-postgres
\`\`\`

Если контейнера ещё нет, создай его (в этом случае пароль будет в переменной текущего PowerShell):

\`\`\`powershell
$dbPassword = "Ticket_" + [guid]::NewGuid().ToString("N") + "A9!"
docker run -d --name ticket-postgres --restart unless-stopped -e POSTGRES_USER=postgres -e "POSTGRES_PASSWORD=$dbPassword" -e POSTGRES_DB=ticket_db -p 127.0.0.1:5433:5432 -v ticket_pgdata:/var/lib/postgresql/data postgres:16
\`\`\`

Если контейнер уже существует, можно прочитать пароль в переменную, не выводя его на экран:

\`\`\`powershell
$info = (docker inspect ticket-postgres | ConvertFrom-Json)[0]
$envLine = $info.Config.Env | Where-Object { $_ -like 'POSTGRES_PASSWORD=*' } | Select-Object -First 1
$dbPassword = $envLine.Substring('POSTGRES_PASSWORD='.Length)
\`\`\`

В логах дождись сообщения database system is ready to accept connections.

### 2. Подготовить и применить миграции

В том же PowerShell:

\`\`\`powershell
cd "D:\focx\Новая папка\Ticket-manadger--BTW-KK-"
$env:TICKET_DB_CONNECTION = "Host=127.0.0.1;Port=5433;Database=ticket_db;Username=postgres;Password=$dbPassword"
$env:TICKET_API_TOKEN_SECRET = [guid]::NewGuid().ToString("N") + [guid]::NewGuid().ToString("N")
$env:ASPNETCORE_URLS = "http://127.0.0.1:5080"
# Только если в этой базе ещё нет администратора:
$env:TICKET_BOOTSTRAP_KEY = [guid]::NewGuid().ToString("N") + [guid]::NewGuid().ToString("N")
Set-Clipboard $env:TICKET_BOOTSTRAP_KEY
dotnet tool update --global dotnet-ef --version 10.0.13
dotnet ef database update --project "Ticket Manager API/Ticket Manager API.csproj"
\`\`\`

Если dotnet-ef ещё не установлен, замени update на install.

### 3. Запустить API

В том же окне, в котором заданы переменные:

\`\`\`powershell
dotnet run --no-launch-profile --project "Ticket Manager API/Ticket Manager API.csproj"
\`\`\`

Оставь это окно открытым. В другом PowerShell проверь:

\`\`\`powershell
Invoke-RestMethod "http://127.0.0.1:5080/health"
\`\`\`

Нужен ответ status=ok, database=connected, migrations=current.

### 4. Создать HTTPS-туннель

В отдельном окне PowerShell:

\`\`\`powershell
cloudflared tunnel --protocol http2 --url http://127.0.0.1:5080
\`\`\`

Скопируй HTTPS-адрес вида https://some-words.trycloudflare.com. В другом окне проверь:

\`\`\`powershell
Invoke-RestMethod "https://ТВОЙ-АДРЕС.trycloudflare.com/health"
\`\`\`

Проверяй адрес из текущего окна cloudflared. Quick Tunnel временный: при перезапуске адрес обычно меняется. Ошибка 1033 означает, что туннель не виден Cloudflare или процесс остановлен.

### 5. Создать администратора, только если его ещё нет

Ключ уже был создан и скопирован в буфер обмена на этапе 2. Не генерируй новый ключ, пока API работает: ключ в API и клиенте должен совпадать. Если администратор уже существует, этот шаг пропусти.

В отдельном PowerShell задай публичный адрес и возьми тот же ключ из буфера:

\`\`\`powershell
$env:TICKET_API_URL = "https://ТВОЙ-АДРЕС.trycloudflare.com"
$env:TICKET_BOOTSTRAP_KEY = (Get-Clipboard -Raw).Trim()
dotnet run --project "Ticket manadger/Ticket manadger.csproj" -- --bootstrap-admin
\`\`\`

Введи данные администратора в консоли. Не отправляй пароль и ключ в чат. После сообщения об успешном создании останови API Ctrl+C, удали ключ из окружения и запусти API снова:

\`\`\`powershell
Remove-Item Env:\TICKET_BOOTSTRAP_KEY -ErrorAction SilentlyContinue
dotnet run --no-launch-profile --project "Ticket Manager API/Ticket Manager API.csproj"
\`\`\`

Если действующий администратор уже есть, повторное создание не выполняй.

### 6. Создать клиентский архив

После успешной внешней проверки /health открой новый PowerShell:

\`\`\`powershell
cd "D:\focx\Новая папка\Ticket-manadger--BTW-KK-"
$env:TICKET_API_URL = "https://ТВОЙ-АДРЕС.trycloudflare.com"
dotnet publish ".\Ticket manadger\Ticket manadger.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ".\publish-client"
$exe = Get-ChildItem ".\publish-client\*.exe" | Select-Object -First 1
if (-not $exe) { throw "Не найден EXE в publish-client" }
@"
@echo off
setlocal
set "TICKET_API_URL=$env:TICKET_API_URL"
"%~dp0$($exe.Name)"
endlocal
"@ | Set-Content ".\publish-client\Start-TicketManager.cmd" -Encoding ASCII
Compress-Archive -Path ".\publish-client\*" -DestinationPath ".\TicketManager-College.zip" -Force
\`\`\`

TicketManager-College.zip — это только переносимый клиент. В нём нет API или базы данных.

## На компьютере колледжа

1. Перенеси TicketManager-College.zip на флешке.
2. Распакуй ZIP.
3. Запусти Start-TicketManager.cmd.
4. Войди под заранее созданной учётной записью.

Для Windows x64 не нужны Docker и .NET SDK: клиент опубликован как self-contained.

## Перед защитой

- Домашний ПК должен быть включён и не должен уходить в сон.
- Окно API и окно cloudflared должны оставаться открытыми.
- Если URL туннеля изменился, повторно собери TicketManager-College.zip с новым адресом.
- Не используй реальные персональные данные: Quick Tunnel предназначен для временной демонстрации, не для production.
- Для дампа Docker-базы используй Backup-TicketManager-Docker.ps1. Храни дамп отдельно от GitHub.
