using System.Collections.Concurrent;
using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Options;

namespace VrmImpl;

public sealed class ColorConsoleLoggerConfiguration
{
    public int EventId { get; set; }
}

public sealed class ColorConsoleLogger(
    string name,
    Func<ColorConsoleLoggerConfiguration> getCurrentConfig
) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => default!;

    public bool IsEnabled(LogLevel logLevel) => true;

    static (string, ConsoleColor) Label(LogLevel l)
    {
        switch (l)
        {
            case LogLevel.Trace:
                return ("Trace", ConsoleColor.DarkGray);
            case LogLevel.Debug:
                return ("Debug", ConsoleColor.DarkGray);
            case LogLevel.Information:
                return ("Info", ConsoleColor.Green);
            case LogLevel.Warning:
                return ("Warn", ConsoleColor.Yellow);
            case LogLevel.Error:
                return ("Error", ConsoleColor.Red);
            case LogLevel.Critical:
                return ("Critical", ConsoleColor.Magenta);
            case LogLevel.None:
                return ("None", ConsoleColor.DarkGray);

            default:
                throw new NotImplementedException();
        }
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        ColorConsoleLoggerConfiguration config = getCurrentConfig();
        if (config.EventId == 0 || config.EventId == eventId.Id)
        {
            var originalColor = (Console.ForegroundColor, Console.BackgroundColor);

            var (label, color) = Label(logLevel);
            Console.BackgroundColor = color;
            Console.ForegroundColor = ConsoleColor.Black;
            Console.Write($"{label}({name})");

            // (Console.ForegroundColor, Console.BackgroundColor) = originalColor;
            Console.ForegroundColor = color;
            Console.BackgroundColor = originalColor.BackgroundColor;

            Console.Write($": {formatter(state, exception)}");

            (Console.ForegroundColor, Console.BackgroundColor) = originalColor;
            Console.WriteLine();
        }
    }
}

[UnsupportedOSPlatform("browser")]
[ProviderAlias("ColorConsole")]
public sealed class ColorConsoleLoggerProvider : ILoggerProvider
{
    private readonly IDisposable? _onChangeToken;
    private ColorConsoleLoggerConfiguration _currentConfig;
    private readonly ConcurrentDictionary<string, ColorConsoleLogger> _loggers = new(
        StringComparer.OrdinalIgnoreCase
    );

    public ColorConsoleLoggerProvider(IOptionsMonitor<ColorConsoleLoggerConfiguration> config)
    {
        _currentConfig = config.CurrentValue;
        _onChangeToken = config.OnChange(updatedConfig => _currentConfig = updatedConfig);
    }

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, name => new ColorConsoleLogger(name, GetCurrentConfig));

    private ColorConsoleLoggerConfiguration GetCurrentConfig() => _currentConfig;

    public void Dispose()
    {
        _loggers.Clear();
        _onChangeToken?.Dispose();
    }
}

public static class ColorConsoleLoggerExtensions
{
    public static ILoggingBuilder AddColorConsoleLogger(this ILoggingBuilder builder)
    {
        builder.AddConfiguration();

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ILoggerProvider, ColorConsoleLoggerProvider>()
        );

        LoggerProviderOptions.RegisterProviderOptions<
            ColorConsoleLoggerConfiguration,
            ColorConsoleLoggerProvider
        >(builder.Services);

        return builder;
    }

    public static ILoggingBuilder AddColorConsoleLogger(
        this ILoggingBuilder builder,
        Action<ColorConsoleLoggerConfiguration> configure
    )
    {
        builder.AddColorConsoleLogger();
        builder.Services.Configure(configure);

        return builder;
    }
}
