using Microsoft.Extensions.Logging;
using MikroTek.Services.ENUM;

namespace MikroTek.Services.Utils
{
    /// <summary>
    /// Our own logger. Writes through ASP.NET Core's logging, so entries show in the
    /// server window / Visual Studio Output and follow the "Logging" levels in appsettings.
    /// SQL statements are logged at Debug, so they only show while developing.
    /// </summary>
    public class Logger
    {
        private readonly ILogger _log;

        public Logger(ILoggerFactory factory)
        {
            _log = factory.CreateLogger("MikroTek.Services");
        }

        public void WriteLog(Level level, LogType type, string message, Exception? ex = null)
        {
            _log.Log(ToLogLevel(level), ex, "[{LogType}] {Message}", type, message);
        }

        private static LogLevel ToLogLevel(Level level) => level switch
        {
            Level.Debug => LogLevel.Debug,
            Level.Info => LogLevel.Information,
            Level.Warning => LogLevel.Warning,
            _ => LogLevel.Error
        };
    }
}
