using System.Text.Json;
using System.Text.Json.Serialization;
using TokenBob.Core.Logging;

namespace TokenBob.Core.Config;

/// <summary>Loads and saves one object as a JSON file. Never throws on a bad or missing file.</summary>
public sealed class JsonStore<T> where T : class, new()
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;
    private readonly ILog _log;

    public JsonStore(string path, ILog log)
    {
        _path = path;
        _log = log;
    }

    public T Load()
    {
        if (!File.Exists(_path)) return new T();
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(_path), Options) ?? new T();
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            _log.Warn($"{Path.GetFileName(_path)} was unreadable; using defaults and keeping a .bak copy");
            File.Copy(_path, _path + ".bak", overwrite: true);
            return new T();
        }
    }

    public void Save(T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value, Options));
        File.Move(temp, _path, overwrite: true);
    }
}
