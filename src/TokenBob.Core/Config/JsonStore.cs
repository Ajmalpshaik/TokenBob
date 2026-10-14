using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using TokenBob.Core.Logging;

namespace TokenBob.Core.Config;

/// <summary>
/// Loads and saves one object as a JSON file. Never throws: a missing, locked or broken file
/// gives defaults, and a single bad value (say, a style from a newer version) only resets
/// that one setting.
/// </summary>
public sealed class JsonStore<T> where T : class, new()
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly PropertyInfo[] Properties =
        typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite).ToArray();

    private readonly string _path;
    private readonly ILog _log;

    public JsonStore(string path, ILog log)
    {
        _path = path;
        _log = log;
    }

    private string Name => Path.GetFileName(_path);

    public T Load()
    {
        string text;
        try
        {
            if (!File.Exists(_path)) return new T();
            text = File.ReadAllText(_path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Warn($"{Name} couldn't be read ({ex.GetType().Name}); using defaults");
            return new T();
        }

        JsonObject? root;
        try
        {
            root = JsonNode.Parse(text, new JsonNodeOptions { PropertyNameCaseInsensitive = true }) as JsonObject;
        }
        catch (JsonException)
        {
            root = null;
        }

        if (root is null)
        {
            _log.Warn($"{Name} was unreadable; using defaults and keeping a .bak copy");
            TryBackup();
            return new T();
        }

        var result = new T();
        foreach (var property in Properties)
        {
            // Missing or null → keep the default.
            if (!root.TryGetPropertyValue(property.Name, out var node) || node is null) continue;
            try
            {
                if (node.Deserialize(property.PropertyType, Options) is { } value) property.SetValue(result, value);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException or FormatException)
            {
                _log.Warn($"{Name}: ignored an unreadable value for {property.Name}");
            }
        }
        return result;
    }

    /// <summary>Writes the file atomically. Returns false (and logs) if it couldn't be saved.</summary>
    public bool Save(T value)
    {
        var temp = _path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
            File.WriteAllText(temp, JsonSerializer.Serialize(value, Options));
            File.Move(temp, _path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Warn($"{Name} couldn't be saved ({ex.GetType().Name})");
            try { File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            return false;
        }
    }

    private void TryBackup()
    {
        try
        {
            File.Copy(_path, _path + ".bak", overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Warn($"{Name}: couldn't keep a .bak copy ({ex.GetType().Name})");
        }
    }
}
