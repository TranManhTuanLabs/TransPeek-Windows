// Windows system TTS (Windows.Media.SpeechSynthesis) — the built-in
// voices, offline, no key, no account. Speaks the panel text (source
// or translation) with the voice matching the text language; falls
// back to the default voice when no voice for that language is
// installed. Speak again while speaking stops (toggle); playback
// ending on its own clears the speaking state.
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
    private bool _disposed;

    /// <summary>True while audio is playing (set on Play, cleared on
    /// Stop or when the stream ends).</summary>
    public bool IsSpeaking => _speaking;

    /// <summary>Raised when playback ends on its own (not via Stop),
    /// so the UI can reset the speaker state.</summary>
    public event EventHandler? SpeechEnded;

    public SystemSpeechService()
    {
        _player.MediaEnded += (_, _) =>
        {
            _speaking = false;
            SpeechEnded?.Invoke(this, EventArgs.Empty);
        };
        _player.MediaFailed += (_, _) => _speaking = false;
    }

    /// <summary>All voices installed on this machine (for the Settings
    /// voice picker). Empty when no text-to-speech voice is installed.</summary>
    public static IReadOnlyList<VoiceInformation> InstalledVoices =>
        SpeechSynthesizer.AllVoices;

    /// <summary>Speak <paramref name="text"/> in the voice matching
    /// <paramref name="languageCode"/> (an app code like "en", "vi",
    /// "zh-Hans", or "auto" for the default voice). Returns false
    /// when there is nothing to speak, no voice is installed, or
    /// synthesis failed. A call while speaking stops the current
    /// playback first.</summary>
    public async Task<bool> SpeakAsync(string text, string languageCode)
    {
        if (_disposed || string.IsNullOrWhiteSpace(text))
            return false;
        if (InstalledVoices.Count == 0)
            return false;

        Stop();
        try
        {
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
            _player.Play();
            return true;
        }
        catch
        {
            _speaking = false;
            return false;
        }
    }

    /// <summary>Stop playback (a no-op when not speaking). The current
    /// stream is released, so a later SpeakAsync starts fresh.</summary>
    public void Stop()
    {
        if (!_speaking && _currentStream is null)
            return;
        _speaking = false;
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

    /// <summary>Best voice for an app language code: exact BCP 47
    /// match ("vi" → "vi-VN"), then a language-prefix match ("en" →
    /// "en-US"), else null = the system default voice. "auto" (source
    /// language unknown) always takes the default voice.</summary>
    private static VoiceInformation? PickVoice(string languageCode)
    {
        var voices = SpeechSynthesizer.AllVoices;
        if (voices.Count == 0)
            return null;
        if (string.IsNullOrWhiteSpace(languageCode)
            || string.Equals(languageCode, "auto", StringComparison.OrdinalIgnoreCase))
            return null;

        var target = MapToBcp47(languageCode);
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

    /// <summary>App code → BCP 47, same mapping the translation
    /// providers use ("zh-Hans" → "zh-CN", "zh-Hant" → "zh-TW").</summary>
    private static string MapToBcp47(string code) => code switch
    {
        "zh-Hans" => "zh-CN",
        "zh-Hant" => "zh-TW",
        _ => code,
    };
}
