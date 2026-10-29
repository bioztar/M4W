using System.Text.Json;

namespace M4W.Core;

public sealed record AppSettings
{
    public const string DefaultHotkey = "Ctrl+Alt+V";
    public const int MinItems = 10;
    public const int MaxItemsCeiling = 100_000;

    public string Hotkey { get; init; } = DefaultHotkey;
    public int MaxItems { get; init; } = 1000;
    public bool FuzzyByDefault { get; init; } = true;
    public bool AutoPaste { get; init; } = true;
    /// <summary>Skip copies flagged ExcludeClipboardContentFromMonitorProcessing. Off by default: M4W records everything.</summary>
    public bool RespectPrivateFlag { get; init; } = false;
}

public static class SettingsLoader
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <summary>Loads settings; every bad or missing value falls back to its default and is logged. Never throws.</summary>
    public static AppSettings Load(string path, ILog log)
    {
        string json;
        try
        {
            if (!File.Exists(path)) return new AppSettings();
            json = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.Error("settings: cannot read file, using defaults", ex);
            return new AppSettings();
        }
        return Parse(json, log);
    }

    public static AppSettings Parse(string json, ILog log)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        }
        catch (JsonException ex)
        {
            log.Error("settings: invalid JSON, using defaults", ex);
            return new AppSettings();
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                log.Warn("settings: root is not an object, using defaults");
                return new AppSettings();
            }

            var d = new AppSettings();
            var root = doc.RootElement;
            return new AppSettings
            {
                Hotkey = ReadHotkey(root, d.Hotkey, log),
                MaxItems = ReadInt(root, nameof(AppSettings.MaxItems), d.MaxItems, AppSettings.MinItems, AppSettings.MaxItemsCeiling, log),
                FuzzyByDefault = ReadBool(root, nameof(AppSettings.FuzzyByDefault), d.FuzzyByDefault, log),
                AutoPaste = ReadBool(root, nameof(AppSettings.AutoPaste), d.AutoPaste, log),
                RespectPrivateFlag = ReadBool(root, nameof(AppSettings.RespectPrivateFlag), d.RespectPrivateFlag, log),
            };
        }
    }

    public static void Save(string path, AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(settings, WriteOptions));
    }

    private static bool TryGet(JsonElement root, string name, out JsonElement value)
    {
        foreach (var p in root.EnumerateObject())
        {
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) { value = p.Value; return true; }
        }
        value = default;
        return false;
    }

    private static string ReadHotkey(JsonElement root, string fallback, ILog log)
    {
        if (!TryGet(root, nameof(AppSettings.Hotkey), out var v)) return fallback;
        if (v.ValueKind == JsonValueKind.String && M4W.Core.Hotkey.TryParse(v.GetString(), out _)) return v.GetString()!;
        log.Warn("settings: invalid Hotkey, using default");
        return fallback;
    }

    private static int ReadInt(JsonElement root, string name, int fallback, int min, int max, ILog log)
    {
        if (!TryGet(root, name, out var v)) return fallback;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) && n >= min && n <= max) return n;
        log.Warn($"settings: invalid {name} (expected integer {min}-{max}), using default");
        return fallback;
    }

    private static bool ReadBool(JsonElement root, string name, bool fallback, ILog log)
    {
        if (!TryGet(root, name, out var v)) return fallback;
        if (v.ValueKind is JsonValueKind.True or JsonValueKind.False) return v.GetBoolean();
        log.Warn($"settings: invalid {name} (expected true/false), using default");
        return fallback;
    }
}
