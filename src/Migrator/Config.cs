namespace Migrator;

/// <summary>
/// Конфигурация мигратора из CLI и переменных окружения.
/// </summary>
public record Config
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; }
    public string User { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string MigrationDirectory { get; init; } = string.Empty;
    public LayoutMode Layout { get; init; } = LayoutMode.Nested;
    public string? Database { get; init; }
    public OnErrorMode OnError { get; init; } = OnErrorMode.Fail;
    public TransactionMode Transaction { get; init; } = TransactionMode.All;
    public bool IgnoreUnmatched { get; init; }
    public bool FailOnScriptErrors { get; init; }
    public bool VerifyHash { get; init; } = true;
    public bool StoreSql { get; init; } = true;

    public Config(string[] args)
    {
        if (args == null || args.Length == 0)
            throw new Exception("Не переданы аргументы для конфигурации мигратора");

        var map = ParseArgs(args);

        Host = GetRequired(map, "host")
               ?? Environment.GetEnvironmentVariable("POSTGRES_HOST")
               ?? GetPositional(args, 0)
               ?? throw new Exception("Не указан хост (--host или POSTGRES_HOST)");

        var portString = GetRequired(map, "port")
                         ?? Environment.GetEnvironmentVariable("POSTGRES_PORT")
                         ?? GetPositional(args, 1)
                         ?? throw new Exception("Не указан порт (--port или POSTGRES_PORT)");
        if (!int.TryParse(portString, out var parsedPort))
            throw new Exception($"Неверный порт: {portString}");
        Port = parsedPort;

        User = GetRequired(map, "user")
               ?? Environment.GetEnvironmentVariable("POSTGRES_USER")
               ?? GetPositional(args, 2)
               ?? throw new Exception("Не указан пользователь (--user или POSTGRES_USER)");

        Password = ResolvePassword(map, args);

        MigrationDirectory = GetRequired(map, "scripts") ?? GetPositional(args, 4)
                              ?? throw new Exception("Не указан путь к скриптам (--scripts)");

        if (!Directory.Exists(MigrationDirectory))
            throw new Exception($"Директория со скриптами не найдена: {MigrationDirectory}");

        Layout = ParseLayout(GetRequired(map, "layout"));
        Database = GetRequired(map, "database")
                   ?? Environment.GetEnvironmentVariable("POSTGRES_DB");

        OnError = ParseOnError(GetRequired(map, "on-error"));
        Transaction = ParseTransaction(GetRequired(map, "transaction"), OnError);

        IgnoreUnmatched = HasFlag(map, "ignore-unmatched");
        FailOnScriptErrors = HasFlag(map, "fail-on-script-errors");
        VerifyHash = ParseVerifyHash(map);
        StoreSql = ParseBoolOption(map, "store-sql", defaultValue: true);

        Validate();
    }

    /// <summary>
    /// Проверяет согласованность layout / on-error / transaction.
    /// </summary>
    private void Validate()
    {
        if (Layout == LayoutMode.Flat && string.IsNullOrWhiteSpace(Database))
            throw new Exception("Для --layout flat обязателен --database (или POSTGRES_DB)");

        if (OnError == OnErrorMode.Continue && Transaction == TransactionMode.All)
            throw new Exception(
                "Несовместимая конфигурация: --on-error continue нельзя сочетать с --transaction all. Используйте --transaction per-file или none.");
    }

    private static string ResolvePassword(IDictionary<string, string> map, string[] args)
    {
        var directPassword = map.TryGetValue("password", out var pwd) ? pwd : null;
        var passwordEnvName = map.TryGetValue("password-env", out var pwdEnv) ? pwdEnv : null;
        if (!string.IsNullOrEmpty(directPassword))
            return directPassword!;

        if (!string.IsNullOrEmpty(passwordEnvName))
        {
            return Environment.GetEnvironmentVariable(passwordEnvName!)
                   ?? throw new Exception($"Не удалось получить пароль из переменной окружения {passwordEnvName}");
        }

        var fromPostgresPassword = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");
        if (!string.IsNullOrEmpty(fromPostgresPassword))
            return fromPostgresPassword;

        var positionalPwdOrEnv = GetPositional(args, 3)
                                 ?? throw new Exception(
                                     "Не указан пароль (--password, --password-env или POSTGRES_PASSWORD)");
        var fromEnv = Environment.GetEnvironmentVariable(positionalPwdOrEnv);
        return fromEnv ?? positionalPwdOrEnv;
    }

    private static LayoutMode ParseLayout(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return LayoutMode.Nested;

        return value.Trim().ToLowerInvariant() switch
        {
            "nested" => LayoutMode.Nested,
            "flat" => LayoutMode.Flat,
            "by-filename" => LayoutMode.ByFilename,
            _ => throw new Exception($"Неизвестный --layout: {value}. Допустимо: nested, flat, by-filename")
        };
    }

    private static OnErrorMode ParseOnError(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return OnErrorMode.Fail;

        return value.Trim().ToLowerInvariant() switch
        {
            "fail" => OnErrorMode.Fail,
            "continue" => OnErrorMode.Continue,
            _ => throw new Exception($"Неизвестный --on-error: {value}. Допустимо: fail, continue")
        };
    }

    /// <summary>
    /// Парсит --transaction; если не задан — default по on-error.
    /// </summary>
    private static TransactionMode ParseTransaction(string? value, OnErrorMode onError)
    {
        if (string.IsNullOrWhiteSpace(value))
            return onError == OnErrorMode.Continue ? TransactionMode.PerFile : TransactionMode.All;

        return value.Trim().ToLowerInvariant() switch
        {
            "all" => TransactionMode.All,
            "per-file" => TransactionMode.PerFile,
            "none" => TransactionMode.None,
            _ => throw new Exception($"Неизвестный --transaction: {value}. Допустимо: all, per-file, none")
        };
    }

    /// <summary>
    /// Парсит --verify-hash / --no-verify-hash (default true).
    /// </summary>
    private static bool ParseVerifyHash(IDictionary<string, string> map)
    {
        if (map.ContainsKey("no-verify-hash"))
            return false;

        return ParseBoolOption(map, "verify-hash", defaultValue: true);
    }

    /// <summary>
    /// Парсит bool-опцию вида --key / --key true|false.
    /// </summary>
    private static bool ParseBoolOption(IDictionary<string, string> map, string key, bool defaultValue)
    {
        if (!map.TryGetValue(key, out var value))
            return defaultValue;

        if (string.IsNullOrWhiteSpace(value))
            return true;

        return value.Trim().ToLowerInvariant() switch
        {
            "true" or "1" or "yes" => true,
            "false" or "0" or "no" => false,
            _ => throw new Exception($"Неверное значение --{key}: {value}. Допустимо: true|false")
        };
    }

    /// <summary>
    /// Флаг считается заданным, если ключ присутствует (значение опционально).
    /// </summary>
    private static bool HasFlag(IDictionary<string, string> map, string key)
        => map.ContainsKey(key);

    private static Dictionary<string, string> ParseArgs(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            var token = args[i];
            if (!token.StartsWith("--"))
                continue;

            var key = token.TrimStart('-');
            var value = string.Empty;
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
            {
                value = args[i + 1];
                i++;
            }

            result[key] = value;
        }

        return result;
    }

    private static string? GetRequired(IDictionary<string, string> map, string key)
        => map.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static string? GetPositional(string[] args, int index)
        => index >= 0 && index < args.Length && !args[index].StartsWith("--")
            ? args[index]
            : null;
}
