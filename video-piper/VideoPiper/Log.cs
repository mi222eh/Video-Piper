using Microsoft.Extensions.Logging;

namespace VideoPiper;

/// <summary>
/// Ambient logging facade for the whole app. Categories resolve from the factory wired
/// up by <see cref="App.InitializeLogging"/> (debug builds only). Without a factory —
/// release builds and unit tests — every call is a no-op <see cref="NullLogger"/>, so
/// logging adds no startup cost or output outside debug.
/// </summary>
public static class Log
{
    private static ILoggerFactory? _factory;

    /// <summary>Wires the ambient logging factory (called once from <see cref="App.InitializeLogging"/>).</summary>
    internal static void Initialize(ILoggerFactory factory) => _factory = factory;

    /// <summary>Returns the logger for the given category type (works for static classes too).</summary>
    public static ILogger For(Type category) => _factory is { } f ? f.CreateLogger(category) : Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
}
