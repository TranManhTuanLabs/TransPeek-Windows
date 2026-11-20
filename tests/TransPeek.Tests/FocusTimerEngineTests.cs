// Tests for the Focus timer engine (feature/focus-timer-decor).
// Pure logic — no UI thread needed.

using TransPeek.Core.FocusTimer;
using Xunit;

namespace TransPeek.Tests;

public sealed class FocusTimerEngineTests
{
    private static void TickMany(FocusTimerEngine engine, int seconds)
    {
        for (var i = 0; i < seconds; i++)
            engine.Tick();
    }

    [Fact]
    public void TogglePomodoro_StartsFocus_WithWorkLength()
    {
        var engine = new FocusTimerEngine(workMinutes: 25, restMinutes: 5);
        engine.TogglePomodoro();
        Assert.Equal(FocusPomodoroPhase.Focus, engine.PomodoroPhase);
        Assert.True(engine.PomodoroRunning);
        Assert.Equal(25 * 60, engine.PomodoroRemainingSeconds);
        Assert.Equal("Focus", engine.PomodoroPhaseName);
    }

    [Fact]
    public void FocusEnds_MovesToRest_AndFiresWorkBreakReminder()
    {
        var engine = new FocusTimerEngine(workMinutes: 5, restMinutes: 5);
        FocusReminder? shown = null;
        engine.ReminderReady += r => shown = r;
        engine.TogglePomodoro();
        TickMany(engine, 5 * 60);
        Assert.Equal(1, engine.CompletedFocusCycles);
        Assert.Equal(FocusPomodoroPhase.Rest, engine.PomodoroPhase);
        Assert.Equal(5 * 60, engine.PomodoroRemainingSeconds);
        Assert.True(engine.PomodoroRunning); // the rest runs on
        Assert.NotNull(shown);
        Assert.Equal(FocusReminderKind.WorkBreak, shown!.Kind);
        Assert.Equal(5, shown.Minutes);
        Assert.Equal(5 * 60, shown.CountdownSeconds);
        Assert.Contains("5 minutes", shown.Message);
    }

    [Fact]
    public void FourthFocusEnds_MovesToLongBreak()
    {
        var engine = new FocusTimerEngine(workMinutes: 5, restMinutes: 5, longBreakMinutes: 15);
        var shown = new List<FocusReminder>();
        engine.ReminderReady += shown.Add;
        engine.TogglePomodoro();
        for (var cycle = 0; cycle < 4; cycle++)
        {
            TickMany(engine, 5 * 60); // focus ends
            if (cycle < 3)
            {
                Assert.Equal(FocusPomodoroPhase.Rest, engine.PomodoroPhase);
                TickMany(engine, 5 * 60); // rest ends, repeat loops back
                Assert.Equal(FocusPomodoroPhase.Focus, engine.PomodoroPhase);
            }
        }
        Assert.Equal(4, engine.CompletedFocusCycles);
        Assert.Equal(FocusPomodoroPhase.LongBreak, engine.PomodoroPhase);
        Assert.Equal(15 * 60, engine.PomodoroRemainingSeconds);
        Assert.Equal(15, shown[^1].Minutes);
    }

    [Fact]
    public void RestEnds_RepeatOn_LoopsBackToFocus()
    {
        var engine = new FocusTimerEngine(workMinutes: 5, restMinutes: 5, repeatAutomatically: true);
        engine.TogglePomodoro();
        TickMany(engine, 5 * 60); // focus ends -> rest
        TickMany(engine, 5 * 60); // rest ends -> focus again
        Assert.Equal(FocusPomodoroPhase.Focus, engine.PomodoroPhase);
        Assert.Equal(5 * 60, engine.PomodoroRemainingSeconds);
        Assert.True(engine.PomodoroRunning);
    }

    [Fact]
    public void RestEnds_RepeatOff_GoesIdle()
    {
        var engine = new FocusTimerEngine(workMinutes: 5, restMinutes: 5, repeatAutomatically: false);
        engine.TogglePomodoro();
        TickMany(engine, 5 * 60); // focus ends -> rest
        TickMany(engine, 5 * 60); // rest ends -> idle
        Assert.Equal(FocusPomodoroPhase.Idle, engine.PomodoroPhase);
        Assert.False(engine.PomodoroRunning);
        Assert.Equal(5 * 60, engine.PomodoroRemainingSeconds);
        Assert.Equal("Ready", engine.PomodoroPhaseName);
    }

    [Fact]
    public void TogglePomodoro_PausesAndResumes()
    {
        var engine = new FocusTimerEngine(workMinutes: 25, restMinutes: 5);
        engine.TogglePomodoro();
        TickMany(engine, 10);
        engine.TogglePomodoro(); // pause
        Assert.False(engine.PomodoroRunning);
        var frozen = engine.PomodoroRemainingSeconds;
        TickMany(engine, 10);
        Assert.Equal(frozen, engine.PomodoroRemainingSeconds);
        engine.TogglePomodoro(); // resume
        Assert.True(engine.PomodoroRunning);
        TickMany(engine, 5);
        Assert.Equal(frozen - 5, engine.PomodoroRemainingSeconds);
    }

    [Fact]
    public void ResetPomodoro_ReturnsToIdle_AndRearms()
    {
        var engine = new FocusTimerEngine(workMinutes: 5, restMinutes: 5);
        engine.TogglePomodoro();
        TickMany(engine, 5 * 60); // one focus done
        engine.ResetPomodoro();
        Assert.Equal(FocusPomodoroPhase.Idle, engine.PomodoroPhase);
        Assert.False(engine.PomodoroRunning);
        Assert.Equal(0, engine.CompletedFocusCycles);
        Assert.Equal(5 * 60, engine.PomodoroRemainingSeconds);
    }

    [Fact]
    public void SetWorkMinutes_IdleRearms_MidSessionKeepsRemaining()
    {
        var engine = new FocusTimerEngine(workMinutes: 25, restMinutes: 5);
        engine.SetWorkMinutes(10);
        Assert.Equal(10 * 60, engine.PomodoroRemainingSeconds);
        engine.TogglePomodoro();
        TickMany(engine, 60);
        engine.SetWorkMinutes(30); // mid-focus: next session only
        Assert.Equal(10 * 60 - 60, engine.PomodoroRemainingSeconds);
        Assert.Equal(30, engine.WorkMinutes);
    }

    [Fact]
    public void EyeRest_FiresEveryTwentyMinutes_AndRearms()
    {
        var engine = new FocusTimerEngine();
        var shown = new List<FocusReminder>();
        engine.ReminderReady += shown.Add;
        engine.ToggleEyeRest();
        Assert.True(engine.EyeRestRunning);
        TickMany(engine, 20 * 60);
        Assert.Single(shown);
        Assert.Equal(FocusReminderKind.EyeRest, shown[0].Kind);
        Assert.Equal(20, shown[0].CountdownSeconds);
        Assert.Equal(20 * 60, engine.EyeRestRemainingSeconds); // re-armed
        TickMany(engine, 20 * 60);
        Assert.Equal(2, shown.Count);
    }

    [Fact]
    public void EyeRest_StopRearmsInterval()
    {
        var engine = new FocusTimerEngine();
        engine.ToggleEyeRest();
        TickMany(engine, 100);
        engine.ToggleEyeRest(); // stop
        Assert.False(engine.EyeRestRunning);
        Assert.Equal(20 * 60, engine.EyeRestRemainingSeconds);
    }

    [Fact]
    public void ReminderWhileReading_Waits_AndShowsWhenReadEnds()
    {
        var engine = new FocusTimerEngine(workMinutes: 5, restMinutes: 5);
        FocusReminder? shown = null;
        engine.ReminderReady += r => shown = r;
        engine.IsReading = () => true;
        engine.TogglePomodoro();
        TickMany(engine, 5 * 60); // focus ends while reading
        Assert.Null(shown);
        Assert.NotNull(engine.PendingReminder);
        Assert.Equal(FocusReminderKind.WorkBreak, engine.PendingReminder!.Kind);
        engine.IsReading = () => false;
        engine.ShowPendingReminder(); // read ended
        Assert.NotNull(shown);
        Assert.Null(engine.PendingReminder);
    }

    [Fact]
    public void TakePendingBreakNow_ShowsWaitingReminder()
    {
        var engine = new FocusTimerEngine(workMinutes: 5, restMinutes: 5);
        FocusReminder? shown = null;
        engine.ReminderReady += r => shown = r;
        engine.IsReading = () => true;
        engine.TogglePomodoro();
        TickMany(engine, 5 * 60);
        Assert.NotNull(engine.PendingReminder);
        engine.TakePendingBreakNow();
        Assert.NotNull(shown);
        Assert.Null(engine.PendingReminder);
    }

    [Fact]
    public void SnoozePendingReminder_RefiresAfterFiveMinutes()
    {
        var engine = new FocusTimerEngine(workMinutes: 5, restMinutes: 5);
        var shown = new List<FocusReminder>();
        engine.ReminderReady += shown.Add;
        engine.IsReading = () => true;
        engine.TogglePomodoro();
        TickMany(engine, 5 * 60); // pending while reading
        Assert.NotNull(engine.PendingReminder);
        engine.SnoozePendingReminder();
        Assert.Equal(5 * 60, engine.SnoozeRemainingSeconds);
        Assert.NotNull(engine.PendingReminder); // still waiting
        TickMany(engine, 5 * 60 - 1);
        Assert.Empty(shown);
        TickMany(engine, 1); // snooze expires -> re-fire (still reading)
        Assert.NotNull(engine.PendingReminder);
        engine.IsReading = () => false;
        engine.SnoozePendingReminder(); // snooze again
        TickMany(engine, 5 * 60); // expires while not reading -> shows
        Assert.Single(shown);
        Assert.Null(engine.PendingReminder);
    }

    [Fact]
    public void Mmss_FormatsMinutesAndSeconds()
    {
        Assert.Equal("0:00", FocusTimerEngine.Mmss(0));
        Assert.Equal("1:00", FocusTimerEngine.Mmss(60));
        Assert.Equal("24:59", FocusTimerEngine.Mmss(1499));
        Assert.Equal("5:05", FocusTimerEngine.Mmss(305));
    }
}
