// App settings (SPEC §3-§5). Single JSON file at
// %LOCALAPPDATA%\TransPeek\settings.json, loaded once at startup and
// saved on change. Same setting names as the mac app where they overlap
// (primaryProviderId, fallbackProviderId, dictionaryLinks, ...), so the
// two platforms stay comparable.
//
// DEBT (v0.1, documented — see README): API keys live in this JSON file
// in plain text. The mac app stores them in the Keychain; the Windows
// equivalent is Credential Locker (PasswordVault). Moving keys to the
// vault is a post-v0.1 task; until then the file is per-user
// (%LOCALAPPDATA%) and the risk is stated in Settings UI + README.

using System.Text.Json;
using System.Text.Json.Serialization;
using TransPeek.Core.Dictionary;
using TransPeek.Core.Ocr;
using TransPeek.Core.Translation;

namespace TransPeek.Core.Settings;

public sealed class AppSettings
{
    // --- Translation ---
    public string SourceLanguage { get; set; } = LanguageCode.Auto;
    public string TargetLanguage { get; set; } = LanguageCode.Vietnamese;
    public string PrimaryProviderId { get; set; } = "google";
    public string FallbackProviderId { get; set; } = "google";
    /// <summary>Provider ids the user disabled. Everything not listed is enabled.</summary>
    public List<string> DisabledProviderIds { get; set; } = [];
    public List<CustomProviderDefinition> CustomProviders { get; set; } = [];

    // --- Trigger icon / float panel ---
    /// <summary>Hover delay before the panel opens, in milliseconds (300 or 500).</summary>
    public int HoverDelayMs { get; set; } = 300;
    /// <summary>Panel top-bar font size in points (the app name). The
    /// tagline (slogan) is always 1 pt smaller (Manh 2026-11-14 — the
    /// 11 pt tagline read too small next to the 13 pt name). Mac
    /// parity: same setting name (popupTopBarFontSize); mac default is
    /// 17, Windows default is 13 (the Windows panel is smaller), and
    /// mac uses a 2 pt gap while Windows uses 1 pt. Slider 12-24 in
    /// Settings -&gt; General.</summary>
    public double PopupTopBarFontSize { get; set; } = 13;
    /// <summary>How long the trigger icon stays after the selection changes, in seconds.</summary>
    public int TriggerIconAutoHideSeconds { get; set; } = 3;
    /// <summary>Global hotkey that shows the panel for the current selection.
    /// Win32 RegisterHotKey modifiers+key, e.g. "Control+Alt+T".</summary>
    public string TranslateHotkey { get; set; } = "Control+Alt+T";
    /// <summary>One global hotkey per tray menu action (Manh 2026-11-12:
    /// full shortcut set in Settings). Defaults use Control+Alt+letter —
    /// free on Windows and distinct from the two originals.</summary>
    public string ManualHotkey { get; set; } = "Control+Alt+M";
    public string ClipboardOcrHotkey { get; set; } = "Control+Alt+C";
    public string SelectionHotkey { get; set; } = "Control+Alt+S";
    public string ScreenshotCopyHotkey { get; set; } = "Control+Alt+P";
    public string ScanHotkey { get; set; } = "Control+Alt+B";
    public string SettingsHotkey { get; set; } = "Control+Alt+O";
    /// <summary>Watch the selection automatically (poll UIA). Off means the
    /// app only reacts to the hotkey / tray menu.</summary>
    public bool WatchSelection { get; set; } = true;

    // --- Dictionary bar ---
    public List<DictionaryLink> DictionaryLinks { get; set; } = DictionaryLink.FreshDefaults();
    /// <summary>"IconAndText" or "IconOnly".</summary>
    public string DictionaryDisplayMode { get; set; } = "IconAndText";

    // --- OCR ---
    // Fully qualified: the property name shadows the enum type inside this class.
    public string OcrProviderId { get; set; } = TransPeek.Core.Ocr.OcrProviderId.Tesseract.ToIdString();
    public List<string> OcrLanguages { get; set; } = ["vi-VN", "en-US"];
    /// <summary>"OpenPanel" (OCR -> translate -> panel, auto-copy translation)
    /// or "CopyOnly" (OCR -> copy source text, toast only).</summary>
    public string OcrAfterCapture { get; set; } = "OpenPanel";
    /// <summary>Auto-copy the translation to the clipboard after OCR (mode OpenPanel).</summary>
    public bool OcrAutoCopyTranslation { get; set; } = true;
    /// <summary>Global hotkey for OCR region capture. Default Ctrl+Shift+S.</summary>
    public string OcrHotkey { get; set; } = "Control+Shift+S";

    // --- General ---
    public bool LaunchAtLogin { get; set; } = false;
    public bool HasCompletedOnboarding { get; set; } = false;

    public bool IsProviderEnabled(string id)
        => !DisabledProviderIds.Contains(id, StringComparer.Ordinal);

    public void SetProviderEnabled(string id, bool enabled)
    {
        DisabledProviderIds.RemoveAll(x => string.Equals(x, id, StringComparison.Ordinal));
        if (!enabled)
            DisabledProviderIds.Add(id);
    }

    public static AppSettings Default() => new();

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static AppSettings FromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<AppSettings>(json) ?? Default();
        }
        catch
        {
            return Default();
        }
    }
}

/// <summary>Load/save of the settings file. All IO is best-effort: a missing
/// or corrupt file yields defaults, and a failed save is ignored (the app
/// keeps running with in-memory settings).</summary>
public static class SettingsStore
{
    public static AppSettings Load()
    {
        try
        {
            var path = Dictionary.AppPaths.SettingsFilePath;
            if (!File.Exists(path))
                return AppSettings.Default();
            var settings = AppSettings.FromJson(File.ReadAllText(path));
            // v0.1 saved the old dictionary defaults: migrate them once
            // in memory (saved back on the next settings save).
            DictionaryLink.MigrateDefaults(settings.DictionaryLinks);
            return settings;
        }
        catch
        {
            return AppSettings.Default();
        }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Dictionary.AppPaths.EnsureDirectories();
            var temp = Dictionary.AppPaths.SettingsFilePath + ".tmp";
            File.WriteAllText(temp, settings.ToJson());
            File.Move(temp, Dictionary.AppPaths.SettingsFilePath, overwrite: true);
        }
        catch
        {
            // Non-fatal: settings stay in memory for this run.
        }
    }
}
