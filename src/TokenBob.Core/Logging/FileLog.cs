using System.Globalization;

namespace TokenBob.Core.Logging;

/// <summary>Writes one log file per day (tokenbob-yyyyMMdd.log) and deletes files older than 7 days.</summary>
public sealed class FileLog : ILog
{
    private const string Prefix = "tokenbob-";
    private static readonly TimeSpan KeepFor = TimeSpan.FromDays(7);

    private readonly string _dir;
    private readonly TimeProvider _time;
    private readonly object _gate = new();

    public FileLog(string logsDir, TimeProvider time)
    {
        _dir = logsDir;
        _time = time;
        try
        {
            Directory.CreateDirectory(_dir);
            Prune();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A logs folder that can't be created or read only means no log; the widget still runs.
        }
    }

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message, Exception? exception = null) =>
        Write("ERROR", exception is null ? message : $"{message} {exception}");

    private void Write(string level, string message)
    {
        var now = _time.GetLocalNow();
        var line = $"{now:yyyy-MM-dd HH:mm:ss} {level} {message}{Environment.NewLine}";
        try
        {
            lock (_gate) File.AppendAllText(Path.Combine(_dir, $"{Prefix}{now:yyyyMMdd}.log"), line);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Logging must never take the widget down (a locked or read-only logs folder included).
        }
    }

    private void Prune()
    {
        var cutoff = _time.GetLocalNow().Date - KeepFor;
        foreach (var file in Directory.EnumerateFiles(_dir, Prefix + "*.log"))
        {
            var stamp = Path.GetFileNameWithoutExtension(file)[Prefix.Length..];
            if (DateTime.TryParseExact(stamp, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
                && day < cutoff)
            {
                try { File.Delete(file); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        }
    }
}
