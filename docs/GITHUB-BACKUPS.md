# Контрольные точки GitHub и резервные копии данных

## Резервные точки исходного кода

Git хранит историю проекта в основной ветке `main`. Несколько прежних резервных веток указывали на коммиты, которые уже входят в историю `main`; для доступа к ним используй неизменяемые SHA:

- Состояние этапа 5 до локального установщика: [ff99b033](https://github.com/Huh-HAH-heh/Ticket-manadger--BTW-KK-/tree/ff99b0335b5002a394e488c51163e701f3d9536a).
- Снимок с инструкциями защиты: [5ba3b78e](https://github.com/Huh-HAH-heh/Ticket-manadger--BTW-KK-/tree/5ba3b78eb53b4358cd5c57eb45581560cef0b2aa).
- Сценарий локального PostgreSQL: [b4fb1fc9](https://github.com/Huh-HAH-heh/Ticket-manadger--BTW-KK-/tree/b4fb1fc9ee4c74711e3285539002c7ce379cb2dd).

Старая экспериментальная настройка PostgreSQL 18, имевшая два уникальных коммита, сохранена отдельно в [archive/stage-5-db-setup-pg18](https://github.com/Huh-HAH-heh/Ticket-manadger--BTW-KK-/tree/archive/stage-5-db-setup-pg18). Она не является текущим сценарием запуска.

Эти SHA — исторические контрольные точки, а не текущие точки разработки. Основная разработка и демонстрация ведутся из `main`.

## Резервные копии PostgreSQL

GitHub хранит исходный код, но не копию реальной базы. Данные PostgreSQL не следует коммитить в публичный репозиторий: дамп содержит заявки, контакты, историю и хэши паролей.

Для локальных дампов предусмотрены два сценария:

- Docker PostgreSQL: `scripts/Backup-TicketManager-Docker.ps1`.
- Старый Native PostgreSQL: `legacy/native/Backup-TicketManager-Native.ps1`.

Оба сценария сохраняют файлы по умолчанию в `%USERPROFILE%\TicketManager-Backups` и создают рядом контрольную сумму SHA-256. Копируй этот каталог отдельно на флешку или в приватное хранилище. Не добавляй дампы, `.env`, файл состояния DPAPI или секреты в Git.
