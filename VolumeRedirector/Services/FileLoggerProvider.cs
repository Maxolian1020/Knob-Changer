using Microsoft.Extensions.Logging;
using System.IO;

namespace VolumeRedirector.Services;

internal sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _directory;

    public FileLoggerProvider(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(_directory, categoryName);

    public void Dispose()
    {
    }

    private sealed class FileLogger : ILogger
    {
        private readonly string _directory;
        private readonly string _categoryName;

        public FileLogger(string directory, string categoryName)
        {
            _directory = directory;
            _categoryName = categoryName;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var fileName = $"volume-redirector-{DateTime.Now:yyyy-MM-dd}.log";
            var filePath = Path.Combine(_directory, fileName);
            var message = formatter(state, exception);
            var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} [{logLevel}] {_categoryName}: {message}";
            if (exception is not null)
            {
                line += Environment.NewLine + exception;
            }

            File.AppendAllText(filePath, line + Environment.NewLine);
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
