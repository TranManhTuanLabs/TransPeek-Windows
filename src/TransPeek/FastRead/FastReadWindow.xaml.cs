// Fast Read (RSVP) reader window — feature/fast-read-rsvp.
// A separate window, not a panel tab: RSVP needs a big word
// (40-450 pt), one fixed focal point, a progress bar and a pause
// context — none of that fits the 420 px translate panel.
// UX copies SwiftRead's reader (rsvp-report.md §6.1) + the mac
// reader window (RSVPView.swift, round 7): one word at a time at
// a fixed ORP pivot (middle char, orange #F6861F), context
// +/-150 words on pause, WPM 275 default, chunk 1-6.
// READ ONLY — no TTS in the reader (listening is the float
// icon's job).
//
// Playback lives in RsvpReaderViewModel (feature/focus-timer-decor),
// shared with the panel-right lite reader — this window only renders
// the slide and the pause context and syncs its controls from the
// view model's PropertyChanged. Single instance owned by App:
// re-opening focuses the existing window and loads the new text.

using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using TransPeek.Core.Settings;
using TransPeek.Interop;
using TransPeek.Services;
using TransPeek.Share;
using Windows.Foundation;
using Windows.System;
using WinRT.Interop;

namespace TransPeek.FastRead;

public sealed partial class FastReadWindow : Window
{
    private readonly RsvpReaderViewModel _viewModel = new();
    // TTS for the speaker button (feature/fastread-speaker-style2):
    // this window owns its own service — the float icon has its own
    // (_iconSpeech in App), so a read speaking never fights the icon
    // over one MediaPlayer. Wired into the view model; disposed when
    // the window closes.
    private readonly SystemSpeechService _speech = new();
    // Voice panel hover state (mac parity: the picker stays open while
    // the mouse is on the speaker button or on the panel itself, and
    // hides 300 ms after leaving both).
    private readonly DispatcherTimer _voicePanelHideTimer = new();
    private bool _hoveringSpeaker;
    private bool _hoveringVoicePanel;
    // A voice picker dropdown is open (the dropdown is a Popup — the
    // pointer leaves the panel for it, so hover alone would hide the
    // panel mid-pick). While a dropdown is open the panel stays put.
    private bool _voiceDropdownOpen;
    /// <summary>Detached TextBlock for measuring word widths (same font
    /// as the displayed word): the ORP pivot is aligned by shifting
    /// the centered word so the pivot char's center lands exactly at
    /// the word area's center.</summary>
    private readonly TextBlock _measurer = new();
    private bool _positioned;
    private bool _updatingProgress;
    /// <summary>Guards the control ValueChanged handlers while LoadText
    /// syncs them from the view model (same _loading pattern as the Settings
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
        // Version right after Reading time, same format as the panel
        // footer (Manh 2026-11-19): "0.1.4 (build 192)", or just the
        // version when there is no CI build number.
        VersionText.Text = AppInfo.CiBuildNumber > 0
            ? $"{AppInfo.Version} (build {AppInfo.CiBuildNumber})"
            : AppInfo.Version;
        _viewModel.PropertyChanged += OnViewModelChanged;
        _viewModel.Speaker = _speech;
        Style2View.ViewModel = _viewModel;
        PopulateVoices(EnglishVoiceCombo, "en");
        PopulateVoices(VietnameseVoiceCombo, "vi");
        _voicePanelHideTimer.Interval = TimeSpan.FromMilliseconds(300);
        _voicePanelHideTimer.Tick += (_, _) =>
        {
            _voicePanelHideTimer.Stop();
            if (!_hoveringSpeaker && !_hoveringVoicePanel && !_voiceDropdownOpen)
                VoicePanel.Visibility = Visibility.Collapsed;
        };
        // PreviewKeyDown (tunnel), NOT KeyDown (bubble): Space must be
        // caught before a focused button sees it — KeyDown reaches the
        // parent only after the button already handled Space (the repo
        // link got activated this way in the panel lite reader).
        if (Content is FrameworkElement contentRoot)
            contentRoot.PreviewKeyDown += OnKeyDown;
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
        Closed += (_, _) =>
        {
            SaveWindowSize();
            _speech.Dispose();
        };
    }

    /// <summary>Load new text and show the first slide (paused — the
    /// caller starts playback via ShowReader once visible). The view
    /// model reads the reading options from Settings; the controls
    /// sync from its PropertyChanged (SyncControls below).</summary>
    public void LoadText(string text)
    {
        _loading = true;
        _viewModel.LoadText(text);
        _loading = false;
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
        if (_viewModel.HasSlides)
            _viewModel.SetPlaying(true);
    }

    /// <summary>View-model changes drive the rendering: the slide (and
    /// the focus size, which the pivot shift depends on), the play
    /// icon, the progress bar + status, the pause context, the reading
    /// time, and the control sync. The view model setters early-return
    /// on equal values, so a control sync feeding back into a setter
    /// (slider Value -&gt; OnWpmChanged -&gt; SetWpm) is a no-op.</summary>
    private void OnViewModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(RsvpReaderViewModel.CurrentSlide):
            case nameof(RsvpReaderViewModel.FontSize):
                RenderSlide();
                break;
            case nameof(RsvpReaderViewModel.IsPlaying):
                PlayPauseIcon.Glyph = _viewModel.IsPlaying ? "\uE769" : "\uE768";
                break;
            case nameof(RsvpReaderViewModel.IsFinished):
                UpdateCompletion();
                break;
            case nameof(RsvpReaderViewModel.StatusText):
            case nameof(RsvpReaderViewModel.CurrentSlideIndex):
            case nameof(RsvpReaderViewModel.SlideCount):
            case nameof(RsvpReaderViewModel.Style2CurrentLineIndex):
            case nameof(RsvpReaderViewModel.Style2LineCount):
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
            case nameof(RsvpReaderViewModel.HighlightCurrentLine):
                SyncControls();
                break;
            case nameof(RsvpReaderViewModel.ContextStyle):
                // The style changes how the context renders (book page
                // vs scroll paragraph) and which control shows it.
                RenderContext();
                SyncControls();
                break;
            case nameof(RsvpReaderViewModel.ReadingStyle):
                // The reading style swaps the reading surface (RSVP
                // grid vs Style 2 page) and the progress units.
                SwitchReadingSurface();
                SyncControls();
                SyncProgress();
                break;
            case nameof(RsvpReaderViewModel.IsSoundOn):
                SyncSpeakerIcon();
                break;
        }
    }

    /// <summary>Swap the reading surface by style: Style 2 hides the
    /// RSVP grid (the Style2 view shows itself) and the pause context
    /// (Style 2 has no pause context — the page IS the context);
    /// Style 1 hides the Style 2 page (it hides itself) and shows the
    /// grid again.</summary>
    private void SwitchReadingSurface()
    {
        var style2 = !_viewModel.IsRsvpStyle;
        ReadingGrid.Visibility = style2 ? Visibility.Collapsed : Visibility.Visible;
        // Hint follows the style: Style 1 zooms the focus size with
        // the wheel, Style 2 scrolls the page with it (font size is
        // click-drag there).
        TopHintTextBlock.Text = style2
            ? "Space play/pause • ←/→ step • ↑/↓ speed • Wheel scroll • Click-drag font size"
            : "Space play/pause • ←/→ step • ↑/↓ speed • Wheel ↑/↓ focus size";
        if (style2)
        {
            ContextScroller.Visibility = Visibility.Collapsed;
            BeforeTextBlock.Visibility = Visibility.Collapsed;
            AfterTextBlock.Visibility = Visibility.Collapsed;
        }
        else
        {
            RenderContext();
        }
    }

    /// <summary>Speaker icon by sound state (mac: speaker.wave.2.fill
    /// vs speaker.slash.fill): Volume E767 = sound on, Mute E74F =
    /// sound off (both verified on Microsoft Learn, Segoe Fluent
    /// Icons).</summary>
    private void SyncSpeakerIcon()
    {
        SpeakerIcon.Glyph = _viewModel.IsSoundOn ? "\uE767" : "\uE74F";
        ToolTipService.SetToolTip(SpeakerButton, _viewModel.IsSoundOn
            ? "Sound on — click to mute the reading"
            : "Sound off — click to hear the reading");
    }

    /// <summary>Voice picker lists (same source as Settings → TTS, so
    /// the two lists can never drift): "System default" first, then
    /// the installed voices for the language. The Tag is the voice Id,
    /// saved to the same settings keys Settings → TTS uses.</summary>
    private void PopulateVoices(ComboBox combo, string languagePrefix)
    {
        combo.Items.Clear();
        combo.Items.Add(new ComboBoxItem { Content = "System default", Tag = "" });
        foreach (var voice in SystemSpeechService.InstalledVoices)
        {
            if (!voice.Language.StartsWith(languagePrefix + "-", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(voice.Language, languagePrefix, StringComparison.OrdinalIgnoreCase))
                continue;
            combo.Items.Add(new ComboBoxItem { Content = voice.DisplayName, Tag = voice.Id });
        }
    }

    /// <summary>Completion (mac §14.5): when a full read finishes, show
    /// the congratulations line + the share row above the progress bar.
    /// The share buttons share the real stats of the finished read.</summary>
    private void UpdateCompletion()
    {
        if (_viewModel.IsFinished && _viewModel.CompletionText is { } completion)
        {
            CompletionText.Text = completion;
            ShareRowBuilder.Populate(CompletionShareRowPanel, _viewModel.ShareText);
            CompletionPanel.Visibility = Visibility.Visible;
        }
        else
        {
            CompletionPanel.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>Sync the bottom-bar / popover controls from the view
    /// model (the window's half of LoadText used to do this inline).
    /// The _loading guard stops the control events from calling back
    /// into the view model while syncing.</summary>
    private void SyncControls()
    {
        _loading = true;
        WpmSlider.Value = _viewModel.Wpm;
        WpmText.Text = $"{_viewModel.Wpm} WPM";
        ChunkCombo.SelectedIndex = Math.Clamp(_viewModel.ChunkSize, 1, 6) - 1;
        SelectComboByTag(FocusSizeCombo, NearestFocusSize(_viewModel.FontSize).ToString());
        SelectComboByTag(ContextSizeCombo, NearestContextPercent(_viewModel.ContextSizeRatio * 100).ToString());
        SelectComboByTag(ColorSchemeCombo, _viewModel.ColorScheme);
        SelectComboByTag(ContextDisplayCombo, _viewModel.ContextDisplay);
        SelectComboByTag(ReadingStyleCombo, _viewModel.ReadingStyle);
        HighlightToggle.IsOn = _viewModel.HighlightCurrentLine;
        SelectComboByTag(EnglishVoiceCombo, App.Settings.EnglishVoiceId ?? "");
        SelectComboByTag(VietnameseVoiceCombo, App.Settings.VietnameseVoiceId ?? "");
        SpeedSlider.Value = Math.Clamp(App.Settings.SpeechRate, SpeedSlider.Minimum, SpeedSlider.Maximum);
        SpeedValueText.Text = $"{SpeedSlider.Value:0.0}x";
        SyncSpeakerIcon();
        ApplyColorScheme();
        _loading = false;
    }

    /// <summary>Render the current slide: the focus word split at its
    /// ORP pivot (left / pivot / right runs) and shifted so the pivot
    /// char sits exactly at the word area's center.</summary>
    private void RenderSlide()
    {
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
            // No pivot (should not happen — every slide has one since
            // 2026-11-19, SwiftRead-style): center the whole text.
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
        // The StackPanel centers the word; shift it so the pivot char's
        // center (left + half the pivot) lands at the word's center.
        var shift = wordWidth / 2 - (leftWidth + pivotWidth / 2);
        WordTextBlock.RenderTransform = new TranslateTransform { X = shift };
    }

    private void SyncProgress()
    {
        // Style 2 counts lines, not slides (the slider seeks lines).
        var total = _viewModel.PositionCount;
        var index = _viewModel.CurrentPositionIndex;
        _updatingProgress = true;
        ProgressSlider.Minimum = 0;
        ProgressSlider.Maximum = Math.Max(0, total - 1);
        ProgressSlider.Value = index;
        _updatingProgress = false;
        StatusText.Text = _viewModel.StatusText;
    }

    /// <summary>Render the pause context (+/-150 words around the
    /// current slide). Two styles: Style 1 (FastRead Lite book page,
    /// default) puts the before-words above the focus line and the
    /// after-words below it, the focus line pinned at the vertical
    /// center; Style 2 (the window's own theme) shows one scrollable
    /// paragraph below the focus line, the current words bold + orange
    /// inline. Size = focus size x the "Context size" percent, line
    /// height 1.5x — same in both styles.</summary>
    private void RenderContext()
    {
        var context = _viewModel.PauseContext;
        if (!_viewModel.ShowContext || context is null)
        {
            ContextScroller.Visibility = Visibility.Collapsed;
            BeforeTextBlock.Visibility = Visibility.Collapsed;
            AfterTextBlock.Visibility = Visibility.Collapsed;
            return;
        }
        var contextSize = _viewModel.ContextFontSize;
        if (string.Equals(_viewModel.ContextStyle, "Style2", StringComparison.OrdinalIgnoreCase))
        {
            // Style 2: the scroll paragraph. The book-page texts hide;
            // the drag-to-resize handlers live on the scroller, so they
            // only fire here.
            BeforeTextBlock.Visibility = Visibility.Collapsed;
            AfterTextBlock.Visibility = Visibility.Collapsed;
            ContextTextBlock.FontSize = contextSize;
            ContextTextBlock.LineHeight = contextSize * 1.5;
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
            return;
        }
        // Style 1: the book page (mac: bookPage). The current words
        // are the focus line itself, so they are not repeated inline.
        ContextScroller.Visibility = Visibility.Collapsed;
        BeforeTextBlock.FontSize = contextSize;
        BeforeTextBlock.LineHeight = contextSize * 1.5;
        AfterTextBlock.FontSize = contextSize;
        AfterTextBlock.LineHeight = contextSize * 1.5;
        BeforeTextBlock.Text = string.Join(" ", context.Words.Take(context.CurrentStart));
        AfterTextBlock.Text = string.Join(" ", context.Words.Skip(context.CurrentStart + context.CurrentCount));
        BeforeTextBlock.Visibility = Visibility.Visible;
        AfterTextBlock.Visibility = Visibility.Visible;
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

    private void OnPlayPauseClicked(object sender, RoutedEventArgs e) => _viewModel.SetPlaying(!_viewModel.IsPlaying);

    /// <summary>Speaker click: toggle the sound for this read. The
    /// voice panel stays open (it is an in-tree overlay, not a popup)
    /// — clicking the speaker never dismisses it first (mac CI314
    /// bug 2: the popover swallowed the first click).</summary>
    private void OnSpeakerClicked(object sender, RoutedEventArgs e)
        => _viewModel.SetSoundOn(!_viewModel.IsSoundOn);

    /// <summary>Keep the voice panel open while the mouse is on the
    /// speaker button or on the panel itself; close it 300 ms after
    /// leaving both, so moving from the button into the panel does
    /// not flicker it shut mid-move (mac parity).</summary>
    private void OnSpeakerPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _hoveringSpeaker = true;
        UpdateVoicePanelVisibility();
    }

    private void OnSpeakerPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _hoveringSpeaker = false;
        UpdateVoicePanelVisibility();
    }

    private void OnVoicePanelPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _hoveringVoicePanel = true;
        UpdateVoicePanelVisibility();
    }

    private void OnVoicePanelPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _hoveringVoicePanel = false;
        UpdateVoicePanelVisibility();
    }

    private void UpdateVoicePanelVisibility()
    {
        _voicePanelHideTimer.Stop();
        if (_hoveringSpeaker || _hoveringVoicePanel || _voiceDropdownOpen)
        {
            VoicePanel.Visibility = Visibility.Visible;
        }
        else
        {
            _voicePanelHideTimer.Start();
        }
    }

    /// <summary>A voice picker dropdown opened / closed: the dropdown
    /// is a Popup, so picking a voice would otherwise read as "the
    /// mouse left the panel" and hide it mid-pick (Manh 2026-11-21).
    /// While a dropdown is open the panel stays put; the 300 ms hide
    /// only starts after it closes and the mouse has left.</summary>
    private void OnVoiceDropDownOpened(object sender, object e)
    {
        _voiceDropdownOpen = true;
        UpdateVoicePanelVisibility();
    }

    private void OnVoiceDropDownClosed(object sender, object e)
    {
        _voiceDropdownOpen = false;
        UpdateVoicePanelVisibility();
    }

    private void OnEnglishVoiceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading)
            return;
        if (EnglishVoiceCombo.SelectedItem is ComboBoxItem item && item.Tag is string id)
        {
            App.Settings.EnglishVoiceId = string.IsNullOrEmpty(id) ? null : id;
            SettingsStore.Save(App.Settings);
        }
    }

    private void OnVietnameseVoiceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading)
            return;
        if (VietnameseVoiceCombo.SelectedItem is ComboBoxItem item && item.Tag is string id)
        {
            App.Settings.VietnameseVoiceId = string.IsNullOrEmpty(id) ? null : id;
            SettingsStore.Save(App.Settings);
        }
    }

    /// <summary>Voice speed (mac: 0.6x–4x in the voice panel): the
    /// real voice-speed multiplier, shared with Settings → TTS
    /// (SpeechRate). It takes effect from the next phrase — no
    /// restart needed, because SpeakAsync reads the setting fresh
    /// every time. WPM is untouched: it only paces silent reading.</summary>
    private void OnSpeedChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        SpeedValueText.Text = $"{e.NewValue:0.0}x";
        if (_loading)
            return;
        App.Settings.SpeechRate = e.NewValue;
        SettingsStore.Save(App.Settings);
    }

    private void OnReadingStyleChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading)
            return;
        if (ReadingStyleCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            _viewModel.SetReadingStyle(tag);
    }

    private void OnHighlightToggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
            return;
        _viewModel.SetHighlightCurrentLine(HighlightToggle.IsOn);
    }

    private void OnRestartClicked(object sender, RoutedEventArgs e) => _viewModel.Restart();

    private void OnProgressChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_loading || _updatingProgress)
            return;
        _viewModel.SeekTo((int)Math.Round(e.NewValue));
    }

    private void OnWpmChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_loading)
            return;
        _viewModel.SetWpm((int)Math.Round(e.NewValue));
    }

    private void OnChunkChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading)
            return;
        if (ChunkCombo.SelectedItem is ComboBoxItem item
            && item.Tag is string tag && int.TryParse(tag, out var chunk))
            _viewModel.SetChunkSize(chunk);
    }

    /// <summary>Focus size picker (Manh 2026-11-19, mac round 7
    /// options 40-450): apply the size, remember it, re-render the
    /// current slide (the pivot shift depends on the size) — the
    /// context font follows the focus size in RenderContext.</summary>
    private void OnFocusSizeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading)
            return;
        if (FocusSizeCombo.SelectedItem is ComboBoxItem item
            && item.Tag is string tag && double.TryParse(tag, out var size))
            _viewModel.ApplyFocusSize(size);
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
            _viewModel.SetContextSizeRatio(percent / 100.0);
    }

    private void OnColorSchemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading)
            return;
        if (ColorSchemeCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            _viewModel.SetColorScheme(tag);
    }

    private void OnContextDisplayChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading)
            return;
        if (ContextDisplayCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            _viewModel.SetContextDisplay(tag);
    }

    /// <summary>Reset to default (Manh 2026-11-19, mac round 7):
    /// speed 275, focus size 110, context size 40%, context display
    /// "Only show when paused". The color scheme and the context style
    /// are theme preferences, so they are NOT reset. The controls sync
    /// from the view model's notifications.</summary>
    private void OnResetClicked(object sender, RoutedEventArgs e)
        => _viewModel.ResetToDefaults();

    private void OnRewindClicked(object sender, RoutedEventArgs e) => _viewModel.StepBack();

    private void OnFastForwardClicked(object sender, RoutedEventArgs e) => _viewModel.StepForward();

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
    /// scroll down = next unit, scroll up = previous unit — one slide
    /// (Style 1) or one line (Style 2) per tick, like a fast seek.</summary>
    private void OnProgressWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var delta = e.GetCurrentPoint((UIElement)sender).Properties.MouseWheelDelta;
        if (delta == 0)
            return;
        _viewModel.SeekTo(_viewModel.CurrentPositionIndex + (delta < 0 ? 1 : -1));
        e.Handled = true;
    }

    /// <summary>Wheel over the focus word (Manh 2026-11-19): scroll up
    /// grows the focus size, scroll down shrinks it, 5pt per tick.
    /// Style 2 clamps 40-450 (the window's own theme); Style 1
    /// (FastRead Lite) does NOT cap — swipe freely, the user trims it
    /// to the frame, like the panel lite. The picker shows the nearest
    /// option — the value itself stays exact, like mac. The focus size
    /// drives the around text too (context = focus x the "Context
    /// size" percent), so one wheel tick grows/shrinks both, like 191.
    /// RESTORED 2026-11-19 from the 191 behaviour: 192 had forwarded
    /// this wheel to scroll the context instead, but Manh liked the
    /// 191 focus-size wheel and asked for it back. To scroll the
    /// context, hover the context box itself (its wheel scrolls
    /// natively). Step raised 2pt to 5pt per tick 2026-11-19 (Manh:
    /// 2pt took too many scrolls).</summary>
    private void OnFocusWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        // Style 2 ("Basic Reading") is a scrollable page: the wheel
        // scrolls the text vertically (Manh 2026-11-21) — font size
        // is click-drag there now, so do NOT zoom or mark handled
        // here; the event keeps bubbling to the page's ScrollViewer.
        // (Lite never had this problem: its Style 2 view is a sibling
        // of the zoom grid, not inside it.)
        if (_viewModel is not null && !_viewModel.IsRsvpStyle)
            return;
        var delta = e.GetCurrentPoint((UIElement)sender).Properties.MouseWheelDelta;
        if (delta == 0)
            return;
        var size = WordTextBlock.FontSize + (delta > 0 ? 5 : -5);
        if (string.Equals(_viewModel.ContextStyle, "Style2", StringComparison.OrdinalIgnoreCase))
            size = Math.Clamp(size, 40, 450);
        else
            size = Math.Max(1, size);
        _viewModel.ApplyFocusSize(size);
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
    /// same direction as the old wheel. The size is saved only on
    /// release (FinishContextResize), not on every move.</summary>
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
        var ratio = _viewModel.ContextSizeRatio;
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
        if (Math.Abs(ratio - _viewModel.ContextSizeRatio) < 0.001)
            return;
        _viewModel.SetContextSizeRatio(ratio, save: false);
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
    /// option nearest to a value — the wheel moves the size by 5pt,
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
                // During an active read, Space is always Play/Pause —
                // it must never fall through to a focused button (a
                // focused transport button would run its own action
                // instead). PreviewKeyDown catches it before the
                // button sees it. After the read finishes, Space
                // replays it from the beginning (mac parity, Manh
                // 2026-11-23) — SetPlaying(true) on a finished read
                // starts a new read at position 0. The share buttons
                // still work with Enter and the mouse.
                _viewModel.SetPlaying(!_viewModel.IsPlaying);
                e.Handled = true;
                break;
            case VirtualKey.Left:
                if (focused is Slider)
                    return; // the progress slider seeks by itself.
                _viewModel.StepBack();
                e.Handled = true;
                break;
            case VirtualKey.Right:
                if (focused is Slider)
                    return;
                _viewModel.StepForward();
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
                _viewModel.Restart();
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
