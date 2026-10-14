namespace TokenBob.Core.Logging;

/// <summary>Minimal logging seam. Callers must never pass tokens, cookies, or response bodies.</summary>
public interface ILog
{
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? exception = null);
}

public sealed class NullLog : ILog
{
    public static readonly NullLog Instance = new();

    public void Info(string message) { }
    public void Warn(string message) { }
    public void Error(string message, Exception? exception = null) { }
}
