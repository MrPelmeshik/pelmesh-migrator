# pelmesh-migrator

Универсальный компонент для накатывания SQL-миграций на PostgreSQL. Подключается в другие проекты как соседняя папка (через `git clone`).

Мигратор:

- читает скрипты из каталога `--scripts`;
- каждый подкаталог трактует как имя базы данных;
- применяет только ещё не накатанные миграции;
- хранит историю в таблице `migration_history` и сверяет хеши уже применённых скриптов.

## Требования

- Docker и Docker Compose — основной способ запуска;
- PostgreSQL (базы должны существовать до запуска мигратора);
- .NET 8 SDK — только для локальной разработки и запуска без Docker.

## Структура скриптов

```
scripts/
  <имя_бд>/
    0001.описание.sql
    0002.описание.sql
```

Правила:

- имя подкаталога = имя БД в PostgreSQL (`Database=...`);
- имя файла: `NNNN.описание.sql`, где `NNNN` — порядковый номер из 4 цифр (например `0001.init.sql`);
- скрипты применяются по возрастанию номера;
- уже применённые файлы нельзя менять: при расхождении хеша мигратор завершится с ошибкой.

Пример в репозитории: [`examples/scripts`](examples/scripts).

## Подключение в другой проект

1. Клонируйте репозиторий рядом с вашим проектом:

```bash
git clone <url-pelmesh-migrator> ./pelmesh-migrator
```

Либо добавьте как git submodule в ту же папку.

2. В родительском `docker-compose.yml` добавьте сервис (адаптируйте пути и сеть под свой проект):

```yaml
services:
  migrator:
    container_name: migrator
    build:
      context: ./pelmesh-migrator/src
      dockerfile: Migrator/Dockerfile
    restart: "no"
    depends_on:
      postgres:
        condition: service_healthy
    command:
      - "--host"
      - "postgres"
      - "--port"
      - "5432"
      - "--user"
      - "postgres"
      - "--password-env"
      - "DB_PG_PWD_POSTGRES"
      - "--scripts"
      - "/app/scripts"
    environment:
      - DB_PG_PWD_POSTGRES=${DB_PG_PWD_POSTGRES:?DB_PG_PWD_POSTGRES is required}
    volumes:
      # Свои миграции родительского проекта
      - ./docker/postgres/scripts/migrations:/app/scripts:ro

  your-app:
    depends_on:
      migrator:
        condition: service_completed_successfully
      postgres:
        condition: service_healthy
```

3. Положите SQL в каталог, который монтируете в `/app/scripts` (структура — как выше).

4. Задайте `DB_PG_PWD_POSTGRES` в `.env` родительского проекта.

Базовый сервис из этого репозитория — [`docker-compose.yml`](docker-compose.yml); его можно копировать и менять `build.context` / volume под путь к клону.

## Аргументы CLI

| Аргумент | Описание |
|---|---|
| `--host` | Хост PostgreSQL |
| `--port` | Порт |
| `--user` | Пользователь |
| `--password` | Пароль напрямую |
| `--password-env` | Имя переменной окружения с паролем |
| `--scripts` | Путь к каталогу со скриптами |

Пример:

```bash
dotnet Migrator.dll \
  --host postgres \
  --port 5432 \
  --user postgres \
  --password-env DB_PG_PWD_POSTGRES \
  --scripts /app/scripts
```

## Локальный demo в этом репозитории

```bash
cp .env.example .env
# отредактируйте DB_PG_PWD_POSTGRES в .env

docker compose -f docker-compose.yml -f docker-compose.demo.yml up --build
```

Поднимается Postgres (`demo_db`) и one-shot контейнер `migrator`, который накатывает [`examples/scripts/demo_db`](examples/scripts/demo_db).

Переменные окружения описаны в [`.env.example`](.env.example).

## Запуск без Docker

```bash
dotnet run --project src/Migrator -- \
  --host localhost \
  --port 5432 \
  --user postgres \
  --password-env DB_PG_PWD_POSTGRES \
  --scripts ./examples/scripts
```

Предварительно должны быть доступны PostgreSQL и база(ы) с именами подкаталогов в `--scripts`.
