# Legacy native Windows launcher

This folder contains the optional legacy deployment path that installs PostgreSQL 16 as a Windows service.

It is not the standard portfolio/demo setup. For the supported Docker-based demonstration, use `Start-TicketManager.cmd` in the repository root and follow [the operations guide](../../docs/OPERATIONS.md).

The native scripts are preserved for the older defense scenario. They require administrator approval during first installation and should not be mixed with the Docker database.

- `Start-TicketManager-OneClick.cmd` — first-time native installation and subsequent launch.
- `Start-TicketManager-Native.cmd` — direct launch after native installation.
- `Install-TicketManager-Native.ps1` — native PostgreSQL installer.
- `Backup-TicketManager-Native.ps1` — backup utility for that separate database.

The shared native API startup helper is in `scripts/Start-TicketManager-Native.ps1`. Source projects and API code remain in the repository root.
