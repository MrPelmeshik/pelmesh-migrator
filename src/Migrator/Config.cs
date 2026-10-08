namespace Migrator;

public record Config
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; }
    public string User { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string MigrationDirectory { get; init; } = string.Empty;

    public Config(string[] args)
    {
        if (args == null || args.Length == 0)
            throw new Exception("Не переданы аргументы для конфигурации мигратора");

        var map = ParseArgs(args);

        Host = GetRequired(map, "host") ?? GetPositional(args, 0)
               ?? throw new Exception("Не указан хост (--host)");

        var portString = GetRequired(map, "port") ?? GetPositional(args, 1)
                         ?? throw new Exception("Не указан порт (--port)");
        if (!int.TryParse(portString, out var parsedPort))
            throw new Exception($"Неверный порт: {portString}");
        Port = parsedPort;

        User = GetRequired(map, "user") ?? GetPositional(args, 2)
               ?? throw new Exception("Не указан пользователь (--user)");

        var directPassword = map.TryGetValue("password", out var pwd) ? pwd : null;
        var passwordEnvName = map.TryGetValue("password-env", out var pwdEnv) ? pwdEnv : null;
        if (!string.IsNullOrEmpty(directPassword))
        {
            Password = directPassword!;
        }
        else if (!string.IsNullOrEmpty(passwordEnvName))
        {
            Password = Environment.GetEnvironmentVariable(passwordEnvName!)
                       ?? throw new Exception($"Не удалось получить пароль из переменной окружения {passwordEnvName}");
        }
        else
        {
            // positional 3 can be either a direct password or env var name
            var positionalPwdOrEnv = GetPositional(args, 3)
                                     ?? throw new Exception("Не указан пароль (--password) или имя переменной окружения (--password-env)");
            var fromEnv = Environment.GetEnvironmentVariable(positionalPwdOrEnv);
            Password = fromEnv ?? positionalPwdOrEnv; // prefer env var if exists, otherwise treat as direct value
        }

        MigrationDirectory = GetRequired(map, "scripts") ?? GetPositional(args, 4)
                              ?? throw new Exception("Не указан путь к скриптам (--scripts)");

        if (!Directory.Exists(MigrationDirectory))
            throw new Exception($"Директория со скриптами не найдена: {MigrationDirectory}");
    }

    private static Dictionary<string, string> ParseArgs(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            var token = args[i];
            if (token.StartsWith("--"))
            {
                var key = token.TrimStart('-');
                string value = string.Empty;
                if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                {
                    value = args[i + 1];
                    i++;
                }
                result[key] = value;
            }
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