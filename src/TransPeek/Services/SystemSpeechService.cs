// Windows system TTS (Windows.Media.SpeechSynthesis) — the built-in
// voices, offline, no key, no account. Speaks the panel text (source
// or translation) with the voice matching the text language; falls
// back to the default voice when no voice for that language is
// installed. Play/pause/resume (Manh 2026-11-26, Speechify /
// NaturalReader behaviour): one speaker press toggles play/pause,
// resume picks up where playback stopped, and only the end of the
// unit stops it on its own. Pause keeps the MediaPlayer stream
// alive, so Resume plays on from the same position — no
// re-synthesis, no word-boundary callback needed (WinRT has none).
//
// This is the 0.1.4 TTS (Manh 2026-11-16): system voices only. The
// neural voices (Azure, Edge Read Aloud) are a later opt-in — Azure
// needs a bring-your-own key, Edge has no public API — so they are
// NOT the default. Voice choice in Settings (a ComboBox over
// AllVoices) is the next step after this works.

using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;

namespace TransPeek.Services;

public sealed class SystemSpeechService : IDisposable
{
    private readonly SpeechSynthesizer _synth = new();
    private readonly MediaPlayer _player = new();
    private SpeechSynthesisStream? _currentStream;
    private bool _speaking;
    private bool _paused;
    private bool _disposed;

    /// <summary>True while a playback session is active — playing or
    /// paused. Cleared on Stop or when the stream ends on its own.</summary>
    public bool IsSpeaking => _speaking;

    /// <summary>True while the session is paused (audio halted, the
    /// stream and its position kept, so Resume plays on from the
    /// same spot). Only true while IsSpeaking is also true.</summary>
    public bool IsPaused => _paused;

    /// <summary>True while audio is actually playing (a session is
    /// active and not paused).</summary>
    public bool IsPlaying => _speaking && !_paused;

    /// <summary>Raised when playback ends on its own (not via Stop),
    /// so the UI can reset the speaker state.</summary>
    public event EventHandler? SpeechEnded;

    public SystemSpeechService()
    {
        _player.MediaEnded += (_, _) =>
        {
            _speaking = false;
            _paused = false;
            SpeechEnded?.Invoke(this, EventArgs.Empty);
        };
        _player.MediaFailed += (_, _) =>
        {
            _speaking = false;
            _paused = false;
        };
    }

    /// <summary>All voices installed on this machine (for the Settings
    /// voice picker). Empty when no text-to-speech voice is installed.</summary>
    public static IReadOnlyList<VoiceInformation> InstalledVoices =>
        SpeechSynthesizer.AllVoices;

    /// <summary>Guess the language of a text when the source language
    /// is "auto" and no provider has detected it yet (the float icon
    /// speaks the selection without translating first). Vietnamese has
    /// distinctive letters (ă â ê ô ơ ư đ + tone-marked vowels) that
    /// no other language in the app's set uses — if any appear, it is
    /// Vietnamese; otherwise assume English (the app's other main
    /// language). A heuristic, not a detector: it only separates
    /// Vietnamese from English, which is the pair the float icon
    /// mostly hears.</summary>
    public static string DetectLanguage(string text)
    {
        foreach (var ch in text)
        {
            if ("ăâêôơưđĂÂÊÔƠƯĐàáảãạèéẻẽẹìíỉĩịòóỏõọùúủũụỳýỷỹỵÀÁẢÃẠÈÉẺẼẸÌÍỈĨỊÒÓỎÕỌÙÚỦŨỤỲÝỶỸỴ".Contains(ch))
                return "vi";
        }
        return "en";
    }

    /// <summary>Speak <paramref name="text"/> in the voice matching
    /// <paramref name="languageCode"/> (an app code like "en", "vi",
    /// "zh-Hans", or "auto" for the default voice). Returns false
    /// when there is nothing to speak, no voice is installed, or
    /// synthesis failed. A call while a session is active stops that
    /// session first, so a new play always starts from the top.</summary>
    public async Task<bool> SpeakAsync(string text, string languageCode)
    {
        if (_disposed || string.IsNullOrWhiteSpace(text))
            return false;
        if (InstalledVoices.Count == 0)
            return false;

        Stop();
        try
        {
            // Rate from Settings (the TTS Speech tab, Default Speech
            // Rate, and the voice panel's Speed slider — same key).
            // SpeechSynthesizer takes 0.5 - 6.0; the setting is
            // clamped to the 0.5 - 4.0 the UI offers (the voice panel
            // offers 0.6x-4x, Settings → TTS 0.5x-4x; 4x is the
            // mac-parity cap).
            _synth.Options.SpeakingRate = Math.Clamp(App.Settings.SpeechRate, 0.5, 4.0);
            // Always set the voice explicitly (matched voice, else
            // the system default) — otherwise the voice from a
            // previous SpeakAsync would stick to the next language.
            var voice = PickVoice(languageCode) ?? SpeechSynthesizer.DefaultVoice;
            if (voice is not null)
                _synth.Voice = voice;

            var stream = await _synth.SynthesizeTextToStreamAsync(text);
            _currentStream = stream;
            _player.Source = MediaSource.CreateFromStream(stream, stream.ContentType);
            _speaking = true;
            _paused = false;
            _player.Play();
            return true;
        }
        catch
        {
            _speaking = false;
            _paused = false;
            return false;
        }
    }

    /// <summary>Pause playback (a no-op unless a session is playing).
    /// Only halts the MediaPlayer — the stream and its position stay,
    /// so Resume plays on from the same spot. The session stays
    /// active (IsSpeaking stays true) until Stop or the stream ends.</summary>
    public void Pause()
    {
        if (!_speaking || _paused)
            return;
        _paused = true;
        _player.Pause();
    }

    /// <summary>Resume a paused session from where it stopped (a
    /// no-op unless paused). Plays the same stream on — no
    /// re-synthesis, so the voice keeps the same position.</summary>
    public void Resume()
    {
        if (!_speaking || !_paused)
            return;
        _paused = false;
        _player.Play();
    }

    /// <summary>Stop playback (a no-op when no session is active).
    /// The current stream is released, so a later SpeakAsync starts
    /// fresh from the top.</summary>
    public void Stop()
    {
        if (!_speaking && _currentStream is null)
            return;
        _speaking = false;
        _paused = false;
        _player.Pause();
        _player.Source = null;
        _currentStream?.Dispose();
        _currentStream = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Stop();
        _player.Dispose();
        _synth.Dispose();
    }

    /// <summary>Best voice for an app language code: the voice the user
    /// picked in Settings -> TTS Speech -> Voices (by Id) when one is
    /// set for this language, then an exact BCP 47 match ("vi" →
    /// "vi-VN"), then a language-prefix match ("en" → "en-US"), else
    /// null = the system default voice. "auto" (source language
    /// unknown) always takes the default voice. A picked voice that is
    /// no longer installed falls through to the match, then the
    /// default — the Settings note says so.</summary>
    private static VoiceInformation? PickVoice(string languageCode)
    {
        var voices = SpeechSynthesizer.AllVoices;
        if (voices.Count == 0)
            return null;
        if (string.IsNullOrWhiteSpace(languageCode)
            || string.Equals(languageCode, "auto", StringComparison.OrdinalIgnoreCase))
            return null;

        // The user's picked voice for this language (TTS Speech tab).
        var preferredId = PreferredVoiceId(languageCode);
        if (!string.IsNullOrWhiteSpace(preferredId))
        {
            foreach (var voice in voices)
            {
                if (string.Equals(voice.Id, preferredId, StringComparison.Ordinal))
                    return voice;
            }
        }

        var target = MapToBcp47(languageCode);
        // English accent (TTS Speech tab, English Accent): when no
        // specific English voice is picked, prefer a voice in the
        // chosen accent (e.g. "en-GB" over "en-US").
        if (target.Split('-')[0].Equals("en", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(App.Settings.EnglishAccent))
        {
            target = App.Settings.EnglishAccent;
        }
        foreach (var voice in voices)
        {
            if (string.Equals(voice.Language, target, StringComparison.OrdinalIgnoreCase))
                return voice;
        }
        var prefix = target.Split('-')[0];
        foreach (var voice in voices)
        {
            if (voice.Language.StartsWith(prefix + "-", StringComparison.OrdinalIgnoreCase))
                return voice;
        }
        return null;
    }

    /// <summary>The voice id the user picked for a language code, or
    /// null for the system default. English uses EnglishVoiceId; for
    /// English the accent setting (EnglishAccent) is the fallback
    /// target — handled by the exact-match step above, since the
    /// accent is itself a BCP 47 tag.</summary>
    private static string? PreferredVoiceId(string languageCode)
    {
        var prefix = MapToBcp47(languageCode).Split('-')[0].ToLowerInvariant();
        return prefix switch
        {
            "en" => App.Settings.EnglishVoiceId,
            "vi" => App.Settings.VietnameseVoiceId,
            _ => null,
        };
    }

    /// <summary>App code → BCP 47, same mapping the translation
    /// providers use ("zh-Hans" → "zh-CN", "zh-Hant" → "zh-TW").</summary>
    private static string MapToBcp47(string code) => code switch
    {
        "zh-Hans" => "zh-CN",
        "zh-Hant" => "zh-TW",
        _ => code,
    };
}
