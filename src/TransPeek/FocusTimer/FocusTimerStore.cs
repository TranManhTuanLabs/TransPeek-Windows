// Focus & Rest store (feature/focus-timer-decor, mac parity:
// FocusTimerStore.swift). The single store for the Focus & Rest card
// (panel left), the panel-right lite reader, the reminder banner and
// the Rest window. It owns the FocusTimerEngine (pure logic, in Core),
// drives it with a 1s DispatcherTimer, and routes reminders: a
// reminder that fires while the user is reading waits (the engine's
// PendingReminder) until the read stops, and only then shows — as a
// Rest window, or as a read in panel right, per the "Run reminder in"
// setting.
//
// Windows system notifications are NOT used yet (Manh 2026-11-20:
// hold until the app has an installer / Store identity — the banner
// + the orange dot + the auto-show on read end carry the reminder
// until then). The store has no notifier hook to fill in later — when
// notifications come, they attach to the engine's ReminderReady /
// PendingReminder like the banner does.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using TransPeek.Core.FocusTimer;
using TransPeek.Core.Settings;
using TransPeek.FastRead;

namespace TransPeek.FocusTimer;

/// <summary>A request to read a reminder in panel right: the text to
/// read and, when set, the seconds the reminder read runs before the
/// panel returns to the demo (a countdown read).</summary>
public sealed record FocusPanelRightRequest(string Text, int? CountdownSeconds);

public sealed class FocusTimerStore : INotifyPropertyChanged
{
    private readonly FocusTimerEngine _engine;
    private readonly DispatcherTimer _ticker = new() { Interval = TimeSpan.FromSeconds(1) };
    private RestWindow? _restWindow;
    private RsvpReaderViewModel? _panelReadingViewModel;
    private FocusPanelRightRequest? _panelRightRequest;
    private int? _panelReminderCountdown;
    private int? _panelReadCountdownSeconds;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The timer engine (Pomodoro + eye-rest). The card reads
    /// its state and subscribes to its PropertyChanged directly.</summary>
    public FocusTimerEngine Engine => _engine;

    /// <summary>Whether the Fast Read window is open (set by App).
    /// Together with a panel-right read, this is "reading" for the
    /// reminder rule.</summary>
    public Func<bool> IsFastReadWindowOpen { get; set; } = () => false;

    /// <summary>Bring the panel forward (set by App). Called when a
    /// reminder is routed to panel right.</summary>
    public Action? OnShowPanel { get; set; }

    /// <summary>Called before a Rest window shows (set by App to hide
    /// the panel — it is topmost and would otherwise float above the
    /// Rest window).</summary>
    public Action? OnShowRestWindow { get; set; }

    /// <summary>The view model of the read currently running in panel
    /// right, or null when panel right shows the demo. The panel
    /// observes this to freeze auto-fit while reading.</summary>
    public RsvpReaderViewModel? PanelReadingViewModel
    {
        get => _panelReadingViewModel;
        private set => SetField(ref _panelReadingViewModel, value);
    }

    /// <summary>A pending request to read in panel right. The panel
    /// consumes it (switches to the Fast Read tab, starts the read,
    /// pins itself) and clears it.</summary>
    public FocusPanelRightRequest? PanelRightRequest
    {
        get => _panelRightRequest;
        private set => SetField(ref _panelRightRequest, value);
    }

    /// <summary>Seconds left on the countdown shown after a reminder
    /// read finishes ("Rest — mm:ss" + Done). Null when no countdown
    /// is running.</summary>
    public int? PanelReminderCountdown
    {
        get => _panelReminderCountdown;
        private set => SetField(ref _panelReminderCountdown, value);
    }

    /// <summary>A reminder that fired while reading and is still
    /// waiting (the engine's). The banner + the orange dot show while
    /// this is set.</summary>
    public FocusReminder? PendingReminder => _engine.PendingReminder;

    public FocusTimerStore()
    {
        _engine = new FocusTimerEngine(
            App.Settings.FocusWorkMinutes,
            App.Settings.FocusRestMinutes,
            App.Settings.FocusLongBreakMinutes,
            App.Settings.FocusRepeatAutomatically);
        _engine.IsReading = () => PanelReadingViewModel is not null || IsFastReadWindowOpen();
        _engine.ReminderReady += ShowReminder;
        _ticker.Tick += (_, _) => Tick();
    }

    // ----- Pomodoro / eye-rest (the card calls these) -----

    public void TogglePomodoro()
    {
        _engine.TogglePomodoro();
        EnsureTicker();
    }

    public void ResetPomodoro()
    {
        _engine.ResetPomodoro();
        EnsureTicker();
    }

    public void SetWorkMinutes(int minutes)
    {
        _engine.SetWorkMinutes(minutes);
        App.Settings.FocusWorkMinutes = _engine.WorkMinutes;
        SettingsStore.Save(App.Settings);
    }

    public void SetRestMinutes(int minutes)
    {
        _engine.SetRestMinutes(minutes);
        App.Settings.FocusRestMinutes = _engine.RestMinutes;
        SettingsStore.Save(App.Settings);
    }

    public void SetRepeatAutomatically(bool repeat)
    {
        _engine.RepeatAutomatically = repeat;
        App.Settings.FocusRepeatAutomatically = repeat;
        SettingsStore.Save(App.Settings);
    }

    public void ToggleEyeRest()
    {
        _engine.ToggleEyeRest();
        EnsureTicker();
    }

    /// <summary>Where a reminder shows: "NewWindow" (a separate Rest
    /// window, default) or "RightPanel" (read in panel right). Only
    /// applies to reminders — not to Fast Read itself.</summary>
    public void SetReminderDestination(string destination)
    {
        App.Settings.FocusReminderDestination = destination;
        SettingsStore.Save(App.Settings);
    }

    /// <summary>Where Fast Read opens: "NewWindow" (a separate Fast
    /// Read window, default) or "RightPanel" (read in panel right).
    /// Applies to every Fast Read entry.</summary>
    public void SetRunLocation(string location)
    {
        App.Settings.FastReadRunLocation = location;
        SettingsStore.Save(App.Settings);
    }

    // ----- Reminders -----

    /// <summary>"Take a break" (banner): show the waiting reminder now.</summary>
    public void TakePendingBreakNow() => _engine.TakePendingBreakNow();

    /// <summary>"Snooze 5 min" (banner): the waiting reminder fires
    /// again in 5 minutes.</summary>
    public void SnoozePendingReminder()
    {
        _engine.SnoozePendingReminder();
        EnsureTicker();
    }

    /// <summary>Called when a read ends (Fast Read window closed /
    /// panel read stopped / panel hidden): a reminder that waited out
    /// the read shows now.</summary>
    public void FastReadWindowClosed() => _engine.ShowPendingReminder();

    private void ShowReminder(FocusReminder reminder)
    {
        if (string.Equals(App.Settings.FocusReminderDestination, "RightPanel", StringComparison.OrdinalIgnoreCase))
        {
            // Order matters: bring the panel forward FIRST, then set
            // the request — the consume handler switches to the Fast
            // Read tab, and ShowPanelNear always opens on the
            // Translate tab, so a request set before the show would be
            // switched away again.
            OnShowPanel?.Invoke();
            RequestPanelReading(reminder.Message, reminder.CountdownSeconds);
        }
        else
        {
            ShowRestWindow(reminder);
        }
    }

    private void ShowRestWindow(FocusReminder reminder)
    {
        // A new reminder replaces a previous Rest window (mac:
        // show closes the old window first). The panel hides first —
        // it is topmost and would float above the Rest window.
        OnShowRestWindow?.Invoke();
        _restWindow?.Close();
        _restWindow = new RestWindow();
        _restWindow.Closed += (_, _) => _restWindow = null;
        _restWindow.ShowRest(reminder.Message, reminder.CountdownSeconds, reminder.Title);
    }

    // ----- Panel-right reading -----

    /// <summary>Ask panel right to read a text: sets the request the
    /// panel consumes (switch tab, start the read, pin). Used by
    /// reminders and by the Fast Read routing.</summary>
    public void RequestPanelReading(string text, int? countdownSeconds)
    {
        PanelRightRequest = new FocusPanelRightRequest(text, countdownSeconds);
    }

    /// <summary>Start reading in panel right (called by the panel when
    /// it consumes PanelRightRequest, or by the demo's "Read in panel"
    /// button). A new read replaces a running one; a waiting reminder
    /// keeps waiting (it shows when this read stops).</summary>
    public void StartPanelReading(string text, int? countdownSeconds = null)
    {
        PanelRightRequest = null;
        PanelReminderCountdown = null;
        _panelReadCountdownSeconds = countdownSeconds;
        // A new read replaces a running one: stop the old view model
        // first, or its timer keeps advancing slides nobody renders.
        if (PanelReadingViewModel is { } old)
            old.SetPlaying(false);
        var viewModel = new RsvpReaderViewModel();
        viewModel.LoadText(text);
        PanelReadingViewModel = viewModel;
        viewModel.SetPlaying(true);
        EnsureTicker();
    }

    /// <summary>Stop the panel-right read and return to the demo. A
    /// waiting reminder shows (unless showPending is false — a new
    /// read replacing the old one keeps the reminder waiting).</summary>
    public void StopPanelReading(bool showPending = true)
    {
        if (PanelReadingViewModel is { } viewModel)
        {
            viewModel.SetPlaying(false);
            PanelReadingViewModel = null;
        }
        PanelRightRequest = null;
        PanelReminderCountdown = null;
        _panelReadCountdownSeconds = null;
        if (showPending)
            _engine.ShowPendingReminder();
        EnsureTicker();
    }

    /// <summary>Start the countdown after a reminder read finishes
    /// (the panel view calls this when the read is finished): the panel
    /// shows "Rest — mm:ss" + Done, and returns to the demo at zero.
    /// No-op for a manual read (no countdown).</summary>
    public void StartPanelReminderCountdown()
    {
        if (_panelReadCountdownSeconds is { } seconds)
        {
            PanelReminderCountdown = seconds;
            EnsureTicker();
        }
    }

    // ----- Ticker -----

    private void Tick()
    {
        _engine.Tick();
        if (PanelReminderCountdown is { } countdown)
        {
            countdown--;
            if (countdown <= 0)
            {
                PanelReminderCountdown = null;
                StopPanelReading();
            }
            else
            {
                PanelReminderCountdown = countdown;
            }
        }
        EnsureTicker();
    }

    /// <summary>The 1s ticker runs only while something counts down
    /// (mac: the Task stops when nothing runs).</summary>
    private void EnsureTicker()
    {
        var active = _engine.HasActiveCountdown || PanelReminderCountdown is not null;
        if (active && !_ticker.IsEnabled)
            _ticker.Start();
        else if (!active && _ticker.IsEnabled)
            _ticker.Stop();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
