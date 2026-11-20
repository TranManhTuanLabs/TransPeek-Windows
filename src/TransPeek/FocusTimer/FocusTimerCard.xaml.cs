// Focus & Rest card code-behind (feature/focus-timer-decor, mac
// parity: FocusTimerCard.swift). Renders the FocusTimerEngine's state
// (Pomodoro + eye-rest + waiting reminder) and forwards the card's
// controls to the store. The engine notifies on every tick, so this
// just re-renders from the engine on PropertyChanged — no local state
// except the _loading guard for the picker sync.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TransPeek.Core.FocusTimer;
using TransPeek.Core.Settings;

namespace TransPeek.FocusTimer;

public sealed partial class FocusTimerCard : UserControl
{
    private FocusTimerStore? _store;
    private bool _loading;

    /// <summary>The store (set by the panel after construction). Subscribes
    /// to the engine so the countdown ticks re-render the card.</summary>
    public FocusTimerStore? Store
    {
        get => _store;
        set
        {
            if (_store is not null)
                _store.Engine.PropertyChanged -= OnEngineChanged;
            _store = value;
            if (_store is not null)
            {
                _store.Engine.PropertyChanged += OnEngineChanged;
                Refresh();
            }
        }
    }

    public FocusTimerCard()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
    }

    private void OnEngineChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => Refresh();

    private void Refresh()
    {
        if (_store is null)
            return;
        var engine = _store.Engine;
        _loading = true;
        PhaseText.Text = engine.PomodoroPhaseName;
        CountdownText.Text = FocusTimerEngine.Mmss(engine.PomodoroRemainingSeconds);
        StartPauseButton.Content = engine.PomodoroPhase == FocusPomodoroPhase.Idle
            ? "Start focus"
            : engine.PomodoroRunning ? "Pause" : "Resume";
        // Cycle dots: filled for completed focus cycles (mac: accent /
        // secondary 0.4 — the tab dots' #0EA5E9 / #C8C8C8 here).
        var dots = new[] { CycleDot0, CycleDot1, CycleDot2, CycleDot3 };
        for (var i = 0; i < dots.Length; i++)
        {
            var filled = i < engine.CompletedFocusCycles;
            dots[i].Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                filled
                    ? Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x0E, 0xA5, 0xE9)
                    : Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xC8, 0xC8, 0xC8));
        }
        SelectComboByTag(WorkCombo, engine.WorkMinutes.ToString());
        SelectComboByTag(RestCombo, engine.RestMinutes.ToString());
        RepeatToggle.IsOn = engine.RepeatAutomatically;
        EyeRestStatusText.Text = engine.EyeRestRunning
            ? $"Next in {FocusTimerEngine.Mmss(engine.EyeRestRemainingSeconds)}"
            : "Off";
        EyeRestButton.Content = engine.EyeRestRunning ? "Stop eye reminders" : "Start eye reminders";
        SelectComboByTag(ReminderDestinationCombo, App.Settings.FocusReminderDestination);
        PendingDot.Visibility = engine.PendingReminder is not null ? Visibility.Visible : Visibility.Collapsed;
        ApplyCollapsed();
        _loading = false;
    }

    private void ApplyCollapsed()
    {
        var collapsed = App.Settings.FocusTimerCollapsed;
        FocusTimerBody.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        FocusTimerChevronText.Text = collapsed ? "›" : "⌄";
    }

    private void OnHeaderClicked(object sender, RoutedEventArgs e)
    {
        App.Settings.FocusTimerCollapsed = !App.Settings.FocusTimerCollapsed;
        SettingsStore.Save(App.Settings);
        ApplyCollapsed();
    }

    private void OnStartPauseClicked(object sender, RoutedEventArgs e)
        => _store?.TogglePomodoro();

    private void OnResetClicked(object sender, RoutedEventArgs e)
        => _store?.ResetPomodoro();

    private void OnRepeatToggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
            return;
        _store?.SetRepeatAutomatically(RepeatToggle.IsOn);
    }

    private void OnWorkChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading)
            return;
        if (WorkCombo.SelectedItem is ComboBoxItem item
            && item.Tag is string tag && int.TryParse(tag, out var minutes))
            _store?.SetWorkMinutes(minutes);
    }

    private void OnRestChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading)
            return;
        if (RestCombo.SelectedItem is ComboBoxItem item
            && item.Tag is string tag && int.TryParse(tag, out var minutes))
            _store?.SetRestMinutes(minutes);
    }

    private void OnEyeRestClicked(object sender, RoutedEventArgs e)
        => _store?.ToggleEyeRest();

    private void OnReminderDestinationChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading)
            return;
        if (ReminderDestinationCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            _store?.SetReminderDestination(tag);
    }

    private static void SelectComboByTag(ComboBox combo, string tag)
    {
        foreach (var item in combo.Items)
        {
            if (item is ComboBoxItem comboItem
                && string.Equals(comboItem.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = item;
                return;
            }
        }
    }
}
