// Panel right — Fast Read lite code-behind (feature/focus-timer-decor,
// mac parity: PanelFastReadView.swift). Two states: the demo (nothing
// reads) and the CompactReadingView (a read is running). The store
// owns the read: this view observes PanelReadingViewModel (binds the
// compact view to it) and PanelReminderCountdown (the "Rest — mm:ss"
// row after a reminder read). When the bound read finishes, the
// countdown starts (a reminder read) or the completion shows (a
// manual read — the compact view's "Congratulations!", closed with X).

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TransPeek.Core.FocusTimer;
using TransPeek.FastRead;
using Windows.Foundation;

namespace TransPeek.FocusTimer;

public sealed partial class PanelFastReadView : UserControl
{
    private FocusTimerStore? _store;
    private RsvpReaderViewModel? _boundViewModel;
    private readonly TextBlock _demoMeasurer = new();
    private bool _loading;

    private string _sourceText = "";

    /// <summary>The text the demo's "Read in panel" button reads (the
    /// text being translated). Set by the panel; the button is disabled
    /// while it is empty.</summary>
    public string SourceText
    {
        get => _sourceText;
        set
        {
            _sourceText = value;
            if (ReadInPanelButton is not null)
                ReadInPanelButton.IsEnabled = !string.IsNullOrWhiteSpace(value);
        }
    }

    /// <summary>The store (set by the panel after construction).</summary>
    public FocusTimerStore? Store
    {
        get => _store;
        set
        {
            if (_store is not null)
                _store.PropertyChanged -= OnStoreChanged;
            _store = value;
            if (_store is not null)
            {
                _store.PropertyChanged += OnStoreChanged;
                Refresh();
            }
        }
    }

    /// <summary>Called when a read starts from this view (the demo's
    /// "Read in panel" button): the panel pins itself.</summary>
    public Action? OnStartReading { get; set; }

    /// <summary>Called when the compact view wants to toggle the
    /// panel size (its Maximize button): the panel toggles between
    /// default and max size.</summary>
    public Action? OnToggleMaximize { get; set; }

    public PanelFastReadView()
    {
        InitializeComponent();
        CompactView.OnCloseReading = () => _store?.StopPanelReading();
        CompactView.OnToggleMaximize = () => OnToggleMaximize?.Invoke();
        Loaded += (_, _) =>
        {
            AlignDemoPivot();
            Refresh();
        };
    }

    private void OnStoreChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FocusTimerStore.PanelReadingViewModel)
            || e.PropertyName == nameof(FocusTimerStore.PanelReminderCountdown))
            Refresh();
    }

    private void Refresh()
    {
        if (_store is null)
            return;
        _loading = true;
        var reading = _store.PanelReadingViewModel is not null;
        DemoGrid.Visibility = reading ? Visibility.Collapsed : Visibility.Visible;
        ReadingGrid.Visibility = reading ? Visibility.Visible : Visibility.Collapsed;
        ReadInPanelButton.IsEnabled = !string.IsNullOrWhiteSpace(SourceText);
        var runInPanel = string.Equals(App.Settings.FastReadRunLocation, "RightPanel", StringComparison.OrdinalIgnoreCase);
        RunInPanelRadio.IsChecked = runInPanel;
        RunInWindowRadio.IsChecked = !runInPanel;
        if (_store.PanelReminderCountdown is { } seconds)
        {
            CountdownText.Text = $"Rest — {FocusTimerEngine.Mmss(seconds)}";
            CountdownGrid.Visibility = Visibility.Visible;
        }
        else
        {
            CountdownGrid.Visibility = Visibility.Collapsed;
        }
        BindReading();
        _loading = false;
    }

    /// <summary>Bind the compact view to the store's current read (and
    /// watch that read's IsFinished: a finished reminder read starts
    /// the countdown, a finished manual read just shows the
    /// completion).</summary>
    private void BindReading()
    {
        if (_store is null)
            return;
        var viewModel = _store.PanelReadingViewModel;
        if (viewModel == _boundViewModel)
            return;
        if (_boundViewModel is not null)
            _boundViewModel.PropertyChanged -= OnBoundViewModelChanged;
        _boundViewModel = viewModel;
        CompactView.ViewModel = viewModel;
        if (_boundViewModel is not null)
            _boundViewModel.PropertyChanged += OnBoundViewModelChanged;
    }

    private void OnBoundViewModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RsvpReaderViewModel.IsFinished)
            && _boundViewModel?.IsFinished == true)
            _store?.StartPanelReminderCountdown();
    }

    /// <summary>Forward a key to the lite reader (Space / arrows /
    /// R). Returns true when the reader handled it. Esc is NOT handled
    /// here — the panel host stops the read / hides the panel itself
    /// (two-stage Esc).</summary>
    public bool HandleKey(Windows.System.VirtualKey key, object? focusedElement)
        => CompactView.HandleKey(key, focusedElement);

    /// <summary>"Read in panel": always reads in the panel, whatever
    /// the "Run FastRead in" radios say (mac). The panel pins itself
    /// via OnStartReading.</summary>
    private void OnReadInPanelClicked(object sender, RoutedEventArgs e)
    {
        if (_store is null || string.IsNullOrWhiteSpace(SourceText))
            return;
        _store.StartPanelReading(SourceText);
        OnStartReading?.Invoke();
    }

    private void OnRunLocationChecked(object sender, RoutedEventArgs e)
    {
        if (_loading || _store is null)
            return;
        _store.SetRunLocation(RunInPanelRadio.IsChecked == true ? "RightPanel" : "NewWindow");
    }

    private void OnCountdownDoneClicked(object sender, RoutedEventArgs e)
        => _store?.StopPanelReading();

    /// <summary>Shift the demo word so the "a" sits exactly at the
    /// center — the two half-bars (centered in the StackPanel) then
    /// line up with it, like the reader's AlignPivot.</summary>
    private void AlignDemoPivot()
    {
        _demoMeasurer.FontFamily = DemoWordTextBlock.FontFamily;
        _demoMeasurer.FontSize = DemoWordTextBlock.FontSize;
        _demoMeasurer.FontWeight = DemoWordTextBlock.FontWeight;
        _demoMeasurer.Text = "re";
        _demoMeasurer.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var leftWidth = _demoMeasurer.DesiredSize.Width;
        _demoMeasurer.Text = "a";
        _demoMeasurer.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var pivotWidth = _demoMeasurer.DesiredSize.Width;
        _demoMeasurer.Text = "reading";
        _demoMeasurer.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var wordWidth = _demoMeasurer.DesiredSize.Width;
        var shift = wordWidth / 2 - (leftWidth + pivotWidth / 2);
        DemoWordTextBlock.RenderTransform = new TranslateTransform { X = shift };
    }
}
