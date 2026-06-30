using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO;

namespace VolumeRedirector.Services;

public static class LoggingExtensions
{
    public static ILoggingBuilder AddFile(this ILoggingBuilder builder, string logPath)
    {
        var directory = Path.GetDirectoryName(logPath)!;
        builder.Services.AddSingleton<ILoggerProvider>(_ => new FileLoggerProvider(directory));
        return builder;
    }
}
