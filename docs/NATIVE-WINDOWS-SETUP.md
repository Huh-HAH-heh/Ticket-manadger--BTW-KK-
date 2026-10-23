# Локальный запуск Ticket Manager без Docker

Основное руководство по сценарию «PostgreSQL + API + консоль на одном Windows-компьютере»:

**[docs/DEFENSE-SCENARIO-B-LOCAL-WINDOWS.md](DEFENSE-SCENARIO-B-LOCAL-WINDOWS.md)**

## Кратко

1. Скачай и полностью распакуй полный ZIP исходного проекта ветки feature/local-postgres-defense:
   https://github.com/Huh-HAH-heh/Ticket-manadger--BTW-KK-/archive/refs/heads/feature/local-postgres-defense.zip
2. В корне проекта запусти Start-TicketManager-OneClick.cmd.
3. Подтверди UAC при запросе Windows. Для первой установки нужны права администратора и интернет.
4. Дождись завершения установки PostgreSQL 16 как службы Windows, миграций и сборки.
5. При первом запуске создай администратора в консоли.
6. Для следующих запусков снова используй Start-TicketManager-OneClick.cmd.

Не путай полный ZIP проекта с TicketManager-College.zip: последний содержит только консольный клиент для сценария с домашним API и Cloudflare Tunnel.

## Данные и безопасность

- Локальная база создаётся отдельно и не импортирует автоматически данные Docker-контейнера.
- Локальные параметры хранятся в %LOCALAPPDATA%\TicketManager-Native\state.json, секреты защищены Windows DPAPI текущей учётной записи.
- Для резервной копии локальной базы запускай Backup-TicketManager-Native.ps1.
- Для резервной копии существующего Docker-контейнера запускай Backup-TicketManager-Docker.ps1.
- Не коммить дампы базы, файл state.json, пароли или токены в публичный GitHub.

## Ограничения

Сценарий предназначен для демонстрации на одном компьютере. При первой установке ему требуется интернет и права администратора; если политика колледжа запрещает устанавливать службы или приложения, выбери [сценарий A с клиентским архивом](DEFENSE-SCENARIO-A-DOCKER-CLOUDFLARE.md) и заранее подготовь домашний компьютер.
