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
    /// value behaves like Paused.</summary>
    public bool ShowContext => _document.Slides.Count > 0
        && (string.Equals(_contextDisplay, "Always", StringComparison.OrdinalIgnoreCase)
            || (!_playing && !string.Equals(_contextDisplay, "Never", StringComparison.OrdinalIgnoreCase)));

    public RsvpReaderViewModel()
    {
        _timer.Tick += (_, _) => AdvanceSlide();
    }

    /// <summary>Load new text and show the first slide (paused — the
    /// caller starts playback once visible). Reads the reading options
    /// from Settings, like the window's LoadText did.</summary>
    public void LoadText(string text)
    {
        _sourceText = text;
        _document = RsvpEngine.Build(text, App.Settings.RsvpWpm, App.Settings.RsvpChunkSize);
        _currentSlide = 0;
        IsFinished = false;
        _timer.Stop();
        IsPlaying = false;
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
        Notify(nameof(SlideCount), nameof(TotalWords), nameof(TotalDuration), nameof(HasSlides));
        ShowSlide(0);
        UpdateProgress();
        UpdateReadingTime();
    }

    public void SetPlaying(bool playing)
    {
        if (_document.Slides.Count == 0)
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
                _currentSlide = 0;
                ShowSlide(0);
            }
            // A playing stretch starts: the clock runs until pause.
            _playStartedAt = DateTimeOffset.UtcNow;
            _timer.Interval = _document.Slides[_currentSlide].Delay;
            _timer.Start();
        }
        else
        {
            // A stretch ends: bank its time into the total.
            BankElapsed();
            _timer.Stop();
        }
        UpdateContext();
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
        if (_document.Slides.Count == 0)
            return;
        IsFinished = false;
        ShowSlide(index);
        if (IsPlaying)
        {
            _timer.Stop();
            _timer.Interval = _document.Slides[_currentSlide].Delay;
            _timer.Start();
        }
        UpdateProgress();
    }

    public void StepBack() => SeekTo(_currentSlide - 1);

    public void StepForward() => SeekTo(_currentSlide + 1);

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
    /// reading position: the slide containing the current word.</summary>
    private void RebuildKeepingPosition()
    {
        if (_document.Slides.Count == 0 || string.IsNullOrEmpty(_sourceText))
            return;
        var wordIndex = _document.Slides[_currentSlide].StartWordIndex;
        _document = RsvpEngine.Build(_sourceText, _wpm, _chunkSize);
        _currentSlide = RsvpEngine.SlideIndexForWord(_document, wordIndex);
        IsFinished = false;
        Notify(nameof(SlideCount), nameof(TotalWords), nameof(TotalDuration));
        ShowSlide(_currentSlide);
        if (IsPlaying)
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
            StatusText = "No text to read.";
            return;
        }
        var remaining = _document.RemainingDuration(_currentSlide);
        StatusText = IsFinished
            ? $"Finished — {_document.TotalWords} words in {FormatDuration(_document.TotalDuration)}"
            : $"Slide {_currentSlide + 1} / {total} • {FormatDuration(remaining)} left • {_document.TotalWords} words";
    }

    private void UpdateContext()
    {
        PauseContext = ShowContext ? _document.Context(_currentSlide) : null;
        Notify(nameof(ShowContext), nameof(ContextFontSize));
    }

    /// <summary>Total estimated reading time (mac: "Reading time:
    /// 2mins", under a minute -&gt; "0min").</summary>
    private void UpdateReadingTime()
    {
        var mins = (int)Math.Round(_document.TotalDuration.TotalMinutes);
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
