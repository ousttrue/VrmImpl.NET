using Microsoft.Extensions.Logging;

namespace VrmImpl.VorticeVulkan;

public partial class VulkanLogger
{
    static ILogger? _logger;

    public static void Inject(ILoggerFactory factory)
    {
        _logger = factory.CreateLogger("Vulkan");
    }

    public static void Trace(string msg)
    {
        if (_logger is ILogger logger)
        {
            logger.LogTrace(msg);
        }
        else
        {
            Console.WriteLine($"VulkanLogger.Trace: {msg}");
        }
    }

    public static void Debug(string msg)
    {
        if (_logger is ILogger logger)
        {
            logger.LogDebug(msg);
        }
        else
        {
            Console.WriteLine($"VulkanLogger.Debug: {msg}");
        }
    }

    public static void Info(string msg)
    {
        if (_logger is ILogger logger)
        {
            logger.LogInformation(msg);
        }
        else
        {
            Console.WriteLine($"VulkanLogger.Info: {msg}");
        }
    }

    public static void Warn(string msg)
    {
        if (_logger is ILogger logger)
        {
            logger.LogWarning(msg);
        }
        else
        {
            Console.WriteLine($"VulkanLogger.Warn: {msg}");
        }
    }

    public static void Error(string msg)
    {
        if (_logger is ILogger logger)
        {
            logger.LogError(msg);
        }
        else
        {
            Console.WriteLine($"VulkanLogger.Error: {msg}");
        }
    }
}
