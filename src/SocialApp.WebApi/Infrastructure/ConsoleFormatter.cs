using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace SocialApp.WebApi.Infrastructure;

public class SocialAppConsoleFormatter : ConsoleFormatter
{
    public SocialAppConsoleFormatter() : base("short") { }

    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter textWriter)
    {
        var logLevelShort = logEntry.LogLevel switch
        {
            LogLevel.Trace => "TRC",
            LogLevel.Debug => "DBG",
            LogLevel.Information => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            LogLevel.Critical => "CRT",
            _ => "UNK"
        };

        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        var pos = logEntry.Category.LastIndexOf('.') + 1;
        var category = logEntry.Category.Substring(pos);
        textWriter.Write($"[{timestamp}] [{logLevelShort}] {category}: ");
        var error = string.Empty;
        if (logEntry.Exception is not null)
        {
            error = $"\n{logEntry.Exception.GetType().Name}:{logEntry.Exception.Message}\n{logEntry.Exception.StackTrace}";
        }
        textWriter.WriteLine($"{logEntry.Formatter(logEntry.State, logEntry.Exception)}{error}");
    }
}