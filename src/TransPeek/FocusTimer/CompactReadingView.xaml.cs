// Lite Fast Read reader code-behind (feature/focus-timer-decor, mac
// parity: CompactReadingView in PanelFastReadView.swift). Renders one
// RsvpReaderViewModel — the same class the Fast Read window uses — in
// the compact layout for panel right and the Rest window. The host
// sets ViewModel (rebinding when a new read starts) and the two
// callbacks: OnCloseReading (X button; null in the Rest window —
// it closes itself on Esc / Done) and OnToggleMaximize (the panel's
// maximize toggle; null in the Rest window, which has no maximize).
// Esc is NOT handled here — the host handles it (two-stage in the
// panel, close in the Rest window), like mac.

using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using TransPeek.FastRead;
using TransPeek.Share;
using Windows.Foundation;
using Windows.System;

namespace TransPeek.FocusTimer;

public sealed partial class CompactReadingView : UserControl
{
    private RsvpReaderViewModel? _viewModel;
    private readonly TextBlock _measurer = new();
    private bool _loading;
    private bool _updatingProgress;
    private bool _fullSize;
    // The share row is filled once per finished read (Populate rebuilds
    // the buttons — re-filling on every chrome update would reset
    // hover/focus for nothing). Cleared when the finish clears.
    private bool _sharePopulated;

    /// <summary>The view model to render. Rebinding (a new read) does
    /// a full sync — the view model may already be playing by then.</summary>
    public RsvpReaderViewModel? ViewModel
    {
        get => _viewModel;
        set
        {
            if (_viewModel is not null)
                _viewModel.PropertyChanged -= OnViewModelChanged;
            _viewModel = value;
            if (_viewModel is not null)
                _viewModel.PropertyChanged += OnViewModelChanged;
            FullSync();
        }
    }

    /// <summary>Close the read and return to the demo (panel only).
    /// Null hides the X button (the Rest window).</summary>
    public Action? OnCloseReading { get; set; }

    /// <summary>Toggle the panel between default and max size (panel
    /// only). Null hides the Maximize button (the Rest window).</summary>
    public Action? OnToggleMaximize { get; set; }

    public CompactReadingView()
    {
        InitializeComponent();
        // Speed picker (mac: menu picker, 50-2000 step 50 — the window
        // uses a slider, which does not fit this tight top row).
        for (var wpm = 50; wpm <= 2000; wpm += 50)
            WpmCombo.Items.Add(new ComboBoxItem { Content = wpm.ToString(), Tag = wpm.ToString() });
        ProgressSlider.AddHandler(UIElement.PointerWheelChangedEvent, new PointerEventHandler(OnProgressWheelChanged), true);
        RootGrid.SizeChanged += (_, e) =>
        {
            var full = e.NewSize.Width >= 540;
            if (full != _fullSize)
            {
                _fullSize = full;
                UpdateChrome();
            }
        };
    }

    /// <summary>Keyboard shortcuts for the read (called by the host's
    /// KeyDown). Space play/pause, Left/Right step, Up/Down speed
    /// ±50, R restart. Esc is the host's job (two-stage in the panel,
    /// close in the Rest window) — never handled here. Returns true
    /// when the key was consumed.</summary>
    public bool HandleKey(VirtualKey key, object? focusedElement)
    {
        if (_viewModel is null)
            return false;
        switch (key)
        {
            case VirtualKey.Space:
                if (focusedElement is Button)
                    return false; // a focused button handles Space itself.
                _viewModel.SetPlaying(!_viewModel.IsPlaying);
                return true;
            case VirtualKey.Left:
                if (focusedElement is Slider)
                    return false; // the progress slider seeks by itself.
                _viewModel.StepBack();
                return true;
            case VirtualKey.Right:
                if (focusedElement is Slider)
                    return false;
                _viewModel.StepForward();
                return true;
            case VirtualKey.Up:
                if (focusedElement is Slider)
                    return false;
                _viewModel.SetWpm(_viewModel.Wpm + 50);
                return true;
            case VirtualKey.Down:
                if (focusedElement is Slider)
                    return false;
                _viewModel.SetWpm(_viewModel.Wpm - 50);
                return true;
            case VirtualKey.R:
                _viewModel.Restart();
                return true;
            default:
                return false;
        }
    }

    private void OnViewModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_viewModel is null)
            return;
        switch (e.PropertyName)
        {
            case nameof(RsvpReaderViewModel.CurrentSlide):
            case nameof(RsvpReaderViewModel.FontSize):
                RenderSlide();
                break;
            case nameof(RsvpReaderViewModel.IsPlaying):
                PlayPauseIcon.Glyph = _viewModel.IsPlaying ? "\uE769" : "\uE768";
                UpdateChrome();
                break;
            case nameof(RsvpReaderViewModel.IsFinished):
                UpdateChrome();
                break;
            case nameof(RsvpReaderViewModel.StatusText):
            case nameof(RsvpReaderViewModel.CurrentSlideIndex):
            case nameof(RsvpReaderViewModel.SlideCount):
                SyncProgress();
                break;
            case nameof(RsvpReaderViewModel.PauseContext):
            case nameof(RsvpReaderViewModel.ShowContext):
            case nameof(RsvpReaderViewModel.ContextSizeRatio):
                RenderContext();
                break;
            case nameof(RsvpReaderViewModel.ReadingTimeText):
                ReadingTimeText.Text = _viewModel.ReadingTimeText;
                break;
            case nameof(RsvpReaderViewModel.Wpm):
            case nameof(RsvpReaderViewModel.ChunkSize):
            case nameof(RsvpReaderViewModel.ContextDisplay):
            case nameof(RsvpReaderViewModel.ColorScheme):
                SyncControls();
                break;
        }
    }

    private void FullSync()
    {
        if (_viewModel is null)
            return;
        RenderSlide();
        SyncProgress();
        RenderContext();
        SyncControls();
        PlayPauseIcon.Glyph = _viewModel.IsPlaying ? "\uE769" : "\uE768";
        ReadingTimeText.Text = _viewModel.ReadingTimeText;
        UpdateChrome();
    }

    /// <summary>Which chrome shows: playing = only the focus line (+
    /// the book page when Always) and the hint; paused = top bar,
    /// progress and transport; finished adds the congratulations line —
    /// the full sentence + the share row at full size, just
    /// "Congratulations!" at the small size (mac shows the sentence +
    /// share full-size only); full size (width &gt;= 540) also shows
    /// the Reading time (left) and the hint (right) in the controls
    /// row, like the window.</summary>
    private void UpdateChrome()
    {
        if (_viewModel is null)
            return;
        var playing = _viewModel.IsPlaying;
        TopBarGrid.Visibility = playing ? Visibility.Collapsed : Visibility.Visible;
        HintTextBlock.Visibility = playing ? Visibility.Visible : Visibility.Collapsed;
        ProgressGrid.Visibility = playing ? Visibility.Collapsed : Visibility.Visible;
        ControlsGrid.Visibility = playing ? Visibility.Collapsed : Visibility.Visible;
        if (!playing && _viewModel.IsFinished)
        {
            CompletionText.Visibility = Visibility.Visible;
            if (_fullSize && _viewModel.CompletionText is { } completion)
            {
                CompletionText.Text = completion;
                if (!_sharePopulated)
                {
                    ShareRowBuilder.Populate(CompletionShareRowPanel, _viewModel.ShareText);
                    _sharePopulated = true;
                }
                CompletionShareRowPanel.Visibility = Visibility.Visible;
            }
            else
            {
                CompletionText.Text = "Congratulations!";
                CompletionShareRowPanel.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            CompletionText.Visibility = Visibility.Collapsed;
            CompletionShareRowPanel.Visibility = Visibility.Collapsed;
            _sharePopulated = false;
        }
        ReadingTimeText.Visibility = !playing && _fullSize ? Visibility.Visible : Visibility.Collapsed;
        SideHintText.Visibility = !playing && _fullSize ? Visibility.Visible : Visibility.Collapsed;
        CloseButton.Visibility = OnCloseReading is not null ? Visibility.Visible : Visibility.Collapsed;
        MaximizeButton.Visibility = OnToggleMaximize is not null ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SyncControls()
    {
        if (_viewModel is null)
            return;
        _loading = true;
        SelectComboByTag(WpmCombo, NearestWpm(_viewModel.Wpm).ToString());
        SelectComboByTag(FocusSizeCombo, NearestFocusSize(_viewModel.FontSize).ToString());
        SelectComboByTag(ContextSizeCombo, NearestContextPercent(_viewModel.ContextSizeRatio * 100).ToString());
        SelectComboByTag(ChunkCombo, _viewModel.ChunkSize.ToString());
        SelectComboByTag(ColorSchemeCombo, _viewModel.ColorScheme);
        SelectComboByTag(ContextDisplayCombo, _viewModel.ContextDisplay);
        ApplyColorScheme();
        _loading = false;
    }

    private void RenderSlide()
    {
        if (_viewModel is null)
            return;
        var slide = _viewModel.CurrentSlide;
        WordTextBlock.FontSize = _viewModel.FontSize;
        if (slide is null)
        {
            LeftRun.Text = "";
            PivotRun.Text = "";
            RightRun.Text = "";
            WordTextBlock.RenderTransform = null;
            return;
        }
        if (slide.PivotCharIndex >= 0 && slide.PivotCharIndex < slide.Text.Length)
        {
            LeftRun.Text = slide.Text[..slide.PivotCharIndex];
            PivotRun.Text = slide.Text.Substring(slide.PivotCharIndex, 1);
            RightRun.Text = slide.Text[(slide.PivotCharIndex + 1)..];
            AlignPivot(slide.Text, slide.PivotCharIndex);
        }
        else
        {
            LeftRun.Text = slide.Text;
            PivotRun.Text = "";
            RightRun.Text = "";
            WordTextBlock.RenderTransform = null;
        }
    }

    private void AlignPivot(string text, int pivotIndex)
    {
        _measurer.FontFamily = WordTextBlock.FontFamily;
        _measurer.FontSize = WordTextBlock.FontSize;
        _measurer.FontWeight = WordTextBlock.FontWeight;
        _measurer.Text = text[..pivotIndex];
        _measurer.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var leftWidth = _measurer.DesiredSize.Width;
        _measurer.Text = text.Substring(pivotIndex, 1);
        _measurer.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var pivotWidth = _measurer.DesiredSize.Width;
        _measurer.Text = text;
        _measurer.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var wordWidth = _measurer.DesiredSize.Width;
        var shift = wordWidth / 2 - (leftWidth + pivotWidth / 2);
        WordTextBlock.RenderTransform = new TranslateTransform { X = shift };
    }

    /// <summary>Book page (mac: bookPage): the words before the current
    /// chunk above the focus line, the words after it below — the focus
    /// line stays pinned at the vertical center. Shown when the view
    /// model says the context shows (Always, or paused with Paused);
    /// hidden otherwise, and the focus line centers on its own.</summary>
    private void RenderContext()
    {
        if (_viewModel is null)
            return;
        var context = _viewModel.PauseContext;
        if (!_viewModel.ShowContext || context is null)
        {
            BeforeTextBlock.Visibility = Visibility.Collapsed;
            AfterTextBlock.Visibility = Visibility.Collapsed;
            return;
        }
        var contextSize = _viewModel.ContextFontSize;
        BeforeTextBlock.FontSize = contextSize;
        BeforeTextBlock.LineHeight = contextSize * 1.5;
        AfterTextBlock.FontSize = contextSize;
        AfterTextBlock.LineHeight = contextSize * 1.5;
        BeforeTextBlock.Text = string.Join(" ", context.Words.Take(context.CurrentStart));
        AfterTextBlock.Text = string.Join(" ", context.Words.Skip(context.CurrentStart + context.CurrentCount));
        BeforeTextBlock.Visibility = Visibility.Visible;
        AfterTextBlock.Visibility = Visibility.Visible;
    }

    private void SyncProgress()
    {
        if (_viewModel is null)
            return;
        var total = _viewModel.SlideCount;
        _updatingProgress = true;
        ProgressSlider.Minimum = 0;
        ProgressSlider.Maximum = Math.Max(0, total - 1);
        ProgressSlider.Value = _viewModel.CurrentSlideIndex;
        _updatingProgress = false;
        StatusText.Text = _viewModel.StatusText;
    }

    /// <summary>Color scheme (mac round 7): White on Black = dark
    /// theme (black page, light controls) via RequestedTheme + a black
    /// page background; Black on White = the default light theme. The
    /// Flyout contents get the same theme — a Flyout is a separate
    /// popup, so it does not inherit the Grid's theme.</summary>
    private void ApplyColorScheme()
    {
        if (_viewModel is null)
            return;
        var dark = string.Equals(_viewModel.ColorScheme, "Dark", StringComparison.OrdinalIgnoreCase);
        var theme = dark ? ElementTheme.Dark : ElementTheme.Light;
        RootGrid.RequestedTheme = theme;
        if (dark)
            RootGrid.Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x00, 0x00, 0x00));
        else
            RootGrid.ClearValue(Grid.BackgroundProperty);
        if (SettingsFlyout.Content is FrameworkElement settingsContent)
            settingsContent.RequestedTheme = theme;
        if (HotkeysFlyout.Content is FrameworkElement hotkeysContent)
            hotkeysContent.RequestedTheme = theme;
    }

    private void OnPlayPauseClicked(object sender, RoutedEventArgs e)
        => _viewModel?.SetPlaying(!_viewModel.IsPlaying);

    private void OnRestartClicked(object sender, RoutedEventArgs e)
        => _viewModel?.Restart();

    private void OnRewindClicked(object sender, RoutedEventArgs e)
        => _viewModel?.StepBack();

    private void OnFastForwardClicked(object sender, RoutedEventArgs e)
        => _viewModel?.StepForward();

    private void OnCloseClicked(object sender, RoutedEventArgs e)
        => OnCloseReading?.Invoke();

    private void OnMaximizeClicked(object sender, RoutedEventArgs e)
        => OnToggleMaximize?.Invoke();

    private void OnProgressChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_loading || _updatingProgress || _viewModel is null)
            return;
        _viewModel.SeekTo((int)Math.Round(e.NewValue));
    }

    /// <summary>Wheel over the progress slider: one slide per tick,
    /// like the window (attached with handledEventsToo — a Slider
    /// consumes the wheel itself and marks it handled).</summary>
    private void OnProgressWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (_viewModel is null)
            return;
        var delta = e.GetCurrentPoint((UIElement)sender).Properties.MouseWheelDelta;
        if (delta == 0)
            return;
        _viewModel.SeekTo(_viewModel.CurrentSlideIndex + (delta < 0 ? 1 : -1));
        e.Handled = true;
    }

    /// <summary>Wheel over the reading area: scroll up grows the focus
    /// size, scroll down shrinks it, 5pt per tick — NO cap (Manh
    /// 2026-11-20, 07:02: swipe freely, the user trims it to the
    /// frame). The 1pt floor is only a crash guard (a zero or negative
    /// font size cannot render), not a cap. The picker shows the
    /// nearest option — the value itself stays exact, like mac.</summary>
    private void OnReadingWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (_viewModel is null)
            return;
        var delta = e.GetCurrentPoint((UIElement)sender).Properties.MouseWheelDelta;
        if (delta == 0)
            return;
        var size = Math.Max(1, _viewModel.FontSize + (delta > 0 ? 5 : -5));
        _viewModel.ApplyFocusSize(size);
        e.Handled = true;
    }

    private void OnWpmChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || _viewModel is null)
            return;
        if (WpmCombo.SelectedItem is ComboBoxItem item
            && item.Tag is string tag && int.TryParse(tag, out var wpm))
            _viewModel.SetWpm(wpm);
    }

    private void OnFocusSizeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || _viewModel is null)
            return;
        if (FocusSizeCombo.SelectedItem is ComboBoxItem item
            && item.Tag is string tag && double.TryParse(tag, out var size))
            _viewModel.ApplyFocusSize(size);
    }

    private void OnContextSizeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || _viewModel is null)
            return;
        if (ContextSizeCombo.SelectedItem is ComboBoxItem item
            && item.Tag is string tag && int.TryParse(tag, out var percent))
            _viewModel.SetContextSizeRatio(percent / 100.0);
    }

    private void OnChunkChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || _viewModel is null)
            return;
        if (ChunkCombo.SelectedItem is ComboBoxItem item
            && item.Tag is string tag && int.TryParse(tag, out var chunk))
            _viewModel.SetChunkSize(chunk);
    }

    private void OnColorSchemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || _viewModel is null)
            return;
        if (ColorSchemeCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            _viewModel.SetColorScheme(tag);
    }

    private void OnContextDisplayChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || _viewModel is null)
            return;
        if (ContextDisplayCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            _viewModel.SetContextDisplay(tag);
    }

    private void OnResetClicked(object sender, RoutedEventArgs e)
        => _viewModel?.ResetToDefaults();

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

    /// <summary>Speed picker options (mac: 50-2000 step 50, default
    /// 500 on mac, 275 on Windows): the picker shows the option
    /// nearest to a value — the wheel and the ↑/↓ keys move the speed
    /// by 50, the value itself stays exact.</summary>
    private static int NearestWpm(int wpm)
        => Math.Clamp((int)Math.Round(wpm / 50.0) * 50, 50, 2000);

    private static readonly int[] FocusSizeOptions =
        [40, 48, 56, 64, 72, 80, 88, 96, 104, 110, 120, 140, 160, 200, 250, 300, 350, 400, 450];

    private static int NearestFocusSize(double size)
        => FocusSizeOptions.MinBy(option => Math.Abs(option - size));

    private static readonly int[] ContextSizeOptions = [25, 30, 35, 40, 50, 60, 75, 100];

    private static int NearestContextPercent(double percent)
        => ContextSizeOptions.MinBy(option => Math.Abs(option - percent));
}
