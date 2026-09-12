using Microsoft.Extensions.Logging;

namespace VrmImpl;

internal class StaticLogger
{
    public static readonly ILoggerFactory Factory = LoggerFactory.Create(builder =>
    {
        builder.ClearProviders();
        builder.SetMinimumLevel(LogLevel.Debug);
        builder.AddColorConsoleLogger(configuration => { });
    });
}
