using Discord;
using Serilog;

namespace Lithium.Services
{
    public class Logger
    {
        public static void LogMessage(string message, LogSeverity severity = LogSeverity.Info)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();

            switch (severity)
            {
                case LogSeverity.Info:
                    Log.Information(message);
                    break;
                case LogSeverity.Warning:
                    Log.Warning(message);
                    break;
                case LogSeverity.Error:
                    Log.Error(message);
                    break;
                case LogSeverity.Debug:
                    Log.Debug(message);
                    break;
                case LogSeverity.Critical:
                    Log.Fatal(message);
                    break;
                case LogSeverity.Verbose:
                    Log.Verbose(message);
                    break;
                default:
                    Log.Information(message);
                    break;
            }
        }
    }
}