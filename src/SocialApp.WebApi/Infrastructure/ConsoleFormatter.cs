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
        Console.ForegroundColor = ConsoleColor.Green;
        textWriter.WriteLine($"{logEntry.Formatter(logEntry.State, logEntry.Exception)}");
        Console.ResetColor();
    }
}