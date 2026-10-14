using TokenBob.Core.Logging;

namespace TokenBob.Core.Tests.TestDoubles;

public sealed class RecordingLog : ILog
{
    public List<string> Messages { get; } = new();

    public string All => string.Join("\n", Messages);

    public void Info(string message) => Messages.Add("INFO " + message);
    public void Warn(string message) => Messages.Add("WARN " + message);
    public void Error(string message, Exception? exception = null) =>
        Messages.Add("ERROR " + message + (exception is null ? "" : " " + exception));
}
