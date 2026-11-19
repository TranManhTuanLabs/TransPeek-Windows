// Fast Read (RSVP) reader window — feature/fast-read-rsvp.
// A separate window (not a panel tab): RSVP needs a big word, one
// fixed focal point, a progress bar and a pause context. The engine
// (TransPeek.Core.FastRead) is pure logic; this window only renders slides
// and drives the timer. Single instance owned by App: re-opening
// focuses the existing window and loads the new text. READ ONLY —
// no TTS in the reader (listening is the float icon's job).

using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using TransPeek.Core.FastRead;
using TransPeek.Core.Settings;
using TransPeek.Interop;
using Windows.Foundation;
using Windows.System;
using WinRT.Interop;

namespace TransPeek.FastRead;

public sealed partial class FastReadWindow : Window
{
    private readonly DispatcherTimer _timer = new();
    /// <summary>Detached TextBlock for measuring word widths (same font
    /// as the displayed word): the ORP pivot is aligned by shifting
    /// the centered word so the pivot char's center lands exactly at
    /// the word area's center.</summary>
    private readonly TextBlock _measurer = new();
    private RsvpDocument _document = new([], [], TimeSpan.Zero);
    private string _sourceText = "";
    private int _currentSlide;
    private bool _playing;
    private bool _finished;
    private bool _positioned;
    private bool _updatingProgress;
    /// <summary>Guards the control ValueChanged handlers while LoadText
    /// syncs them from Settings (same _loading pattern as the Settings
    /// pages).</summary>
    private bool _loading = true;
    /// <summary>Drag-to-resize state for the context area (Manh
    /// 2026-11-19): press-hold left-click + drag UP/DOWN grows/shrinks
    /// the Context size % (5% per 24 px of vertical drag, clamped
    /// 25-100%). Only a vertical-dominant drag resizes — a horizontal
    /// drag is text selection (select words to look them up), so it
    /// must pass through untouched. The drag latches into resize mode
    /// once it is clearly vertical; until then nothing is handled.</summary>
    private bool _contextResizing;
    private double _dragStartX;
    private double _dragStartY;
    private double _dragLastY;
    private double _dragAccumulated;

    public FastReadWindow()
    {
        InitializeComponent();
        Title = "TransPeek — Fast Read RSVP";
        _timer.Tick += (_, _) => AdvanceSlide();
        if (Content is FrameworkElement contentRoot)
            contentRoot.KeyDown += OnKeyDown;
        // Wheel over the progress slider steps one word — a Slider
        // consumes the wheel itself (seek) and marks it handled, so a
        // plain XAML handler would never fire. Attach with
        // handledEventsToo so the reader's wheel wins (Manh 2026-11-19).
        // The focus word area is a plain Grid, so its wheel handler
        // lives in XAML. The context ScrollViewer scrolls natively on
        // the wheel (no hijack — the wheel is scroll again since
        // 2026-11-19). The drag-to-resize handlers below also use
        // AddHandler with handledEventsToo, so they still see presses
        // the selectable context text handles for its own selection.
        ProgressSlider.AddHandler(UIElement.PointerWheelChangedEvent, new PointerEventHandler(OnProgressWheelChanged), true);
        ContextScroller.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnContextPointerPressed), true);
        ContextScroller.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler(OnContextPointerMoved), true);
        ContextScroller.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(OnContextPointerReleased), true);
        ContextScroller.AddHandler(UIElement.PointerCaptureLostEvent, new PointerEventHandler(OnContextPointerCaptureLost), true);
        Closed += (_, _) => SaveWindowSize();
    }

    /// <summary>Load new text and show the first slide (paused — the
    /// caller starts playback via ShowReader once visible).</summary>
    public void LoadText(string text)
    {
        _sourceText = text;
        _document = RsvpEngine.Build(text, App.Settings.RsvpWpm, App.Settings.RsvpChunkSize);
        _currentSlide = 0;
        _finished = false;
        _timer.Stop();
        _playing = false;
        PlayPauseIcon.Glyph = "\uE768";
        _loading = true;
        WpmSlider.Value = App.Settings.RsvpWpm;
        WpmText.Text = $"{App.Settings.RsvpWpm} WPM";
        ChunkCombo.SelectedIndex = Math.Clamp(App.Settings.RsvpChunkSize, 1, 6) - 1;
        SelectComboByTag(FocusSizeCombo, NearestFocusSize(App.Settings.RsvpFontSize).ToString());
        SelectComboByTag(ContextSizeCombo, NearestContextPercent(App.Settings.RsvpContextSizeRatio * 100).ToString());
        SelectComboByTag(ColorSchemeCombo, App.Settings.RsvpColorScheme);
        SelectComboByTag(ContextDisplayCombo, App.Settings.RsvpContextDisplay);
        ApplyColorScheme();
        WordTextBlock.FontSize = App.Settings.RsvpFontSize;
        _loading = false;
        ShowSlide(0);
        UpdateProgress();
        UpdateReadingTime();
    }

    public void ShowReader()
    {
        if (!_positioned)
        {
            _positioned = true;
            try
            {
                // Default 70% x 55% of the work area (Sprint Reader);
                // a saved size (remembered after resize) wins; min 880x550.
                var (cx, cy) = WindowHelpers.CursorPosition();
                var work = WindowHelpers.DisplayWorkArea(cx, cy);
                var width = (int)(work.Width * 0.70);
                var height = (int)(work.Height * 0.55);
                var savedW = (int)App.Settings.RsvpWindowWidth;
                var savedH = (int)App.Settings.RsvpWindowHeight;
                if (savedW > 0 && savedH > 0)
                {
                    width = savedW;
                    height = savedH;
                }
                width = Math.Clamp(width, Math.Min(880, work.Width), work.Width);
                height = Math.Clamp(height, Math.Min(550, work.Height), work.Height);
                var x = work.X + Math.Max(0, (work.Width - width) / 2);
                var y = work.Y + Math.Max(0, (work.Height - height) / 2);
                WindowHelpers.MoveAndResize(this, x, y, width, height);
            }
            catch
            {
                // Positioning must never stop the window from opening.
            }
        }
        // Bring it to the front as the focused window (same pattern as
        // the Settings window: restore first, then force it forward).
        var hwnd = WindowNative.GetWindowHandle(this);
        if (NativeMethods.IsIconic(hwnd))
            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
        NativeMethods.SetForegroundWindow(hwnd);
        Activate();
        if (_document.Slides.Count > 0)
            SetPlaying(true);
    }

    private void ShowSlide(int index)
    {
        if (_document.Slides.Count == 0)
        {
            LeftRun.Text = "";
            PivotRun.Text = "";
            RightRun.Text = "";
            WordTextBlock.RenderTransform = null;
            return;
        }
        _currentSlide = Math.Clamp(index, 0, _document.Slides.Count - 1);
        var slide = _document.Slides[_currentSlide];
        if (slide.PivotCharIndex >= 0 && slide.PivotCharIndex < slide.Text.Length)
        {
            LeftRun.Text = slide.Text[..slide.PivotCharIndex];
            PivotRun.Text = slide.Text.Substring(slide.PivotCharIndex, 1);
            RightRun.Text = slide.Text[(slide.PivotCharIndex + 1)..];
            AlignPivot(slide.Text, slide.PivotCharIndex);
        }
        else
        {
            // Multi-word slide: no single pivot, center the whole text.
            LeftRun.Text = slide.Text;
            PivotRun.Text = "";
            RightRun.Text = "";
            WordTextBlock.RenderTransform = null;
        }
        UpdateProgress();
        UpdateContext();
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
        // The StackPanel centers the word; shift it so the pivot char's
        // center (left + half the pivot) lands at the word's center.
        var shift = wordWidth / 2 - (leftWidth + pivotWidth / 2);
        WordTextBlock.RenderTransform = new TranslateTransform { X = shift };
    }

    private void AdvanceSlide()
    {
        if (_currentSlide + 1 >= _document.Slides.Count)
        {
            _finished = true;
            SetPlaying(false);
            UpdateProgress();
            return;
        }
        ShowSlide(_currentSlide + 1);
        if (_playing)
            _timer.Interval = _document.Slides[_currentSlide].Delay;
    }

    private void SeekTo(int index)
    {
        if (_document.Slides.Count == 0)
            return;
        _finished = false;
        ShowSlide(index);
        if (_playing)
        {
            _timer.Stop();
            _timer.Interval = _document.Slides[_currentSlide].Delay;
            _timer.Start();
        }
        UpdateProgress();
    }

    private void SetPlaying(bool playing)
    {
        if (_document.Slides.Count == 0)
            playing = false;
        _playing = playing;
        PlayPauseIcon.Glyph = playing ? "\uE769" : "\uE768";
        if (playing)
        {
            if (_finished)
            {
                _finished = false;
                _currentSlide = 0;
                ShowSlide(0);
            }
            _timer.Interval = _document.Slides[_currentSlide].Delay;
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
        UpdateContext();
    }

    /// <summary>Rebuild the document (WPM/chunk changed) keeping the
    /// reading position: the slide containing the current word.</summary>
    private void RebuildKeepingPosition()
    {
        if (_document.Slides.Count == 0 || string.IsNullOrEmpty(_sourceText))
            return;
        var wordIndex = _document.Slides[_currentSlide].StartWordIndex;
        _document = RsvpEngine.Build(_sourceText, App.Settings.RsvpWpm, App.Settings.RsvpChunkSize);
        _currentSlide = RsvpEngine.SlideIndexForWord(_document, wordIndex);
        _finished = false;
        ShowSlide(_currentSlide);
        if (_playing)
        {
            _timer.Stop();
            _timer.Interval = _document.Slides[_currentSlide].Delay;
            _timer.Start();
        }
        UpdateReadingTime();
    }

    private void UpdateProgress()
    {
        var total = _document.TotalSlides;
        if (total == 0)
        {
            StatusText.Text = "No text to read.";
            return;
        }
        _updatingProgress = true;
        ProgressSlider.Minimum = 0;
        ProgressSlider.Maximum = total - 1;
        ProgressSlider.Value = _currentSlide;
        _updatingProgress = false;
        var remaining = _document.RemainingDuration(_currentSlide);
        StatusText.Text = _finished
            ? $"Finished — {_document.TotalWords} words in {FormatDuration(_document.TotalDuration)}"
            : $"Slide {_currentSlide + 1} / {total} • {FormatDuration(remaining)} left • {_document.TotalWords} words";
    }

    private static string FormatDuration(TimeSpan d) => $"{(int)d.TotalMinutes}:{d.Seconds:00}";

    private void UpdateContext()
    {
        // When the context shows depends on the Settings popover
        // (mac round 7, SwiftRead's contextDisplayStyle): Always =
        // even while playing; Paused = only while paused (default);
        // Never = never. An unknown value behaves like Paused.
        var display = App.Settings.RsvpContextDisplay;
        var show = _document.Slides.Count > 0
            && (string.Equals(display, "Always", StringComparison.OrdinalIgnoreCase)
                || (!_playing && !string.Equals(display, "Never", StringComparison.OrdinalIgnoreCase)));
        if (!show)
        {
            ContextScroller.Visibility = Visibility.Collapsed;
            return;
        }
        // Context size = focus size x the "Context size" percent
        // (mac round 7, default 40%); line height follows at 1.5x.
        var contextSize = WordTextBlock.FontSize * App.Settings.RsvpContextSizeRatio;
        ContextTextBlock.FontSize = contextSize;
        ContextTextBlock.LineHeight = contextSize * 1.5;
        var context = _document.Context(_currentSlide);
        ContextTextBlock.Inlines.Clear();
        var before = string.Join(" ", context.Words.Take(context.CurrentStart));
        var current = string.Join(" ", context.Words.Skip(context.CurrentStart).Take(context.CurrentCount));
        var after = string.Join(" ", context.Words.Skip(context.CurrentStart + context.CurrentCount));
        if (before.Length > 0)
            ContextTextBlock.Inlines.Add(new Run { Text = before + " " });
        ContextTextBlock.Inlines.Add(new Run
        {
            Text = current,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xF6, 0x86, 0x1F)),
        });
        if (after.Length > 0)
            ContextTextBlock.Inlines.Add(new Run { Text = " " + after });
        ContextScroller.Visibility = Visibility.Visible;
    }

    private void SaveWindowSize()
    {
        try
        {
            var size = AppWindow.Size;
            if (size.Width > 0 && size.Height > 0)
            {
                App.Settings.RsvpWindowWidth = size.Width;
                App.Settings.RsvpWindowHeight = size.Height;
                SettingsStore.Save(App.Settings);
            }
        }
        catch
        {
            // Best effort: a lost size just reopens at the default.
        }
    }

    private void OnPlayPauseClicked(object sender, RoutedEventArgs e) => SetPlaying(!_playing);

    private void OnRestartClicked(object sender, RoutedEventArgs e) => SeekTo(0);

    private void OnProgressChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_loading || _updatingProgress)
            return;
        SeekTo((int)Math.Round(e.NewValue));
    }

    private void OnWpmChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_loading)
            return;
        var wpm = (int)Math.Round(e.NewValue);
        WpmText.Text = $"{wpm} WPM";
        App.Settings.RsvpWpm = wpm;
        SettingsStore.Save(App.Settings);
        RebuildKeepingPosition();
    }

    private void OnChunkChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading)
            return;
        if (ChunkCombo.SelectedItem is ComboBoxItem item
            && item.Tag is string tag && int.TryParse(tag, out var chunk))
        {
            App.Settings.RsvpChunkSize = chunk;
            SettingsStore.Save(App.Settings);
            RebuildKeepingPosition();
        }
    }

    /// <summary>Focus size picker (Manh 2026-11-19, mac round 7
    /// options 40-450): apply the size, remember it, re-render the
    /// current slide (the pivot shift depends on the size) — the
    /// context font follows the focus size in UpdateContext.</summary>
    private void OnFocusSizeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading)
            return;
        if (FocusSizeCombo.SelectedItem is ComboBoxItem item
            && item.Tag is string tag && double.TryParse(tag, out var size))
            ApplyFocusSize(size);
    }

    private void ApplyFocusSize(double size)
    {
        WordTextBlock.FontSize = size;
        App.Settings.RsvpFontSize = size;
        SettingsStore.Save(App.Settings);
        ShowSlide(_currentSlide);
    }

    /// <summary>"Context size" picker (Manh 2026-11-19, mac round 7
    /// options 25-100%): the context font is the focus size times
    /// this percent — no rebuild, just re-render the context.</summary>
    private void OnContextSizeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading)
            return;
        if (ContextSizeCombo.SelectedItem is ComboBoxItem item
            && item.Tag is string tag && int.TryParse(tag, out var percent))
        {
            App.Settings.RsvpContextSizeRatio = percent / 100.0;
            SettingsStore.Save(App.Settings);
            UpdateContext();
        }
    }

    private void OnColorSchemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading)
            return;
        if (ColorSchemeCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            App.Settings.RsvpColorScheme = tag;
            SettingsStore.Save(App.Settings);
            ApplyColorScheme();
        }
    }

    private void OnContextDisplayChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading)
            return;
        if (ContextDisplayCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            App.Settings.RsvpContextDisplay = tag;
            SettingsStore.Save(App.Settings);
            UpdateContext();
        }
    }

    /// <summary>Reset to default (Manh 2026-11-19, mac round 7):
    /// speed 275, focus size 110, context size 40%, context display
    /// "Only show when paused". The color scheme is a theme
    /// preference, so it is NOT reset.</summary>
    private void OnResetClicked(object sender, RoutedEventArgs e)
    {
        _loading = true;
        App.Settings.RsvpWpm = 275;
        App.Settings.RsvpFontSize = 110;
        App.Settings.RsvpContextSizeRatio = 0.40;
        App.Settings.RsvpContextDisplay = "Paused";
        SettingsStore.Save(App.Settings);
        WpmSlider.Value = 275;
        WpmText.Text = "275 WPM";
        SelectComboByTag(FocusSizeCombo, "110");
        SelectComboByTag(ContextSizeCombo, "40");
        SelectComboByTag(ContextDisplayCombo, "Paused");
        _loading = false;
        RebuildKeepingPosition();
        ShowSlide(_currentSlide);
    }

    private void OnRewindClicked(object sender, RoutedEventArgs e) => SeekTo(_currentSlide - 1);

    private void OnFastForwardClicked(object sender, RoutedEventArgs e) => SeekTo(_currentSlide + 1);

    /// <summary>Maximize / restore the reader window (Manh 2026-11-19,
    /// like mac's toggle-maximize button).</summary>
    private void OnMaximizeClicked(object sender, RoutedEventArgs e)
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            if (presenter.State == OverlappedPresenterState.Maximized)
                presenter.Restore();
            else
                presenter.Maximize();
        }
    }

    /// <summary>Wheel over the progress slider (Manh 2026-11-19):
    /// scroll down = next word, scroll up = previous word — one slide
    /// per tick, like a fast seek.</summary>
    private void OnProgressWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var delta = e.GetCurrentPoint((UIElement)sender).Properties.MouseWheelDelta;
        if (delta == 0)
            return;
        SeekTo(_currentSlide + (delta < 0 ? 1 : -1));
        e.Handled = true;
    }

    /// <summary>Wheel over the focus word (Manh 2026-11-19): scroll up
    /// grows the focus size, scroll down shrinks it, 2pt per tick
    /// (the old slider step), clamped 40-450. The picker shows the
    /// nearest option — the value itself stays exact, like mac.
    /// RESTORED 2026-11-19 from the 191 behaviour: 192 had forwarded
    /// this wheel to scroll the context instead, but Manh liked the
    /// 191 focus-size wheel and asked for it back. To scroll the
    /// context, hover the context box itself (its wheel scrolls
    /// natively).</summary>
    private void OnFocusWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var delta = e.GetCurrentPoint((UIElement)sender).Properties.MouseWheelDelta;
        if (delta == 0)
            return;
        var size = Math.Clamp(WordTextBlock.FontSize + (delta > 0 ? 2 : -2), 40, 450);
        ApplyFocusSize(size);
        _loading = true;
        SelectComboByTag(FocusSizeCombo, NearestFocusSize(size).ToString());
        _loading = false;
        e.Handled = true;
    }

    /// <summary>Drag-to-resize, press: record the start, decide
    /// nothing yet — a horizontal drag is text selection and must not
    /// be disturbed. Only a press on the context text itself starts
    /// a possible resize (a press on the scrollbar is a scroll, not
    /// a resize). Do NOT mark handled.</summary>
    private void OnContextPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.OriginalSource is not TextBlock)
            return;
        if (!e.GetCurrentPoint(ContextScroller).Properties.IsLeftButtonPressed)
            return;
        var point = e.GetCurrentPoint(ContextScroller).Position;
        _dragStartX = point.X;
        _dragStartY = point.Y;
        _dragLastY = point.Y;
        _dragAccumulated = 0;
        _contextResizing = false;
    }

    /// <summary>Drag-to-resize, move: latch into resize mode only on
    /// a clearly vertical drag (at least 12 px up/down and more
    /// vertical than horizontal). Anything else (a click, a horizontal
    /// selection drag) passes through untouched. Once resizing, one 5%
    /// step per 24 px of vertical drag, accumulated across moves so a
    /// slow drag still steps smoothly. Up grows, down shrinks — the
    /// same direction as the old wheel.</summary>
    private void OnContextPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!e.GetCurrentPoint(ContextScroller).Properties.IsLeftButtonPressed)
            return;
        var point = e.GetCurrentPoint(ContextScroller).Position;
        if (!_contextResizing)
        {
            var dx = point.X - _dragStartX;
            var dy = point.Y - _dragStartY;
            if (Math.Abs(dy) < 12 || Math.Abs(dy) <= Math.Abs(dx))
                return;
            _contextResizing = true;
            // Turn selection off while resizing, so the drag stops
            // extending the selection. Restored on release.
            ContextTextBlock.IsTextSelectionEnabled = false;
        }
        _dragAccumulated += point.Y - _dragLastY;
        _dragLastY = point.Y;
        var ratio = App.Settings.RsvpContextSizeRatio;
        while (_dragAccumulated <= -24)
        {
            ratio += 0.05;
            _dragAccumulated += 24;
        }
        while (_dragAccumulated >= 24)
        {
            ratio -= 0.05;
            _dragAccumulated -= 24;
        }
        ratio = Math.Clamp(ratio, 0.25, 1.00);
        if (Math.Abs(ratio - App.Settings.RsvpContextSizeRatio) < 0.001)
            return;
        App.Settings.RsvpContextSizeRatio = ratio;
        _loading = true;
        SelectComboByTag(ContextSizeCombo, NearestContextPercent(ratio * 100).ToString());
        _loading = false;
        UpdateContext();
        e.Handled = true;
    }

    private void OnContextPointerReleased(object sender, PointerRoutedEventArgs e)
        => FinishContextResize();

    private void OnContextPointerCaptureLost(object sender, PointerRoutedEventArgs e)
        => FinishContextResize();

    /// <summary>End a context resize drag (release, cancel, or lost
    /// capture): restore text selection and save the size. Idempotent
    /// — release and capture-lost can both fire for one drag.</summary>
    private void FinishContextResize()
    {
        if (!_contextResizing)
            return;
        _contextResizing = false;
        ContextTextBlock.IsTextSelectionEnabled = true;
        SettingsStore.Save(App.Settings);
    }

    /// <summary>Color scheme (mac round 7): White on Black = dark
    /// theme (black page, light controls) via RequestedTheme + a black
    /// page background; Black on White = the default light theme. The
    /// Flyout contents get the same theme — a Flyout is a separate
    /// popup, so it does not inherit the Grid's theme.</summary>
    private void ApplyColorScheme()
    {
        var dark = string.Equals(App.Settings.RsvpColorScheme, "Dark", StringComparison.OrdinalIgnoreCase);
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

    /// <summary>Total estimated reading time (mac: "Reading time:
    /// 2mins", under a minute -> "0min"). Top-left inside the block
    /// text (Manh 2026-11-19).</summary>
    private void UpdateReadingTime()
    {
        var mins = (int)Math.Round(_document.TotalDuration.TotalMinutes);
        ReadingTimeText.Text = mins <= 0 ? "Reading time: 0min" : $"Reading time: {mins}mins";
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

    /// <summary>Focus size picker options (mac round 7: 40-450, steps
    /// grow past 110 so the menu stays short). The picker shows the
    /// option nearest to a value — the wheel moves the size by 2pt,
    /// so the value is not always one of the options.</summary>
    private static readonly int[] FocusSizeOptions =
        [40, 48, 56, 64, 72, 80, 88, 96, 104, 110, 120, 140, 160, 200, 250, 300, 350, 400, 450];

    private static int NearestFocusSize(double size)
        => FocusSizeOptions.MinBy(option => Math.Abs(option - size));

    /// <summary>Context size picker options (mac round 7: 25-100%),
    /// same nearest-option rule as the focus size.</summary>
    private static readonly int[] ContextSizeOptions = [25, 30, 35, 40, 50, 60, 75, 100];

    private static int NearestContextPercent(double percent)
        => ContextSizeOptions.MinBy(option => Math.Abs(option - percent));

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var focused = FocusManager.GetFocusedElement();
        switch (e.Key)
        {
            case VirtualKey.Space:
                // A focused button handles Space itself (Play toggles,
                // Restart restarts) — only toggle here when no button
                // has focus.
                if (focused is Button)
                    return;
                SetPlaying(!_playing);
                e.Handled = true;
                break;
            case VirtualKey.Left:
                if (focused is Slider)
                    return; // the progress slider seeks by itself.
                SeekTo(_currentSlide - 1);
                e.Handled = true;
                break;
            case VirtualKey.Right:
                if (focused is Slider)
                    return;
                SeekTo(_currentSlide + 1);
                e.Handled = true;
                break;
            case VirtualKey.Up:
                if (focused is Slider)
                    return;
                WpmSlider.Value = Math.Clamp(WpmSlider.Value + 10, WpmSlider.Minimum, WpmSlider.Maximum);
                e.Handled = true;
                break;
            case VirtualKey.Down:
                if (focused is Slider)
                    return;
                WpmSlider.Value = Math.Clamp(WpmSlider.Value - 10, WpmSlider.Minimum, WpmSlider.Maximum);
                e.Handled = true;
                break;
            case VirtualKey.R:
                SeekTo(0);
                e.Handled = true;
                break;
            case VirtualKey.Escape:
            case VirtualKey.Q:
                Close();
                e.Handled = true;
                break;
        }
    }
}
