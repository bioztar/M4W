using System.Text.Json;

namespace M4W.Core;

public sealed record AppSettings
{
    public const string DefaultHotkey = "Ctrl+Alt+V";
    public const int MinItems = 10;
    public const int MaxItemsCeiling = 100_000;
    public const int MinTypingIdleOffMinutes = 1;
    public const int MaxTypingIdleOffMinutes = 1440;
    public const int MinMinTypedChars = 0;
    public const int MaxMinTypedChars = 1000;
    public const int MinIdleFlushSeconds = 1;
    public const int MaxIdleFlushSeconds = 300;

    public string Hotkey { get; init; } = DefaultHotkey;
    public int MaxItems { get; init; } = 1000;
    public bool FuzzyByDefault { get; init; } = true;
    public bool AutoPaste { get; init; } = true;
    /// <summary>Skip copies flagged ExcludeClipboardContentFromMonitorProcessing. Off by default: M4W records everything.</summary>
    public bool RespectPrivateFlag { get; init; } = false;
    /// <summary>
    /// Whether typed text is captured into history. Always loads as false regardless of what is on
    /// disk — this is a manual, per-session toggle from the tray, never a setting that persists as on.
    /// </summary>
    public bool CaptureTyping { get; init; } = false;
    public int TypingIdleOffMinutes { get; init; } = 30;
    /// <summary>
    /// Off by default (Vitaly's call, 2026-10-29): he wants everything he types captured, password
    /// fields included — those are exactly what he forgets. Settable true if he ever wants the skip.
    /// </summary>
    public bool TypingSkipPasswordFields { get; init; } = false;
    /// <summary>A typed chunk shorter than this (e.g. a single keystroke) is discarded, not stored.</summary>
    public int MinTypedChars { get; init; } = 3;
    /// <summary>Seconds of no typing before the current buffer is flushed as its own entry.</summary>
    public int IdleFlushSeconds { get; init; } = 2;
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
                CaptureTyping = ReadCaptureTyping(root, log),
                TypingIdleOffMinutes = ReadInt(root, nameof(AppSettings.TypingIdleOffMinutes), d.TypingIdleOffMinutes, AppSettings.MinTypingIdleOffMinutes, AppSettings.MaxTypingIdleOffMinutes, log),
                TypingSkipPasswordFields = ReadBool(root, nameof(AppSettings.TypingSkipPasswordFields), d.TypingSkipPasswordFields, log),
                MinTypedChars = ReadInt(root, nameof(AppSettings.MinTypedChars), d.MinTypedChars, AppSettings.MinMinTypedChars, AppSettings.MaxMinTypedChars, log),
                IdleFlushSeconds = ReadInt(root, nameof(AppSettings.IdleFlushSeconds), d.IdleFlushSeconds, AppSettings.MinIdleFlushSeconds, AppSettings.MaxIdleFlushSeconds, log),
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

    /// <summary>Never returns true from a stored value: capture must start off on every launch. Logs only if the stored value was not even a bool.</summary>
    private static bool ReadCaptureTyping(JsonElement root, ILog log)
    {
        if (TryGet(root, nameof(AppSettings.CaptureTyping), out var v) && v.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            log.Warn("settings: invalid CaptureTyping (expected true/false), using default");
        return false;
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
