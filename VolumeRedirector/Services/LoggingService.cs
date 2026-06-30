using Microsoft.Extensions.Logging;
using System.IO;

namespace VolumeRedirector.Services;

internal sealed class SimpleConsoleLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new SimpleConsoleLogger(categoryName);

    public void Dispose()
    {
    }

    private sealed class SimpleConsoleLogger : ILogger
    {
        private readonly string _categoryName;

        public SimpleConsoleLogger(string categoryName) => _categoryName = categoryName;

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Console.WriteLine($"[{DateTimeOffset.Now:HH:mm:ss}] [{logLevel}] {_categoryName}: {formatter(state, exception)}");
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();
            public void Dispose() { }
        }
    }
}

public sealed class LoggingService : IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<LoggingService> _logger;

    public LoggingService()
    {
        var logsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VolumeRedirector", "Logs");
        Directory.CreateDirectory(logsDirectory);

        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddProvider(new SimpleConsoleLoggerProvider());
            builder.AddFile(Path.Combine(logsDirectory, "volume-redirector-{Date}.log"));
        });

        _logger = _loggerFactory.CreateLogger<LoggingService>();
    }

    public ILogger<T> CreateLogger<T>() => _loggerFactory.CreateLogger<T>();

    public void LogInformation(string message) => _logger.LogInformation(message);

    public void Dispose() => _loggerFactory.Dispose();
}
