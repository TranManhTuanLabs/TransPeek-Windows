// Shared Fast Read playback logic (feature/focus-timer-decor): the
// Fast Read window and the panel-right lite reader use the SAME view
// model class — one playback engine, two layouts. The view model owns
// the document, the current slide, the timer, the elapsed reading time
// and the Settings-backed reading options (WPM / chunk / focus size /
// context size / context display / color scheme / context style);
// each view renders the slide and the pause context its own way and
// syncs its own controls. No XAML control references here — views
// subscribe to PropertyChanged. Behaviour is the FastReadWindow's
// (2026-11-19, settled style), moved here unchanged: same delays, same
// clamps on WPM/chunk/context ratio, same status and reading-time text.
//
// The focus size is NOT clamped here on purpose: the window clamps
// it 40-450 at its own call sites (wheel + picker), the panel does
// not cap it at all (Manh 2026-11-20, 07:02 — swipe freely).

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using TransPeek.Core.FastRead;
using TransPeek.Core.Settings;
using TransPeek.Services;

namespace TransPeek.FastRead;

public sealed class RsvpReaderViewModel : INotifyPropertyChanged
{
    private readonly DispatcherTimer _timer = new();
    private RsvpDocument _document = new([], [], TimeSpan.Zero);
    private string _sourceText = "";
    private int _currentSlide;
    private bool _playing;
    private bool _finished;
    private int _wpm = 275;
    private int _chunkSize = 1;
    private double _fontSize = 110;
    private double _contextSizeRatio = 0.40;
    private string _contextDisplay = "Paused";
    private string _colorScheme = "Light";
    private string _contextStyle = "Style1";
    // Reading style (feature/fastread-speaker-style2, mac parity):
    // "RsvpFocus" (the RSVP focus window, default) or "BasicReading"
    // (a scrollable page, Play advances one line at a time). A mode
    // preference like the color scheme — Reset does NOT reset it.
    private string _readingStyle = "RsvpFocus";
    // Highlight the current line in the Basic Reading style (a toggle
    // in the Settings popover, default OFF). The highlight also turns
    // itself on while TTS is reading — see HighlightVisible.
    private bool _highlightCurrentLine;
    // Style 2 lines (paragraphs split into ≤12-word lines) and the
    // line the focus is on. Built at LoadText, like the slides.
    private IReadOnlyList<string> _style2Lines = [];
    private int _style2CurrentLineIndex;
    // Sound (feature/fastread-speaker-style2): the speaker button is a
    // mute/unmute toggle for the read, NOT a separate "speak the whole
    // text" action (that was mac CI314 bug 1). Default OFF, NOT
    // persisted — a new read starts silent, like today. When sound is
    // on, Play/Space runs each chunk/line through TTS and the speech
    // itself paces the read (no WPM timer); when off, the timer paces
    // as today. The service is wired by the host (window / panel);
    // null = no speaker at all (the Rest window).
    private bool _soundOn;
    private SystemSpeechService? _speaker;
    // Language the read is spoken in (vi/en heuristic, same as the
    // float icon's speak — SystemSpeechService.DetectLanguage).
    private string _speakLanguageCode = "en";
    // Speech generation: each spoken unit bumps it, so a SpeechEnded
    // arriving for an older utterance (after a seek/stop) is ignored
    // instead of advancing the read one extra unit.
    private int _speechGeneration;
    private int _expectedSpeechGeneration;
    // Elapsed reading time (mac RSVPView.elapsedSeconds): banked across
    // the playing stretches of the current read, for the completion
    // line + share text. The stretch clock starts when playback starts
    // and banks into the total when it pauses or finishes.
    private TimeSpan _accumulatedElapsed;
    private DateTimeOffset? _playStartedAt;
    private RsvpContext? _pauseContext;
    private string _statusText = "No text to read.";
    private string _readingTimeText = "Reading time: 0min";

    public event PropertyChangedEventHandler? PropertyChanged;

    public RsvpSlide? CurrentSlide => _document.Slides.Count == 0 ? null : _document.Slides[_currentSlide];
    public int CurrentSlideIndex => _currentSlide;
    public int SlideCount => _document.Slides.Count;
    public int TotalWords => _document.TotalWords;
    public TimeSpan TotalDuration => _document.TotalDuration;
    public bool HasSlides => _document.Slides.Count > 0;

    public bool IsPlaying
    {
        get => _playing;
        private set => SetField(ref _playing, value);
    }

    public bool IsFinished
    {
        get => _finished;
        private set
        {
            // The completion line + share text are derived from the
            // finished state (and the elapsed time), so their readers
            // must re-read them whenever this flips.
            if (SetField(ref _finished, value))
                Notify(nameof(CompletionText), nameof(ShareText));
        }
    }

    public int Wpm
    {
        get => _wpm;
        private set => SetField(ref _wpm, value);
    }

    public int ChunkSize
    {
        get => _chunkSize;
        private set => SetField(ref _chunkSize, value);
    }

    public double FontSize
    {
        get => _fontSize;
        private set => SetField(ref _fontSize, value);
    }

    public double ContextSizeRatio
    {
        get => _contextSizeRatio;
        private set => SetField(ref _contextSizeRatio, value);
    }

    public string ContextDisplay
    {
        get => _contextDisplay;
        private set => SetField(ref _contextDisplay, value);
    }

    public string ColorScheme
    {
        get => _colorScheme;
        private set => SetField(ref _colorScheme, value);
    }

    public string ContextStyle
    {
        get => _contextStyle;
        private set => SetField(ref _contextStyle, value);
    }

    /// <summary>Reading style: "RsvpFocus" (default) or "BasicReading".
    /// Display names are "RSVP Focus" / "Basic Reading" (never "Style
    /// 1" / "Style 2" — mac parity).</summary>
    public string ReadingStyle
    {
        get => _readingStyle;
        private set => SetField(ref _readingStyle, value);
    }

    /// <summary>True for the RSVP focus style (the current design).</summary>
    public bool IsRsvpStyle => string.Equals(_readingStyle, "RsvpFocus", StringComparison.OrdinalIgnoreCase);

    public bool HighlightCurrentLine
    {
        get => _highlightCurrentLine;
        private set => SetField(ref _highlightCurrentLine, value);
    }

    /// <summary>The line highlight is on when the toggle is on, or
    /// automatically while TTS is reading (mac parity: the spoken line
    /// stays visible without the reader having to turn the toggle on
    /// first). Computed, not stored.</summary>
    public bool HighlightVisible => _highlightCurrentLine || (_soundOn && _playing);

    /// <summary>Style 2 lines of the text (paragraphs split into
    /// ≤12-word lines). Empty when there is no text.</summary>
    public IReadOnlyList<string> Style2Lines => _style2Lines;

    public int Style2CurrentLineIndex => _style2CurrentLineIndex;

    public int Style2LineCount => _style2Lines.Count;

    /// <summary>Current position in the active style's unit: the slide
    /// index (Style 1) or the line index (Style 2). The progress bar
    /// and the step keys read this, not the slide index directly.</summary>
    public int CurrentPositionIndex => IsRsvpStyle ? _currentSlide : _style2CurrentLineIndex;

    /// <summary>Total units in the active style: slides (Style 1) or
    /// lines (Style 2).</summary>
    public int PositionCount => IsRsvpStyle ? _document.Slides.Count : _style2Lines.Count;

    /// <summary>Sound on/off for this read (the speaker button). Not
    /// persisted — a new read starts silent.</summary>
    public bool IsSoundOn
    {
        get => _soundOn;
        private set => SetField(ref _soundOn, value);
    }

    /// <summary>The TTS service this read speaks through (wired by the
    /// host: the reader window / the panel). Null = no speaker (the
    /// Rest window never wires one, like mac).</summary>
    public SystemSpeechService? Speaker
    {
        get => _speaker;
        set
        {
            if (_speaker == value)
                return;
            if (_speaker is not null)
                _speaker.SpeechEnded -= OnSpeechEnded;
            _speaker = value;
            if (_speaker is not null)
                _speaker.SpeechEnded += OnSpeechEnded;
            OnPropertyChanged();
        }
    }

    public RsvpContext? PauseContext
    {
        get => _pauseContext;
        private set => SetField(ref _pauseContext, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public string ReadingTimeText
    {
        get => _readingTimeText;
        private set => SetField(ref _readingTimeText, value);
    }

    // Elapsed time of the current read (banked stretches + the running
    // one, if playing). Only the completion line + share text read it,
    // and both are only shown once finished — when the clock has stopped.
    private TimeSpan ElapsedTime => _accumulatedElapsed
        + (_playStartedAt.HasValue ? DateTimeOffset.UtcNow - _playStartedAt.Value : TimeSpan.Zero);
    private int ElapsedTotalSeconds => (int)ElapsedTime.TotalSeconds;
    private int ElapsedMinutes => ElapsedTotalSeconds / 60;
    private int ElapsedSecondsPart => ElapsedTotalSeconds % 60;

    private static string Plural(int n, string singular) => n == 1 ? singular : singular + "s";

    /// <summary>Elapsed time in natural words ("2 seconds", "1 minute",
    /// "2 minutes, 30 seconds") — mac RSVPView.elapsedNaturalText, for
    /// the share text.</summary>
    private string ElapsedNaturalText
    {
        get
        {
            var total = ElapsedTotalSeconds;
            if (total < 60)
                return $"{total} {Plural(total, "second")}";
            var minutes = total / 60;
            var seconds = total % 60;
            if (seconds == 0)
                return $"{minutes} {Plural(minutes, "minute")}";
            return $"{minutes} {Plural(minutes, "minute")}, {seconds} {Plural(seconds, "second")}";
        }
    }

    /// <summary>"Congratulations! You just read X words in Y minutes, Z
    /// seconds!" — mac RSVPView.completionText (spec §14.5), shown above
    /// the progress bar once a full read finishes. Null until then.</summary>
    public string? CompletionText => IsFinished
        ? $"Congratulations! You just read {TotalWords} {Plural(TotalWords, "word")} in {ElapsedMinutes} {Plural(ElapsedMinutes, "minute")}, {ElapsedSecondsPart} {Plural(ElapsedSecondsPart, "second")}!"
        : null;

    /// <summary>Share text (mac RSVPView.shareText, option A): the real
    /// stats of the finished read, so the share is personal. Empty
    /// until the read finishes.</summary>
    public string ShareText => IsFinished
        ? $"I just read {TotalWords} {Plural(TotalWords, "word")} in {ElapsedNaturalText} with TransPeek Fast Read — read 2–3x faster and improve your learning experience!"
        : "";

    /// <summary>Context font size = focus size x the "Context size"
    /// percent (mac round 7, default 40%).</summary>
    public double ContextFontSize => FontSize * ContextSizeRatio;

    /// <summary>Whether the pause context shows right now (mac round 7,
    /// SwiftRead's contextDisplayStyle): Always = even while playing;
    /// Paused = only while paused (default); Never = never. An unknown
    /// value behaves like Paused. Style 2 never shows it — the whole
    /// page IS the context there.</summary>
    public bool ShowContext => IsRsvpStyle && _document.Slides.Count > 0
        && (string.Equals(_contextDisplay, "Always", StringComparison.OrdinalIgnoreCase)
            || (!_playing && !string.Equals(_contextDisplay, "Never", StringComparison.OrdinalIgnoreCase)));

    public RsvpReaderViewModel()
    {
        _timer.Tick += (_, _) => AdvancePosition();
    }

    /// <summary>Load new text and show the first slide (paused — the
    /// caller starts playback once visible). Reads the reading options
    /// from Settings, like the window's LoadText did.</summary>
    public void LoadText(string text)
    {
        _sourceText = text;
        _document = RsvpEngine.Build(text, App.Settings.RsvpWpm, App.Settings.RsvpChunkSize);
        _style2Lines = RsvpEngine.BuildLines(text);
        _style2CurrentLineIndex = 0;
        _speakLanguageCode = SystemSpeechService.DetectLanguage(text);
        _currentSlide = 0;
        IsFinished = false;
        _timer.Stop();
        StopSpeech();
        IsPlaying = false;
        // A new read starts silent (sound is not persisted).
        if (_soundOn)
            IsSoundOn = false;
        // A new text is a new read — the elapsed clock restarts.
        _accumulatedElapsed = TimeSpan.Zero;
        _playStartedAt = null;
        Wpm = App.Settings.RsvpWpm;
        ChunkSize = App.Settings.RsvpChunkSize;
        FontSize = App.Settings.RsvpFontSize;
        ContextSizeRatio = App.Settings.RsvpContextSizeRatio;
        ContextDisplay = App.Settings.RsvpContextDisplay;
        ColorScheme = App.Settings.RsvpColorScheme;
        ContextStyle = NormalizeContextStyle(App.Settings.RsvpContextStyle);
        ReadingStyle = NormalizeReadingStyle(App.Settings.RsvpReadingStyle);
        HighlightCurrentLine = App.Settings.RsvpHighlightCurrentLine;
        Notify(nameof(SlideCount), nameof(TotalWords), nameof(TotalDuration), nameof(HasSlides),
            nameof(Style2Lines), nameof(Style2LineCount), nameof(Style2CurrentLineIndex),
            nameof(CurrentPositionIndex), nameof(PositionCount), nameof(HighlightVisible));
        ShowSlide(0);
        UpdateProgress();
        UpdateReadingTime();
    }

    public void SetPlaying(bool playing)
    {
        if (PositionCount == 0)
            playing = false;
        IsPlaying = playing;
        if (playing)
        {
            if (IsFinished)
            {
                // Playing after a finish starts a NEW read — the
                // elapsed clock restarts with it.
                IsFinished = false;
                _accumulatedElapsed = TimeSpan.Zero;
                if (IsRsvpStyle)
                {
                    _currentSlide = 0;
                    ShowSlide(0);
                }
                else
                {
                    _style2CurrentLineIndex = 0;
                    ShowLine(0);
                }
            }
            // A playing stretch starts: the clock runs until pause.
            _playStartedAt = DateTimeOffset.UtcNow;
            RestartPacing();
        }
        else
        {
            // A stretch ends: bank its time into the total.
            BankElapsed();
            _timer.Stop();
            StopSpeech();
        }
        UpdateContext();
        Notify(nameof(HighlightVisible));
    }

    /// <summary>Restart the pacing for the current unit after a change
    /// (play / seek / style / WPM): with sound on, the speech itself
    /// paces the read — speak the current unit and wait for its end;
    /// otherwise the WPM timer paces, as today. The timer path is
    /// untouched by the sound path, so silent reading behaves exactly
    /// like before this feature.</summary>
    private void RestartPacing()
    {
        _timer.Stop();
        if (_soundOn && _speaker is not null)
        {
            _ = SpeakCurrentUnitAsync();
        }
        else
        {
            _timer.Interval = CurrentUnitDelay;
            _timer.Start();
        }
    }

    /// <summary>Delay of the current unit for the timer path: the slide
    /// delay (Style 1) or the line delay (Style 2 — words in the line
    /// at the WPM rate, mac startStyle2PlaybackTask).</summary>
    private TimeSpan CurrentUnitDelay => IsRsvpStyle
        ? _document.Slides[_currentSlide].Delay
        : LineDelay(_style2CurrentLineIndex);

    /// <summary>How long one Style 2 line takes at the current WPM
    /// (words / WPM, floored at 10 ms like a slide).</summary>
    private TimeSpan LineDelay(int lineIndex)
    {
        if (_style2Lines.Count == 0)
            return TimeSpan.FromMilliseconds(RsvpEngine.MinSlideMs);
        var words = CountWords(_style2Lines[Math.Clamp(lineIndex, 0, _style2Lines.Count - 1)]);
        var ms = words * 60000.0 / Math.Max(_wpm, 1);
        return TimeSpan.FromMilliseconds(Math.Max(RsvpEngine.MinSlideMs, ms));
    }

    private static int CountWords(string line)
    {
        var count = 0;
        var inWord = false;
        foreach (var c in line)
        {
            if (char.IsWhiteSpace(c))
                inWord = false;
            else if (!inWord)
            {
                inWord = true;
                count++;
            }
        }
        return count;
    }

    /// <summary>Speak the current unit aloud (Style 1: the slide text;
    /// Style 2: the line). The returned false (no voice installed, or
    /// synthesis failed) falls back to the timer for this unit, so the
    /// read keeps moving instead of stalling on a silent chunk.</summary>
    private async Task SpeakCurrentUnitAsync()
    {
        if (_speaker is null || !IsPlaying || !_soundOn)
            return;
        var generation = ++_speechGeneration;
        _expectedSpeechGeneration = generation;
        var text = IsRsvpStyle
            ? _document.Slides[_currentSlide].Text
            : _style2Lines[_style2CurrentLineIndex];
        var spoken = await _speaker.SpeakAsync(text, _speakLanguageCode);
        // The read may have paused / sought / switched style while the
        // speech was starting — only fall back when this utterance is
        // still the live one.
        if (!spoken && generation == _expectedSpeechGeneration && IsPlaying && _soundOn)
        {
            _timer.Interval = CurrentUnitDelay;
            _timer.Start();
        }
    }

    /// <summary>A spoken unit ended on its own: advance one unit and
    /// speak the next (TTS is the pacing clock while sound is on).
    /// Stale events (an older utterance ending after a seek/stop) are
    /// ignored via the generation, and via IsSpeaking — if a newer
    /// utterance is already playing, this end belongs to the old one.
    /// The event may arrive off the UI thread, so the advance runs on
    /// the timer's dispatcher queue.</summary>
    private void OnSpeechEnded(object? sender, EventArgs e)
    {
        var generation = _expectedSpeechGeneration;
        _timer.DispatcherQueue.TryEnqueue(() =>
        {
            if (generation != _expectedSpeechGeneration || !IsPlaying || !_soundOn)
                return;
            if (_speaker?.IsSpeaking == true)
                return;
            AdvancePosition();
            if (IsPlaying)
                _ = SpeakCurrentUnitAsync();
        });
    }

    /// <summary>Stop any speech (a no-op when nothing is speaking) and
    /// invalidate the pending SpeechEnded, so it cannot advance the
    /// read after the stop.</summary>
    private void StopSpeech()
    {
        _speechGeneration++;
        _speaker?.Stop();
    }

    /// <summary>Turn the sound on/off for this read (the speaker
    /// button). On while playing: stop the timer, speak the current
    /// unit. Off while playing: stop the speech, the timer takes over
    /// from the current unit — playback continues silently.</summary>
    public void SetSoundOn(bool soundOn)
    {
        // No speaker wired (the Rest window) — sound cannot turn on.
        if (soundOn && _speaker is null)
            return;
        if (soundOn == _soundOn)
            return;
        IsSoundOn = soundOn;
        Notify(nameof(HighlightVisible));
        if (!IsPlaying)
            return;
        if (soundOn && _speaker is not null)
        {
            _timer.Stop();
            _ = SpeakCurrentUnitAsync();
        }
        else
        {
            StopSpeech();
            _timer.Interval = CurrentUnitDelay;
            _timer.Start();
        }
    }

    /// <summary>Switch the reading style (the Settings picker). The
    /// reading position is kept as a fraction (mac parity: the same
    /// share of the text in the new style). Changing style stops the
    /// sound (mac: the voice stops with the style); playback itself
    /// continues in the new style.</summary>
    public void SetReadingStyle(string style)
    {
        style = NormalizeReadingStyle(style);
        if (string.Equals(style, _readingStyle, StringComparison.OrdinalIgnoreCase))
            return;
        var fraction = PositionCount > 1
            ? (double)CurrentPositionIndex / (PositionCount - 1)
            : 0;
        // Stop the sound first (without touching the timer — the
        // pacing restarts below for the new style).
        if (_soundOn)
        {
            StopSpeech();
            IsSoundOn = false;
        }
        ReadingStyle = style;
        App.Settings.RsvpReadingStyle = style;
        SettingsStore.Save(App.Settings);
        var newCount = PositionCount;
        var newIndex = newCount == 0 ? 0 : (int)Math.Round(fraction * (newCount - 1));
        if (IsRsvpStyle)
            ShowSlide(newIndex);
        else
            ShowLine(newIndex);
        if (IsPlaying)
            RestartPacing();
        Notify(nameof(HighlightVisible));
    }

    private static string NormalizeReadingStyle(string style)
        => string.Equals(style, "BasicReading", StringComparison.OrdinalIgnoreCase) ? "BasicReading" : "RsvpFocus";

    /// <summary>Toggle the Style 2 line highlight (the Settings
    /// popover). Persisted; the highlight also auto-shows while TTS
    /// is reading (HighlightVisible), whether or not this is on.</summary>
    public void SetHighlightCurrentLine(bool highlight)
    {
        if (highlight == _highlightCurrentLine)
            return;
        HighlightCurrentLine = highlight;
        App.Settings.RsvpHighlightCurrentLine = highlight;
        SettingsStore.Save(App.Settings);
        Notify(nameof(HighlightVisible));
    }

    /// <summary>Bank the running stretch into the elapsed total (called
    /// on pause / finish; no-op when nothing is playing).</summary>
    private void BankElapsed()
    {
        if (_playStartedAt.HasValue)
        {
            _accumulatedElapsed += DateTimeOffset.UtcNow - _playStartedAt.Value;
            _playStartedAt = null;
        }
    }

    public void SeekTo(int index)
    {
        if (PositionCount == 0)
            return;
        IsFinished = false;
        if (IsRsvpStyle)
            ShowSlide(index);
        else
            ShowLine(index);
        if (IsPlaying)
            RestartPacing();
        UpdateProgress();
    }

    public void StepBack() => SeekTo(CurrentPositionIndex - 1);

    public void StepForward() => SeekTo(CurrentPositionIndex + 1);

    /// <summary>Restart from the beginning: a fresh read, so the
    /// elapsed clock restarts too (a restart mid-read does not keep
    /// the previous partial time).</summary>
    public void Restart()
    {
        _accumulatedElapsed = TimeSpan.Zero;
        _playStartedAt = IsPlaying ? DateTimeOffset.UtcNow : null;
        SeekTo(0);
    }

    public void SetWpm(int wpm)
    {
        wpm = Math.Clamp(wpm, RsvpEngine.MinWpm, RsvpEngine.MaxWpm);
        if (wpm == _wpm)
            return;
        Wpm = wpm;
        App.Settings.RsvpWpm = wpm;
        SettingsStore.Save(App.Settings);
        RebuildKeepingPosition();
    }

    public void SetChunkSize(int chunk)
    {
        chunk = Math.Clamp(chunk, 1, 6);
        if (chunk == _chunkSize)
            return;
        ChunkSize = chunk;
        App.Settings.RsvpChunkSize = chunk;
        SettingsStore.Save(App.Settings);
        RebuildKeepingPosition();
    }

    /// <summary>Apply a focus size (no clamp here — the window clamps
    /// 40-450 at its call sites, the panel does not cap it) and
    /// re-render the current slide (the pivot shift depends on it).</summary>
    public void ApplyFocusSize(double size)
    {
        if (Math.Abs(size - _fontSize) < 0.001)
            return;
        FontSize = size;
        App.Settings.RsvpFontSize = size;
        SettingsStore.Save(App.Settings);
        ShowSlide(_currentSlide);
    }

    /// <summary>Set the context size ratio (0.25-1.00). The window's
    /// drag-to-resize passes save: false — the size is saved once on
    /// release (FinishContextResize), not on every pointer move.</summary>
    public void SetContextSizeRatio(double ratio, bool save = true)
    {
        ratio = Math.Clamp(ratio, 0.25, 1.00);
        if (Math.Abs(ratio - _contextSizeRatio) < 0.001)
            return;
        ContextSizeRatio = ratio;
        App.Settings.RsvpContextSizeRatio = ratio;
        if (save)
            SettingsStore.Save(App.Settings);
        UpdateContext();
    }

    /// <summary>Set the context text style ("Style1" = FastRead Lite
    /// book page, default; "Style2" = the window's scroll paragraph).
    /// Unknown values fall back to Style1, like ContextDisplay falls
    /// back to Paused. The style is a theme preference, so Reset
    /// does NOT reset it (same as the color scheme).</summary>
    public void SetContextStyle(string style)
    {
        style = NormalizeContextStyle(style);
        if (string.Equals(style, _contextStyle, StringComparison.OrdinalIgnoreCase))
            return;
        ContextStyle = style;
        App.Settings.RsvpContextStyle = style;
        SettingsStore.Save(App.Settings);
    }

    private static string NormalizeContextStyle(string style)
        => string.Equals(style, "Style2", StringComparison.OrdinalIgnoreCase) ? "Style2" : "Style1";

    public void SetColorScheme(string scheme)
    {
        if (string.Equals(scheme, _colorScheme, StringComparison.OrdinalIgnoreCase))
            return;
        ColorScheme = scheme;
        App.Settings.RsvpColorScheme = scheme;
        SettingsStore.Save(App.Settings);
    }

    public void SetContextDisplay(string display)
    {
        if (string.Equals(display, _contextDisplay, StringComparison.OrdinalIgnoreCase))
            return;
        ContextDisplay = display;
        App.Settings.RsvpContextDisplay = display;
        SettingsStore.Save(App.Settings);
        UpdateContext();
    }

    /// <summary>Reset to default (mac round 7): speed 275, focus size
    /// 110, context size 40%, context display "Only show when paused".
    /// The color scheme and the context style are theme preferences,
    /// so they are NOT reset.</summary>
    public void ResetToDefaults()
    {
        App.Settings.RsvpWpm = 275;
        App.Settings.RsvpFontSize = 110;
        App.Settings.RsvpContextSizeRatio = 0.40;
        App.Settings.RsvpContextDisplay = "Paused";
        SettingsStore.Save(App.Settings);
        Wpm = 275;
        FontSize = 110;
        ContextSizeRatio = 0.40;
        ContextDisplay = "Paused";
        RebuildKeepingPosition();
        ShowSlide(_currentSlide);
    }

    private void AdvancePosition()
    {
        if (IsRsvpStyle)
            AdvanceSlide();
        else
            AdvanceLine();
    }

    private void AdvanceSlide()
    {
        if (_currentSlide + 1 >= _document.Slides.Count)
        {
            // Finish: bank the last stretch FIRST, so the completion
            // line reads the final elapsed time when IsFinished flips.
            SetPlaying(false);
            IsFinished = true;
            UpdateProgress();
            return;
        }
        ShowSlide(_currentSlide + 1);
        if (IsPlaying)
            _timer.Interval = _document.Slides[_currentSlide].Delay;
    }

    /// <summary>Style 2 advance (mac startStyle2PlaybackTask): one
    /// focus line at a time, at the WPM pace for that line's words.
    /// The timer path re-arms itself; the sound path is driven by
    /// SpeechEnded instead (OnSpeechEnded calls AdvancePosition, so
    /// this runs there too — the timer is stopped while sound is on).</summary>
    private void AdvanceLine()
    {
        if (_style2CurrentLineIndex + 1 >= _style2Lines.Count)
        {
            // Finish: same bank-first order as AdvanceSlide.
            SetPlaying(false);
            IsFinished = true;
            UpdateProgress();
            return;
        }
        ShowLine(_style2CurrentLineIndex + 1);
        if (IsPlaying)
            _timer.Interval = LineDelay(_style2CurrentLineIndex);
    }

    private void ShowLine(int index)
    {
        if (_style2Lines.Count == 0)
        {
            _style2CurrentLineIndex = 0;
            Notify(nameof(Style2CurrentLineIndex), nameof(CurrentPositionIndex), nameof(PositionCount));
            UpdateProgress();
            return;
        }
        _style2CurrentLineIndex = Math.Clamp(index, 0, _style2Lines.Count - 1);
        Notify(nameof(Style2CurrentLineIndex), nameof(CurrentPositionIndex), nameof(PositionCount));
        UpdateProgress();
    }

    /// <summary>Index of the Style 2 line containing the given word
    /// index (for keeping the reading position when WPM changes —
    /// the slide equivalent is RsvpEngine.SlideIndexForWord).</summary>
    private int LineIndexForWord(int wordIndex)
    {
        wordIndex = Math.Clamp(wordIndex, 0, Math.Max(0, TotalWords - 1));
        var seen = 0;
        for (var i = 0; i < _style2Lines.Count; i++)
        {
            seen += CountWords(_style2Lines[i]);
            if (wordIndex < seen)
                return i;
        }
        return Math.Max(0, _style2Lines.Count - 1);
    }

    private void ShowSlide(int index)
    {
        if (_document.Slides.Count == 0)
        {
            _currentSlide = 0;
            Notify(nameof(CurrentSlide), nameof(CurrentSlideIndex));
            UpdateProgress();
            UpdateContext();
            return;
        }
        _currentSlide = Math.Clamp(index, 0, _document.Slides.Count - 1);
        Notify(nameof(CurrentSlide), nameof(CurrentSlideIndex));
        UpdateProgress();
        UpdateContext();
    }

    /// <summary>Rebuild the document (WPM/chunk changed) keeping the
    /// reading position: the slide containing the current word (Style
    /// 1) or the line containing it (Style 2 — the lines do not depend
    /// on WPM/chunk, only their pace does).</summary>
    private void RebuildKeepingPosition()
    {
        if (_document.Slides.Count == 0 || string.IsNullOrEmpty(_sourceText))
            return;
        var wordIndex = IsRsvpStyle
            ? _document.Slides[_currentSlide].StartWordIndex
            : _style2Lines.Take(_style2CurrentLineIndex).Sum(CountWords);
        _document = RsvpEngine.Build(_sourceText, _wpm, _chunkSize);
        if (IsRsvpStyle)
        {
            _currentSlide = RsvpEngine.SlideIndexForWord(_document, wordIndex);
        }
        else
        {
            _style2CurrentLineIndex = LineIndexForWord(wordIndex);
            Notify(nameof(Style2CurrentLineIndex), nameof(CurrentPositionIndex));
        }
        IsFinished = false;
        Notify(nameof(SlideCount), nameof(TotalWords), nameof(TotalDuration));
        if (IsRsvpStyle)
            ShowSlide(_currentSlide);
        else
            ShowLine(_style2CurrentLineIndex);
        if (IsPlaying)
            RestartPacing();
        UpdateReadingTime();
    }

    private void UpdateProgress()
    {
        if (PositionCount == 0)
        {
            StatusText = "No text to read.";
            return;
        }
        if (IsRsvpStyle)
        {
            var total = _document.TotalSlides;
            var remaining = _document.RemainingDuration(_currentSlide);
            StatusText = IsFinished
                ? $"Finished — {_document.TotalWords} words in {FormatDuration(_document.TotalDuration)}"
                : $"Slide {_currentSlide + 1} / {total} • {FormatDuration(remaining)} left • {_document.TotalWords} words";
            return;
        }
        // Style 2 counts lines, not slides (mac: "3 / 42").
        var totalLines = _style2Lines.Count;
        var remainingLines = Style2RemainingDuration(_style2CurrentLineIndex);
        StatusText = IsFinished
            ? $"Finished — {TotalWords} words in {FormatDuration(Style2TotalDuration())}"
            : $"Line {_style2CurrentLineIndex + 1} / {totalLines} • {FormatDuration(remainingLines)} left • {TotalWords} words";
    }

    /// <summary>Time left from the given line to the end (inclusive),
    /// at the current WPM — the line equivalent of
    /// RsvpDocument.RemainingDuration.</summary>
    private TimeSpan Style2RemainingDuration(int lineIndex)
    {
        var total = TimeSpan.Zero;
        for (var i = Math.Clamp(lineIndex, 0, Math.Max(0, _style2Lines.Count - 1)); i < _style2Lines.Count; i++)
            total += LineDelay(i);
        return total;
    }

    /// <summary>Total Style 2 reading time at the current WPM (all
    /// lines' delays).</summary>
    private TimeSpan Style2TotalDuration() => Style2RemainingDuration(0);

    private void UpdateContext()
    {
        PauseContext = ShowContext ? _document.Context(_currentSlide) : null;
        Notify(nameof(ShowContext), nameof(ContextFontSize));
    }

    /// <summary>Total estimated reading time (mac: "Reading time:
    /// 2mins", under a minute -&gt; "0min"). Style 2 estimates from
    /// the line delays at the current WPM, not the slide total.</summary>
    private void UpdateReadingTime()
    {
        var duration = IsRsvpStyle ? _document.TotalDuration : Style2TotalDuration();
        var mins = (int)Math.Round(duration.TotalMinutes);
        ReadingTimeText = mins <= 0 ? "Reading time: 0min" : $"Reading time: {mins}mins";
    }

    private static string FormatDuration(TimeSpan d) => $"{(int)d.TotalMinutes}:{d.Seconds:00}";

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void Notify(params string[] propertyNames)
    {
        foreach (var name in propertyNames)
            OnPropertyChanged(name);
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
