# pelmesh-migrator

Универсальный компонент для накатывания SQL-миграций на PostgreSQL. Подключается в другие проекты как соседняя папка (`git clone` / submodule).

Версия: **2.0.0** — см. [CHANGELOG.md](CHANGELOG.md).

Мигратор:

- читает скрипты из `--scripts` согласно `--layout`;
- применяет только ещё не накатанные миграции;
- хранит историю в `migration_history`;
- по умолчанию сверяет хеши уже применённых скриптов (`--verify-hash`).

**Ограничение:** создание БД, ролей, грантов и seed — на стороне вызывающего. Migrator только накатывает SQL в уже существующие базы.

## Требования

- Docker и Docker Compose — основной способ запуска;
- PostgreSQL с заранее созданными базами;
- .NET 8 SDK — только для локальной разработки без Docker.

## Быстрый старт: внедрение в свой проект

1. Клонируйте рядом с проектом:

```bash
git clone <url-pelmesh-migrator> ./pelmesh-migrator
# или git submodule
```

2. Возьмите шаблон [`docker-compose.yml`](docker-compose.yml) — один из способов:

**А. Скопировать сервис** `migrator` в родительский compose (самый простой путь).

**Б. Подключить файл** (поправьте пути):

```yaml
# родительский docker-compose.yml
include:
  - path: ./pelmesh-migrator/docker-compose.yml
    env_file: .env

services:
  postgres:
    # ... ваш Postgres ...
    networks: [app_net]

  your-app:
    depends_on:
      migrator:
        condition: service_completed_successfully
    networks: [app_net]

networks:
  app_net:
    name: app_net
```

В `.env` родителя:

```env
DB_PG_PWD_POSTGRES=secret
MIGRATOR_BUILD_CONTEXT=./pelmesh-migrator/src
MIGRATOR_SCRIPTS_PATH=./docker/postgres/scripts/migrations
MIGRATOR_NETWORK=app_net
MIGRATOR_DB_HOST=postgres
```

И в шаблоне migrator выставьте сеть как external (или объедините сети вручную) — см. комментарии в [`docker-compose.yml`](docker-compose.yml).

**В. Минимальный фрагмент** (вставка в свой compose):

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
      DB_PG_PWD_POSTGRES: ${DB_PG_PWD_POSTGRES:?DB_PG_PWD_POSTGRES is required}
    volumes:
      - ./docker/postgres/scripts/migrations:/app/scripts:ro
```

3. Положите SQL в смонтированный каталог (структура — по `--layout`, default `nested`).

4. Задайте переменные из [`.env.example`](.env.example).

Приложение должно ждать успешного завершения migrator:

```yaml
depends_on:
  migrator:
    condition: service_completed_successfully
```

### Сборка образа со своим registry

```bash
docker build \
  --build-arg RUNTIME_IMAGE=your-mirror/dotnet/runtime:8.0 \
  --build-arg SDK_IMAGE=your-mirror/dotnet/sdk:8.0 \
  -f src/Migrator/Dockerfile \
  src
```

Те же значения можно задать через `MIGRATOR_RUNTIME_IMAGE` / `MIGRATOR_SDK_IMAGE` в compose.

### Пример one-shot Job (Kubernetes)

```yaml
apiVersion: batch/v1
kind: Job
metadata:
  name: pelmesh-migrator
spec:
  template:
    spec:
      restartPolicy: Never
      containers:
        - name: migrator
          image: your-registry/pelmesh-migrator:2.0.0
          args:
            - "--host"
            - "postgres"
            - "--port"
            - "5432"
            - "--user"
            - "postgres"
            - "--password-env"
            - "POSTGRES_PASSWORD"
            - "--scripts"
            - "/scripts"
          env:
            - name: POSTGRES_PASSWORD
              valueFrom:
                secretKeyRef:
                  name: postgres
                  key: password
          volumeMounts:
            - name: scripts
              mountPath: /scripts
              readOnly: true
      volumes:
        - name: scripts
          configMap:
            name: sql-migrations
```

## Layout и имена файлов

| `--layout` | Откуда БД | Шаблон имени |
|---|---|---|
| `nested` (default) | имя подкаталога | `<digits>.<desc>.sql` внутри `scripts/<db>/` |
| `flat` | `--database` (обязателен) | `<digits>.<desc>.sql` в корне `--scripts` |
| `by-filename` | сегмент `<database>` в имени | `<digits>.<database>.<rest>.sql` |

`<digits>` — `\d+` (`1`, `001`, `0001`, `10001`). Сортировка по числовому `order`.

Примеры `by-filename`:

- `001.dq.meta.sql` → order=`1`, database=`dq`, name=`meta`
- `1806.dq.meta.alter_views.sql` → order=`1806`, database=`dq`, name=`meta.alter_views`

В `nested` сегмент БД в имени **не** переопределяет папку; при несовпадении сегмента с именем папки — **WARNING**.

`*.sql` вне regex: по умолчанию **ошибка** со списком путей; `--ignore-unmatched` — WARNING + skip.

Дубликаты `order` в одной БД — fail до применения. «Дыры» в нумерации допустимы.

Пример структуры для `nested`:

```text
scripts/
  <имя_бд>/
    0001.init.sql
    0002.add_users.sql
```

## Политика ошибок и транзакций

| `--on-error` | Поведение |
|---|---|
| `fail` (default) | ошибка прерывает прогон |
| `continue` | ошибка не останавливает следующие statements/файлы |

| `--transaction` | Поведение |
|---|---|
| `all` (default при `fail`) | одна транзакция на все pending файлы одной БД |
| `per-file` (default при `continue`) | транзакция на файл |
| `none` | без обёртки |

`continue` + `all` запрещены.

При `continue`: statements внутри файла выполняются по отдельности (dollar-quoting учитывается); при `per-file` ошибки откатываются через savepoints. В history — `applied_with_errors`. `--fail-on-script-errors` — exit ≠ 0, если были script errors.

При `on-error=fail` файл уходит в PostgreSQL целиком.

## Проверка хешей и store-sql

- `--verify-hash` / `--no-verify-hash` (default: `true`);
- при расхождении hash applied-файла → fail;
- при `--no-verify-hash` — skip по order без сравнения содержимого; WARNING в лог;
- hash всегда считается и пишется в history при применении.

`--store-sql true|false` (default `true`): при `false` в колонку `sql` пишется пустая строка.

## История миграций

Таблица `migration_history` (create + безопасный upgrade):

| Поле | Назначение |
|---|---|
| `order_number` | номер (unique в пределах БД) |
| `file_name` | имя файла (unique) |
| `name` | описание |
| `sql` | текст скрипта |
| `hash` | SHA-256 |
| `applied_at` | timestamptz |
| `status` | `applied` \| `applied_with_errors` |
| `error_text` | опционально, обрезанный |

## CLI и env

| Аргумент | Описание |
|---|---|
| `--host` | Хост PostgreSQL |
| `--port` | Порт |
| `--user` | Пользователь |
| `--password` | Пароль напрямую |
| `--password-env` | Имя env с паролем |
| `--scripts` | Каталог со скриптами |
| `--layout` | `nested` \| `flat` \| `by-filename` |
| `--database` | БД для `flat` / фильтр для `by-filename` |
| `--on-error` | `fail` \| `continue` |
| `--transaction` | `all` \| `per-file` \| `none` |
| `--ignore-unmatched` | WARNING + skip unmatched `*.sql` |
| `--fail-on-script-errors` | exit ≠ 0 при script errors в continue |
| `--verify-hash` / `--no-verify-hash` | проверка хешей (default on) |
| `--store-sql` | `true`\|`false` (default true) |

Env fallback, если аргумент не задан:

| Параметр | Env |
|---|---|
| host | `POSTGRES_HOST` |
| port | `POSTGRES_PORT` |
| user | `POSTGRES_USER` |
| password | `POSTGRES_PASSWORD` или `--password-env` |
| database | `POSTGRES_DB` |

Summary в stdout: `databases=N applied=A skipped=S errors=E`.

Коды выхода: `0` при успехе; ≠0 при ошибках конфигурации, `on-error=fail`, hash mismatch (если verify включён), либо script errors при `--fail-on-script-errors`.

```bash
dotnet Migrator.dll \
  --host postgres \
  --port 5432 \
  --user postgres \
  --password-env DB_PG_PWD_POSTGRES \
  --scripts /app/scripts
```

## Запуск без Docker

```bash
dotnet run --project src/Migrator -- \
  --host localhost \
  --port 5432 \
  --user postgres \
  --password-env DB_PG_PWD_POSTGRES \
  --scripts ./examples/scripts
```

PostgreSQL и базы с именами согласно layout должны быть доступны заранее. Пример скриптов: [`examples/scripts`](examples/scripts).
