// Break-reminder banner code-behind (feature/focus-timer-decor, mac
// parity: PendingReminderBanner.swift). Shows the engine's waiting
// reminder (title + "Your reading stays as it is.") with the two
// actions: take the break now, or snooze it 5 minutes.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace TransPeek.FocusTimer;

public sealed partial class PendingReminderBanner : UserControl
{
    private FocusTimerStore? _store;

    /// <summary>The store (set by the panel after construction).
    /// Subscribes to the engine so the banner appears the moment a
    /// reminder starts waiting.</summary>
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

    public PendingReminderBanner()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
    }

    private void OnEngineChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Core.FocusTimer.FocusTimerEngine.PendingReminder))
            Refresh();
    }

    private void Refresh()
    {
        var pending = _store?.Engine.PendingReminder;
        if (pending is null)
        {
            BannerBorder.Visibility = Visibility.Collapsed;
            return;
        }
        TitleText.Text = pending.NotificationTitle;
        BannerBorder.Visibility = Visibility.Visible;
    }

    private void OnTakeBreakClicked(object sender, RoutedEventArgs e)
        => _store?.TakePendingBreakNow();

    private void OnSnoozeClicked(object sender, RoutedEventArgs e)
        => _store?.SnoozePendingReminder();
}
