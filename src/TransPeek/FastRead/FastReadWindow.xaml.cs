// Fast Read (RSVP) reader window — feature/fast-read-rsvp.
// A separate window (not a panel tab): RSVP needs a big word, one
// fixed focal point, a progress bar and a pause context. The engine
// (TransPeek.Core.FastRead) is pure logic; this window only renders slides
// and drives the timer. Single instance owned by App: re-opening
// focuses the existing window and loads the new text. READ ONLY —
// no TTS in the reader (listening is the float icon's job).

using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
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

    public FastReadWindow()
    {
        InitializeComponent();
        Title = "Fast Read — TransPeek";
        _timer.Tick += (_, _) => AdvanceSlide();
        if (Content is FrameworkElement contentRoot)
            contentRoot.KeyDown += OnKeyDown;
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
        FontSlider.Value = App.Settings.RsvpFontSize;
        FontText.Text = $"{App.Settings.RsvpFontSize:0} pt";
        WordTextBlock.FontSize = App.Settings.RsvpFontSize;
        _loading = false;
        ShowSlide(0);
        UpdateProgress();
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
        if (_playing || _document.Slides.Count == 0)
        {
            ContextScroller.Visibility = Visibility.Collapsed;
            return;
        }
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

    private void OnFontSizeChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_loading)
            return;
        var size = e.NewValue;
        FontText.Text = $"{size:0} pt";
        WordTextBlock.FontSize = size;
        App.Settings.RsvpFontSize = size;
        SettingsStore.Save(App.Settings);
        // Re-render the current slide: the pivot shift depends on the size.
        ShowSlide(_currentSlide);
    }

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
