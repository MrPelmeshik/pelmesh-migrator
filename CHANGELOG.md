# Changelog

## 2.0.0

### Breaking

- Файлы `*.sql`, не прошедшие активный regex layout, больше не пропускаются тихо: по умолчанию запуск завершается с ошибкой и списком путей. Обход: `--ignore-unmatched` (WARNING + skip).

### Added

- Номер миграции: любое количество цифр до первой точки (`\d+`), сортировка по числовому `order`.
- `--layout nested|flat|by-filename` — выбор БД из подкаталога, из `--database` или из сегмента имени файла.
- `--database` — обязателен для `flat`; фильтр для `by-filename`; в `nested` игнорируется.
- `--on-error fail|continue` и `--transaction all|per-file|none` (запрет `continue` + `all`).
- При `continue`: **statement-level** внутри файла (dollar-quoting учитывается); при ошибках — `applied_with_errors` в history.
- `--fail-on-script-errors` — ненулевой exit после прогона, если были script errors.
- `--verify-hash` / `--no-verify-hash` (default: проверка включена).
- `--store-sql true|false` (default `true`) — писать текст SQL в history или пустую строку.
- Env fallbacks: `POSTGRES_HOST`, `POSTGRES_PORT`, `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB`.
- Расширение `migration_history`: `status`, `applied_at`, `error_text` (+ безопасный upgrade, unique).
- В `nested`: WARNING, если сегмент БД в трёхсегментном имени ≠ имени папки.
- Dockerfile: `ARG RUNTIME_IMAGE` / `ARG SDK_IMAGE` для подстановки зеркала registry.
- Summary в stdout: `databases=N applied=A skipped=S errors=E`.

### Changed

- Default без новых флагов: `layout=nested`, `on-error=fail`, `transaction=all`, `verify-hash=true`, `store-sql=true` (как раньше, кроме unmatched).
