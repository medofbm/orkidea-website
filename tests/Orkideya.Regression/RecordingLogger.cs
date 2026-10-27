using Microsoft.Extensions.Logging;

internal sealed class RecordingLogger : ILogger<Orkideya.Services.WhatsAppNotificationService>
{
    public int Count { get; private set; }
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter) => Count++;
}
