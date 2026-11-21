// Alternative icon controller (mac FAST_READ_SELECT_ICON.md): owns the
// Read-selection monitor, the floating icon window, and the icon's OWN
// system-TTS path. The translate action is App's job (it knows whether
// the selection came from the Full window or the panel's Lite tab), so
// the controller only reports it via a callback — it never touches the
// panel or the reader itself.
//
// The icon's TTS path is SEPARATE from the Read speaker button's: the
// reader's speaker drives RSVP playback sync (TTS advances the chunks),
// while this one is a quick independent "speak this selected text"
// that does not touch playback state (mac §6).

using Microsoft.UI.Dispatching;
using TransPeek.Core.Translation;
using TransPeek.Services;

namespace TransPeek.AlternativeIcon;

public sealed class AlternativeIconController : IDisposable
{
    private readonly Func<bool> _isReadSurfaceActive;
    private readonly Action<string, (int X, int Y)> _onTranslateRequested;
    private readonly DispatcherQueue _uiQueue;
    private readonly ReadSelectionMonitor _monitor;
    private readonly AlternativeIconWindow _icon;
    private readonly SystemSpeechService _speech = new();
    private string _selectedText = "";
    private (int X, int Y) _selectedPoint;
    private bool _disposed;

    /// <param name="isReadSurfaceActive">True while a Read surface is
    /// the foreground window. Evaluated on the monitor's background
    /// thread — keep it to thread-safe reads (see
    /// ReadSelectionMonitor).</param>
    /// <param name="onTranslateRequested">Invoked on the UI thread
    /// when the user hovers the icon: App translates the selected text
    /// (Full window: open the panel at the selection; Lite: translate
    /// in place).</param>
    /// <param name="uiQueue">The UI thread's queue (captured in
    /// App.OnLaunched) — the monitor reports on a background thread,
    /// so every icon call marshals through it.</param>
    public AlternativeIconController(
        Func<bool> isReadSurfaceActive,
        Action<string, (int X, int Y)> onTranslateRequested,
        DispatcherQueue uiQueue)
    {
        _isReadSurfaceActive = isReadSurfaceActive;
        _onTranslateRequested = onTranslateRequested;
        _uiQueue = uiQueue;
        _monitor = new ReadSelectionMonitor(
            isReadSurfaceActive,
            OnSelectionChanged,
            OnSelectionCleared);
        // Window creation is UI-thread work: App builds this
        // controller in OnLaunched, on the UI thread.
        _icon = new AlternativeIconWindow();
        _icon.OpenRequested += OnIconOpenRequested;
        _icon.SpeakRequested += OnIconSpeakRequested;
        _icon.SpeakStopped += () => _speech.Stop();
        _speech.SpeechEnded += (_, _) =>
        {
            // Playback ended on its own: back to the A-in-bubble mark.
            // MediaEnded fires off the UI thread — marshal to it.
            _uiQueue.TryEnqueue(() => _icon.SetSpeaking(false));
        };
    }

    public void Start() => _monitor.Start();

    private void OnSelectionChanged(string text, (int X, int Y) point)
    {
        // The monitor runs on a background thread: marshal to the UI
        // queue (same rule as App.OnSelectionChanged).
        _uiQueue.TryEnqueue(() =>
        {
            _selectedText = text;
            _selectedPoint = point;
            _icon.ShowAt(point.X, point.Y);
        });
    }

    private void OnSelectionCleared()
    {
        _uiQueue.TryEnqueue(() =>
        {
            _selectedText = "";
            _icon.HideIcon();
        });
    }

    private void OnIconOpenRequested()
    {
        // Hover = translate (mac §4). Hide the icon first — the panel
        // opens (Full) or switches tab (Lite) — then report the
        // selection to App, which picks the flow.
        var text = _selectedText;
        var point = _selectedPoint;
        _icon.HideIcon();
        if (!string.IsNullOrWhiteSpace(text))
            _onTranslateRequested(text, point);
    }

    private async void OnIconSpeakRequested()
    {
        // Click = listen through the icon's OWN TTS path (mac §6): a
        // quick independent speak that never touches RSVP playback
        // state. Language: the source language setting when the user
        // picked one, else guess from the text — same rule as the main
        // icon's Speak path.
        var text = _selectedText;
        if (string.IsNullOrWhiteSpace(text))
            return;
        var lang = App.Settings.SourceLanguage == LanguageCode.Auto
            ? SystemSpeechService.DetectLanguage(text)
            : App.Settings.SourceLanguage;
        var ok = await _speech.SpeakAsync(text, lang);
        if (!ok)
        {
            // Nothing to speak (no voice installed, synthesis failed):
            // back to the A-in-bubble mark.
            _uiQueue.TryEnqueue(() => _icon.SetSpeaking(false));
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _monitor.Dispose();
        _speech.Dispose();
        _icon.HideIcon();
    }
}
