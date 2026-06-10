using Serilog;
using Serilog.Core;

namespace CleanWipe.Core.Services;

/// <summary>
/// Logging estructurado mediante Serilog. Cada operación de la app se registra en
/// %APPDATA%\CleanWipe\logs\cleanwipe-.log (rotación diaria). El log lo gestiona esta
/// capa, no las vistas, manteniendo separación de responsabilidades.
/// </summary>
public static class AppLogger
{
    private static Logger? _logger;
    private static readonly object Gate = new();

    public static string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CleanWipe", "logs");

    public static ILogger Instance
    {
        get
        {
            if (_logger != null) return _logger;
            lock (Gate)
            {
                if (_logger != null) return _logger;
                Directory.CreateDirectory(LogDirectory);
                _logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.File(
                        Path.Combine(LogDirectory, "cleanwipe-.log"),
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 31,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                    .CreateLogger();
                return _logger;
            }
        }
    }

    public static void Info(string message) => Instance.Information(message);
    public static void Warn(string message) => Instance.Warning(message);
    public static void Error(string message, Exception? ex = null) => Instance.Error(ex, message);

    public static void CloseAndFlush()
    {
        lock (Gate)
        {
            _logger?.Dispose();
            _logger = null;
        }
    }
}
