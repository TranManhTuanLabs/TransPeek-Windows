// Rest window code-behind (feature/focus-timer-decor, mac parity:
// RestWindowController.swift). Shows a break reminder with its own
// lite reader (its own RsvpReaderViewModel — never the Fast Read
// window's), then counts the break down and closes itself at zero.
// Esc closes the window; the reader's own keys (Space / arrows /
// R) are forwarded to the compact view.

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using TransPeek.Core.FocusTimer;
using TransPeek.FastRead;
using TransPeek.Interop;
using Windows.System;
using WinRT.Interop;

namespace TransPeek.FocusTimer;

public sealed partial class RestWindow : Window
{
    private readonly RsvpReaderViewModel _viewModel = new();
    private readonly DispatcherTimer _countdownTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private int _countdownSeconds;

    public RestWindow()
    {
        InitializeComponent();
        Title = "TransPeek — Rest";
        // Fixed 560x400 (mac: min 480x320): a transient rest window
        // does not need resizing — and the lite reader's top bar needs
        // the width (its four pickers + gear + "?" fit at 560).
        if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
            presenter.IsResizable = false;
        CompactView.ViewModel = _viewModel;
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(RsvpReaderViewModel.IsFinished) && _viewModel.IsFinished)
                ShowCountdown();
        };
        _countdownTimer.Tick += (_, _) =>
        {
            _countdownSeconds--;
            if (_countdownSeconds <= 0)
            {
                Close();
                return;
            }
            CountdownText.Text = FocusTimerEngine.Mmss(_countdownSeconds);
        };
        if (Content is FrameworkElement contentRoot)
            contentRoot.KeyDown += OnKeyDown;
        Closed += (_, _) =>
        {
            _countdownTimer.Stop();
            _viewModel.SetPlaying(false);
        };
    }

    /// <summary>Show the reminder: center a 560x400 window on the
    /// work area, load the text and start reading at once.</summary>
    public void ShowRest(string text, int countdownSeconds, string title)
    {
        TitleText.Text = title;
        _countdownSeconds = countdownSeconds;
        CountdownText.Text = FocusTimerEngine.Mmss(countdownSeconds);
        CountdownGrid.Visibility = Visibility.Collapsed;
        CompactView.Visibility = Visibility.Visible;
        _viewModel.LoadText(text);
        try
        {
            var (cx, cy) = WindowHelpers.CursorPosition();
            var work = WindowHelpers.DisplayWorkArea(cx, cy);
            var width = Math.Min(560, work.Width);
            var height = Math.Min(400, work.Height);
            var x = work.X + Math.Max(0, (work.Width - width) / 2);
            var y = work.Y + Math.Max(0, (work.Height - height) / 2);
            WindowHelpers.MoveAndResize(this, x, y, width, height);
        }
        catch
        {
            // Positioning must never stop the window from opening.
        }
        var hwnd = WindowNative.GetWindowHandle(this);
        if (NativeMethods.IsIconic(hwnd))
            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
        NativeMethods.SetForegroundWindow(hwnd);
        Activate();
        if (_viewModel.HasSlides)
            _viewModel.SetPlaying(true);
        else
            // Empty text (should not happen — reminder messages are
            // fixed strings): go straight to the countdown instead of
            // sitting on an empty reader forever.
            ShowCountdown();
    }

    /// <summary>The reminder read finished: swap the reader for the
    /// countdown and close at zero.</summary>
    private void ShowCountdown()
    {
        CompactView.Visibility = Visibility.Collapsed;
        CountdownGrid.Visibility = Visibility.Visible;
        CountdownText.Text = FocusTimerEngine.Mmss(_countdownSeconds);
        _countdownTimer.Start();
    }

    private void OnDoneClicked(object sender, RoutedEventArgs e)
        => Close();

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Esc closes the window (mac: Esc closes the Rest window).
        if (e.Key == VirtualKey.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }
        // The reader's own keys (Space / arrows / R) go to the compact
        // view — it ignores Esc itself, so there is no double-handling.
        if (CompactView.HandleKey(e.Key, FocusManager.GetFocusedElement()))
            e.Handled = true;
    }
}
