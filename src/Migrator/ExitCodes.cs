namespace Migrator;

/// <summary>
/// Вычисление кода выхода по итогам прогона.
/// </summary>
public static class ExitCodes
{
    /// <summary>
    /// 0 при успехе; 1 при ошибках fail / fail-on-script-errors.
    /// </summary>
    public static int Resolve(ApplySummary summary, OnErrorMode onError, bool failOnScriptErrors)
    {
        if (summary.Errors > 0 && (onError == OnErrorMode.Fail || failOnScriptErrors))
            return 1;

        return 0;
    }
}
