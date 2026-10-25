# Local Ticket Manager deployment: PostgreSQL + API in Docker

## What this starts

This is the recommended first deployment on your Windows PC:

- PostgreSQL runs in a Docker container.
- Ticket Manager API runs in a separate Docker container.
- The API is published only to 127.0.0.1 on the host, by default port 5080.
- The database files live in the persistent Docker volume ticket-manager-postgres-data.
- The existing console client runs on Windows as a temporary interface. The VK bot can replace that client later without replacing the API or database.

The PostgreSQL port is not published to Windows or the internet. Do not open database port 5432/5433 in the router.

## First startup

1. Install and start Docker Desktop.
2. Extract/clone the complete repository branch feature/local-postgres-defense.
3. Double-click Start-TicketManager-Docker.cmd.
4. On first launch, the script creates a local .env file with generated passwords and secrets, builds the API image, starts PostgreSQL and API, waits for migrations and the /health check.
5. The bootstrap key for creating the first administrator is copied to the clipboard. The key is stored only in the ignored local .env file; do not commit or share it.

The API health response should contain:
- status: ok
- database: connected
- migrations: current

You can verify it in PowerShell:

    Invoke-RestMethod http://127.0.0.1:5080/health | Format-List

If port 5080 is already used by an older API, stop that process or edit TICKET_API_PORT in .env to a free local port, then start again.

## Create the first administrator

Run Start-TicketManager-Docker.cmd first, then double-click Create-TicketManager-Admin.cmd. It starts the existing console client against the Docker API and supplies the one-time bootstrap key from .env. Enter the requested administrator name, login, email, and password.

After the console confirms successful creation, the script asks for confirmation. Type Y to remove TICKET_BOOTSTRAP_KEY from .env and recreate the API container. This disables first-admin setup while keeping normal login available. Do not confirm if the account was not created successfully.

## Use the console client

Double-click Start-TicketManager-Console.cmd to open the console client. It checks API health and connects to the configured local port.

The host needs .NET 10 SDK for the temporary console client. If you prefer PowerShell, run:

    $env:TICKET_API_URL = "http://127.0.0.1:5080"
    dotnet run --project "./Ticket manadger/Ticket manadger.csproj" The PostgreSQL database and API themselves are built and run by Docker.

## Routine commands

Show container status:

    docker compose ps

View API logs:

    docker compose logs -f api

View database logs:

    docker compose logs -f db

Stop containers without deleting the database:

    docker compose down

Start them again:

    docker compose up -d

Rebuild the API after code changes:

    docker compose up --build -d

The persistent volume is intentionally retained by docker compose down. **Do not use docker compose down -v** unless you intentionally want to delete all data in this Docker database.

## Backups

For a PostgreSQL dump, use Backup-TicketManager-Docker.ps1 after configuring it for the correct local container/database, or run a tested pg_dump workflow. Store dumps outside the repository: they contain user data and password hashes.

## Troubleshooting

- Docker not available: start Docker Desktop and wait for its engine to be ready.
- Build error: run docker compose build --no-cache api and inspect the first error.
- API not ready: run docker compose logs --tail 100 api and docker compose logs --tail 50 db.
- Health reports pending migrations: the API startup applies EF Core migrations automatically; review the API logs and restart with docker compose up -d after fixing the reported issue.
- Port conflict: change TICKET_API_PORT in .env and restart with docker compose up --build -d.
