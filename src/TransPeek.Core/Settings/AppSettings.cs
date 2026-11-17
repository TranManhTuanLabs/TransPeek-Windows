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
    public int HoverDelayMs { get; set; } = 500;
    /// <summary>Panel content font size in points (the source and
    /// translation text). Mac parity: same setting name
    /// (popupFontSize); mac default is 14. Slider 12-24 in
    /// Settings -&gt; General -&gt; Appearance -&gt; Text Size (Manh
    /// 2026-11-15 — the main Text Size slider was missing).</summary>
    public double PopupFontSize { get; set; } = 14;
    /// <summary>Panel top-bar font size in points (the app name). The
    /// tagline (slogan) is always 1 pt smaller (Manh 2026-11-14 — the
    /// 11 pt tagline read too small next to the 13 pt name). Mac
    /// parity: same setting name (popupTopBarFontSize); mac default is
    /// 17, and Windows now also defaults to 17 (Manh 2026-11-15 —
    /// was 13, too small next to the 14 pt content), slogan 16; mac
    /// uses a 2 pt gap while Windows uses 1 pt. Slider 12-24 in
    /// Settings -&gt; General.</summary>
    public double PopupTopBarFontSize { get; set; } = 17;
    /// <summary>Settings window font size in points (the Settings UI
    /// text itself). Mac parity: same setting name (appTextSize).
    /// The main Settings text is this size; description/hint text is
    /// 1 pt smaller (Manh 2026-11-15 — "Setting Text Size (chỉnh cả
    /// font chính + mô tả (-1)"). Default 15 (main) / 14 (description)
    /// — Manh 2026-11-15, was 14/13. Slider 12-24 in Settings -&gt;
    /// General -&gt; Appearance -&gt; Setting Text Size. Note: a value
    /// already saved in settings.json wins over the default until the
    /// slider is moved once.</summary>
    public double AppTextSize { get; set; } = 15;
    /// <summary>Panel default width in logical pixels (DIPs).
    /// Shared panel design (2026-11-16): the panel opens at this
    /// DEFAULT size every show (420x450); a manual drag only enlarges
    /// it for the session and is NOT saved back here. Mac parity:
    /// same setting names (popupDefaultWidth / popupDefaultHeight).
    /// Auto-fit sets the height every show and snaps the width back
    /// to this default for short text (a wider panel gains nothing
    /// there); long text keeps a dragged width.</summary>
    public double PanelWidth { get; set; } = 420;
    /// <summary>Panel default height in logical pixels (DIPs).
    /// Shared panel design (2026-11-16): the panel opens at this
    /// DEFAULT size every show (420x450); a manual drag only enlarges
    /// it for the session and is NOT saved back here. Was 480; the
    /// shared design sets the default open height to 450.</summary>
    public double PanelHeight { get; set; } = 450;
    /// <summary>Settings schema version for one-time migrations
    /// (0 = never migrated). Version 1 (2026-11-16): reset the panel
    /// size to the shared default 440x450 — files written before this
    /// held a dragged size in PanelWidth/PanelHeight (manual resize
    /// used to save back), which would otherwise keep opening the
    /// panel at the old dragged size. Version 2 (2026-11-17): narrow
    /// the default width 440 to 420 (Manh: the horizontal snap-back
    /// was a touch wide) — files at version 1 get PanelWidth 420.</summary>
    public int SettingsVersion { get; set; } = 0;
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
    /// <summary>Global hotkey for OCR to Clipboard - Translated
    /// (OCR -&gt; translate -&gt; copy the translation). Default
    /// Control+Alt+R (tRanslated — T is taken by Translate).</summary>
    public string ClipboardOcrTranslatedHotkey { get; set; } = "Control+Alt+R";
    public string SelectionHotkey { get; set; } = "Control+Alt+S";
    /// <summary>Global hotkey for Translate Clipboard (translate the
    /// text currently in the clipboard). Default Control+Alt+L
    /// (cLipboard — C is taken by OCR to Clipboard).</summary>
    public string ClipboardTranslationHotkey { get; set; } = "Control+Alt+L";
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

    // --- Text-to-speech (Settings -> TTS Speech, Manh 2026-11-16) ---
    /// <summary>TTS engine id. Only "system" (Windows built-in voices)
    /// for now — the picker exists so a neural engine (Azure, Edge)
    /// can be added later without moving the setting.</summary>
    public string TtsService { get; set; } = "system";
    /// <summary>Preferred English accent (BCP 47, e.g. "en-US"). Used
    /// when no specific English voice is picked.</summary>
    public string EnglishAccent { get; set; } = "en-US";
    /// <summary>Id of the chosen English voice (VoiceInformation.Id),
    /// or null/empty for the system default English voice.</summary>
    public string? EnglishVoiceId { get; set; }
    /// <summary>Id of the chosen Vietnamese voice, or null/empty for
    /// the system default Vietnamese voice.</summary>
    public string? VietnameseVoiceId { get; set; }
    /// <summary>Default speech rate (0.5 - 2.0, 1.0 = normal). The
    /// SpeechSynthesizer rate.</summary>
    public double SpeechRate { get; set; } = 1.0;
    /// <summary>Read the source text aloud automatically after a
    /// translation finishes.</summary>
    public bool AutoPlaySource { get; set; } = false;
    /// <summary>Read the translation aloud automatically after a
    /// translation finishes.</summary>
    public bool AutoPlayTranslation { get; set; } = false;

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
            // Shared panel design (2026-11-16): the panel opens at the
            // DEFAULT size (420x450) every show; a manual drag is no
            // longer saved to Settings. Files written before this held
            // a dragged size in PanelWidth/PanelHeight — reset it to
            // the default once so old installs open compact again.
            if (settings.SettingsVersion < 1)
            {
                settings.PanelWidth = 440;
                settings.PanelHeight = 450;
                settings.SettingsVersion = 1;
            }
            // Version 2 (2026-11-17): narrow the default width 440 to
            // 420 (Manh: the horizontal snap-back was a touch wide).
            // Files already at version 1 pick this up once; the height
            // stays 450.
            if (settings.SettingsVersion < 2)
            {
                settings.PanelWidth = 420;
                settings.SettingsVersion = 2;
            }
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
