// Alternative icon controller (mac FAST_READ_SELECT_ICON.md): owns the
// Read-selection monitor and the floating icon window. The translate
// action and the listen action are both App's job — App knows the
// shared click-to-listen flow (one voice, separate indicator), so the
// controller only reports hover/click via callbacks and never touches
// the panel, the reader, or any TTS service itself (mac 1cdaa44a: no
// synthesizer of its own anymore).

using Microsoft.UI.Dispatching;

namespace TransPeek.AlternativeIcon;

public sealed class AlternativeIconController : IDisposable
{
    private readonly Func<bool> _isReadSurfaceActive;
    private readonly Action<string, (int X, int Y)> _onTranslateRequested;
    private readonly Action<string, (int X, int Y)> _onSpeakRequested;
    private readonly DispatcherQueue _uiQueue;
    private readonly ReadSelectionMonitor _monitor;
    private readonly AlternativeIconWindow _icon;
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
    /// <param name="onSpeakRequested">Invoked on the UI thread when
    /// the user clicks the icon: App starts the shared click-to-listen
    /// session, with the indicator at the icon's position (passed
    /// along — read before the icon hides).</param>
    /// <param name="uiQueue">The UI thread's queue (captured in
    /// App.OnLaunched) — the monitor reports on a background thread,
    /// so every icon call marshals through it.</param>
    public AlternativeIconController(
        Func<bool> isReadSurfaceActive,
        Action<string, (int X, int Y)> onTranslateRequested,
        Action<string, (int X, int Y)> onSpeakRequested,
        DispatcherQueue uiQueue)
    {
        _isReadSurfaceActive = isReadSurfaceActive;
        _onTranslateRequested = onTranslateRequested;
        _onSpeakRequested = onSpeakRequested;
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

    /// <summary>The icon window's current position (physical pixels)
    /// — where the speaker indicator appears when the user clicks to
    /// listen. Read BEFORE hiding the icon (mac 1cdaa44a: hiding may
    /// clear the frame).</summary>
    public (int X, int Y) CurrentPosition
    {
        get
        {
            var pos = _icon.AppWindow.Position;
            return (pos.X, pos.Y);
        }
    }

    private void OnIconSpeakRequested()
    {
        // Click = listen through the SHARED click-to-listen flow (mac
        // 1cdaa44a): the icon only reports the click + its position;
        // App starts the session (pauses any read, stops any old
        // session, shows the indicator at this spot). Clicking again
        // starts a NEW session replacing the old one — pause/resume is
        // the indicator's job, never a toggle here.
        var text = _selectedText;
        if (string.IsNullOrWhiteSpace(text))
            return;
        // Read the position BEFORE hiding — the indicator appears at
        // the spot the user pressed (same order as mac).
        var pos = CurrentPosition;
        _icon.HideIcon();
        _onSpeakRequested(text, pos);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _monitor.Dispose();
        _icon.HideIcon();
    }
}
