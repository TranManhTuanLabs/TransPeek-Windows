// Focus timer engine (feature/focus-timer-decor, mac parity:
// FocusTimerStore.swift). Pure logic, no UI types — the app-side
// FocusTimerStore drives it with a 1s DispatcherTimer and routes the
// reminders (Rest window vs panel right). Pomodoro (Work 25 / Rest 5,
// long break 15 after 4 focus cycles) and the 20-20-20 eye-rest run
// independently. A reminder that fires while the user is reading does
// NOT steal the screen: it waits (PendingReminder) until the read
// stops, and only then shows.

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TransPeek.Core.FocusTimer;

/// <summary>Pomodoro phase. Display name: Idle = "Ready".</summary>
public enum FocusPomodoroPhase
{
    Idle,
    Focus,
    Rest,
    LongBreak,
}

public enum FocusReminderKind
{
    WorkBreak,
    EyeRest,
}

/// <summary>A break reminder: what it is, how long the break lasts
/// (WorkBreak = the rest minutes that just started), and how long the
/// reminder text takes to read (CountdownSeconds). Text mirrors mac's
/// FocusReminderKind exactly.</summary>
public sealed record FocusReminder(FocusReminderKind Kind, int Minutes, int CountdownSeconds)
{
    public string Title => Kind == FocusReminderKind.WorkBreak ? "Take a break" : "Rest your eyes";

    public string Message => Kind == FocusReminderKind.WorkBreak
        ? $"You have read, worked, and sat for {Minutes} minutes continuously. Rest your eyes and brain. Stand up and walk around. Be more physically active."
        : "Rest your eyes. Look at something 20 feet away for 20 seconds.";

    public string NotificationTitle => Kind == FocusReminderKind.WorkBreak
        ? $"{Minutes} minutes of work done — time for a break"
        : "Time to rest your eyes (20-20-20)";
}

public sealed class FocusTimerEngine : INotifyPropertyChanged
{
    /// <summary>Eye-rest interval: remind every 20 minutes.</summary>
    public const int EyeRestIntervalSeconds = 20 * 60;

    /// <summary>Eye-rest read length: the reminder text reads for 20 seconds.</summary>
    public const int EyeRestCountdownSeconds = 20;

    /// <summary>Snooze length: a snoozed reminder fires again after 5 minutes.</summary>
    public const int SnoozeSeconds = 5 * 60;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised when a reminder should be SHOWN now (not while
    /// reading): the store routes it to the Rest window or panel right.</summary>
    public event Action<FocusReminder>? ReminderReady;

    /// <summary>Whether the user is reading right now (a Fast Read
    /// window or a panel-right read). Set by the store: while reading,
    /// a reminder waits as PendingReminder instead of showing — unless
    /// WaitWhileReading says to run it now (see below).</summary>
    public Func<bool> IsReading { get; set; } = () => false;

    /// <summary>Whether a reminder that fires while reading should
    /// wait (default) or run now. Set by the store from the "When a
    /// reminder fires while reading" setting ("NotificationOnly" =
    /// wait, "RunNow" = run). Only consulted while reading — when not
    /// reading the reminder always shows at once.</summary>
    public Func<bool> WaitWhileReading { get; set; } = () => true;

    public int WorkMinutes { get; private set; }
    public int RestMinutes { get; private set; }
    public int LongBreakMinutes { get; private set; }
    public bool RepeatAutomatically { get; set; }

    private FocusPomodoroPhase _pomodoroPhase = FocusPomodoroPhase.Idle;
    public FocusPomodoroPhase PomodoroPhase
    {
        get => _pomodoroPhase;
        private set => SetField(ref _pomodoroPhase, value);
    }

    public string PomodoroPhaseName => PomodoroPhase switch
    {
        FocusPomodoroPhase.Focus => "Focus",
        FocusPomodoroPhase.Rest => "Rest",
        FocusPomodoroPhase.LongBreak => "Long break",
        _ => "Ready",
    };

    private bool _pomodoroRunning;
    public bool PomodoroRunning
    {
        get => _pomodoroRunning;
        private set => SetField(ref _pomodoroRunning, value);
    }

    private int _pomodoroRemainingSeconds;
    public int PomodoroRemainingSeconds
    {
        get => _pomodoroRemainingSeconds;
        private set => SetField(ref _pomodoroRemainingSeconds, value);
    }

    private int _completedFocusCycles;
    public int CompletedFocusCycles
    {
        get => _completedFocusCycles;
        private set => SetField(ref _completedFocusCycles, value);
    }

    private bool _eyeRestRunning;
    public bool EyeRestRunning
    {
        get => _eyeRestRunning;
        private set => SetField(ref _eyeRestRunning, value);
    }

    private int _eyeRestRemainingSeconds = EyeRestIntervalSeconds;
    public int EyeRestRemainingSeconds
    {
        get => _eyeRestRemainingSeconds;
        private set => SetField(ref _eyeRestRemainingSeconds, value);
    }

    private FocusReminder? _pendingReminder;
    /// <summary>A reminder that fired while reading and is still
    /// waiting. Null when nothing waits. The banner + orange dot show
    /// while this is set.</summary>
    public FocusReminder? PendingReminder
    {
        get => _pendingReminder;
        private set => SetField(ref _pendingReminder, value);
    }

    /// <summary>Seconds left on a snoozed reminder (0 = no snooze).</summary>
    public int SnoozeRemainingSeconds { get; private set; }

    /// <summary>True while any countdown is active (pomodoro running,
    /// eye-rest running, or a snooze waiting) — the store's ticker
    /// runs only then.</summary>
    public bool HasActiveCountdown
        => PomodoroRunning || EyeRestRunning || SnoozeRemainingSeconds > 0;

    public FocusTimerEngine(int workMinutes = 25, int restMinutes = 5, int longBreakMinutes = 15, bool repeatAutomatically = true)
    {
        WorkMinutes = Math.Clamp(workMinutes, 5, 60);
        RestMinutes = Math.Clamp(restMinutes, 5, 15);
        LongBreakMinutes = Math.Clamp(longBreakMinutes, 5, 60);
        RepeatAutomatically = repeatAutomatically;
        _pomodoroRemainingSeconds = WorkMinutes * 60;
    }

    /// <summary>Start / Pause / Resume the Pomodoro (the card's main
    /// button): idle starts a focus session, running pauses, paused
    /// resumes.</summary>
    public void TogglePomodoro()
    {
        if (PomodoroPhase == FocusPomodoroPhase.Idle)
        {
            PomodoroPhase = FocusPomodoroPhase.Focus;
            PomodoroRemainingSeconds = WorkMinutes * 60;
            PomodoroRunning = true;
        }
        else
        {
            PomodoroRunning = !PomodoroRunning;
        }
        OnPropertyChanged(nameof(PomodoroPhaseName));
    }

    /// <summary>Reset the Pomodoro to idle (cycles back to 0, remaining
    /// re-armed to the work length). A waiting reminder is untouched.</summary>
    public void ResetPomodoro()
    {
        PomodoroPhase = FocusPomodoroPhase.Idle;
        PomodoroRunning = false;
        PomodoroRemainingSeconds = WorkMinutes * 60;
        CompletedFocusCycles = 0;
        OnPropertyChanged(nameof(PomodoroPhaseName));
    }

    /// <summary>Change the work length. Idle re-arms the remaining time
    /// at once; mid-session it only applies to the next focus session.</summary>
    public void SetWorkMinutes(int minutes)
    {
        WorkMinutes = Math.Clamp(minutes, 5, 60);
        OnPropertyChanged(nameof(WorkMinutes));
        if (PomodoroPhase == FocusPomodoroPhase.Idle)
            PomodoroRemainingSeconds = WorkMinutes * 60;
    }

    /// <summary>Change the rest length. Applies to the next rest only.</summary>
    public void SetRestMinutes(int minutes)
    {
        RestMinutes = Math.Clamp(minutes, 5, 15);
        OnPropertyChanged(nameof(RestMinutes));
    }

    /// <summary>Start / stop the 20-20-20 eye-rest reminders. While
    /// running, a reminder fires every 20 minutes. Stopping re-arms
    /// the interval to the full 20 minutes.</summary>
    public void ToggleEyeRest()
    {
        EyeRestRunning = !EyeRestRunning;
        if (!EyeRestRunning)
            EyeRestRemainingSeconds = EyeRestIntervalSeconds;
    }

    /// <summary>One second of time: advance the Pomodoro, the eye-rest
    /// interval, and a snoozed reminder. Called by the store's 1s
    /// ticker; the ticker runs only while HasActiveCountdown.</summary>
    public void Tick()
    {
        if (PomodoroRunning)
        {
            PomodoroRemainingSeconds--;
            if (PomodoroRemainingSeconds <= 0)
                PomodoroIntervalEnded();
        }
        if (EyeRestRunning)
        {
            EyeRestRemainingSeconds--;
            if (EyeRestRemainingSeconds <= 0)
            {
                EyeRestRemainingSeconds = EyeRestIntervalSeconds;
                FireReminder(new FocusReminder(FocusReminderKind.EyeRest, 0, EyeRestCountdownSeconds));
            }
        }
        if (SnoozeRemainingSeconds > 0)
        {
            SnoozeRemainingSeconds--;
            OnPropertyChanged(nameof(SnoozeRemainingSeconds));
            if (SnoozeRemainingSeconds <= 0 && PendingReminder is { } pending)
                FireReminder(pending);
        }
    }

    private void PomodoroIntervalEnded()
    {
        if (PomodoroPhase == FocusPomodoroPhase.Focus)
        {
            CompletedFocusCycles++;
            var isLongBreak = CompletedFocusCycles % 4 == 0;
            var breakMinutes = isLongBreak ? LongBreakMinutes : RestMinutes;
            PomodoroPhase = isLongBreak ? FocusPomodoroPhase.LongBreak : FocusPomodoroPhase.Rest;
            PomodoroRemainingSeconds = breakMinutes * 60;
            OnPropertyChanged(nameof(PomodoroPhaseName));
            FireReminder(new FocusReminder(FocusReminderKind.WorkBreak, breakMinutes, breakMinutes * 60));
        }
        else
        {
            // A rest / long break ended: repeat loops back to a focus
            // session, otherwise the Pomodoro goes idle.
            if (RepeatAutomatically)
            {
                PomodoroPhase = FocusPomodoroPhase.Focus;
                PomodoroRemainingSeconds = WorkMinutes * 60;
                OnPropertyChanged(nameof(PomodoroPhaseName));
            }
            else
            {
                PomodoroPhase = FocusPomodoroPhase.Idle;
                PomodoroRunning = false;
                PomodoroRemainingSeconds = WorkMinutes * 60;
                OnPropertyChanged(nameof(PomodoroPhaseName));
            }
        }
    }

    /// <summary>Fire a reminder: while reading it waits (pending)
    /// when the "fires while reading" setting says to wait, otherwise
    /// it shows now. Called on a focus end, an eye-rest interval, and
    /// a snooze expiry.</summary>
    private void FireReminder(FocusReminder reminder)
    {
        if (IsReading() && WaitWhileReading())
        {
            PendingReminder = reminder;
        }
        else
        {
            ShowReminder(reminder);
        }
    }

    /// <summary>Show a reminder now: clear any waiting reminder and
    /// any snooze first, then raise ReminderReady for the store.</summary>
    private void ShowReminder(FocusReminder reminder)
    {
        PendingReminder = null;
        SnoozeRemainingSeconds = 0;
        OnPropertyChanged(nameof(SnoozeRemainingSeconds));
        ReminderReady?.Invoke(reminder);
    }

    /// <summary>"Take a break" (banner / notification): show the
    /// waiting reminder now. No-op when nothing waits.</summary>
    public void TakePendingBreakNow()
    {
        if (PendingReminder is { } pending)
            ShowReminder(pending);
    }

    /// <summary>"Snooze 5 min" (banner / notification): the waiting
    /// reminder fires again in 5 minutes. It keeps waiting (banner +
    /// dot stay) until then. No-op when nothing waits.</summary>
    public void SnoozePendingReminder()
    {
        if (PendingReminder is not null)
        {
            SnoozeRemainingSeconds = SnoozeSeconds;
            OnPropertyChanged(nameof(SnoozeRemainingSeconds));
        }
    }

    /// <summary>Show the waiting reminder now, if any — called when a
    /// read ends (window closed / panel read stopped / panel hidden),
    /// so a reminder that waited out the read appears on its own.</summary>
    public void ShowPendingReminder()
    {
        if (PendingReminder is { } pending)
            ShowReminder(pending);
    }

    /// <summary>"24:59" — minutes without padding, seconds 2-digit
    /// (mac's mmss).</summary>
    public static string Mmss(int seconds)
    {
        seconds = Math.Max(0, seconds);
        return $"{seconds / 60}:{seconds % 60:00}";
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
