// Style 2 ("basic reading") page code-behind
// (feature/fastread-speaker-style2, mac parity:
// Style2ReadingPage.swift). Renders one RsvpReaderViewModel as a
// scrollable page of short lines (≤12 words each): the whole text at
// once, Play advancing a focus line one line at a time. The view
// model owns the lines and the current line index; this control only
// renders them, highlights the current line when the highlight is
// on, and scrolls it into the middle of the page. The control shows
// itself only in the Basic Reading style — the host hides the RSVP
// reading area itself.

using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace TransPeek.FastRead;

public sealed partial class Style2ReadingView : UserControl
{
    private RsvpReaderViewModel? _viewModel;
    // One border per line (the border carries the highlight).
    // TextBlock has NO Background in WinUI (unlike WPF), so the line
    // text lives inside a Border and the highlight goes on the border.
    private readonly List<Border> _lineBorders = [];
    // Soft accent highlight for the current line (mac: accent at 0.18
    // opacity) — the same blue the app uses for accents (#0078D4).
    private static readonly Brush HighlightBrush =
        new SolidColorBrush(ColorHelper.FromArgb(0x2E, 0x00, 0x78, 0xD4));
    // Font-size drag: press point + size at press time; the live size
    // follows the drag and is saved once on release (same pattern as
    // the window's context resize drag).
    private bool _draggingFontSize;
    private double _dragStartY;
    private double _dragStartFontSize;
    private bool _dragFontSizeChanged;

    /// <summary>The view model to render (same class the RSVP views
    /// use — one playback engine, two layouts).</summary>
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
            Refresh();
        }
    }

    /// <summary>Horizontal padding of the page (window 32, lite 12 —
    /// matches each surface's book page, so the page lines up with
    /// the Style 1 text).</summary>
    public double HorizontalPadding
    {
        get => _horizontalPadding;
        set
        {
            _horizontalPadding = value;
            // The setter can run from XAML before LinesPanel exists
            // (attributes apply before children load) — apply then.
            if (LinesPanel is not null)
                LinesPanel.Padding = new Thickness(value, 16, value, 16);
        }
    }

    private double _horizontalPadding = 32;

    public Style2ReadingView()
    {
        InitializeComponent();
        LinesPanel.Padding = new Thickness(_horizontalPadding, 16, _horizontalPadding, 16);
        // Click-drag on the page changes the font size (Manh
        // 2026-11-21, both the window and the lite reader — this
        // control is shared): press on the text, drag up to grow,
        // drag down to shrink. Replaces the wheel zoom (commit
        // 50bd04d0) — the wheel scrolls the page again, so the page
        // scrolls with the wheel AND the scrollbar.
        PageScroller.PointerPressed += OnPagePointerPressed;
        PageScroller.PointerMoved += OnPagePointerMoved;
        PageScroller.PointerReleased += OnPagePointerReleased;
        PageScroller.PointerCaptureLost += OnPagePointerCaptureLost;
    }

    /// <summary>Start a font-size drag (Manh 2026-11-21): click on the
    /// text and drag up/down to change the size. Mouse only — touch
    /// keeps scrolling the page (a touch drag is a scroll, not a
    /// zoom). IsLeftButtonPressed alone is NOT enough to tell mouse
    /// from touch: touch contact sets it too (Microsoft Learn,
    /// PointerPointProperties) — the device type is the real gate.</summary>
    private void OnPagePointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_viewModel is null)
            return;
        if (e.Pointer.PointerDeviceType != Microsoft.UI.Input.PointerDeviceType.Mouse)
            return;
        if (!e.GetCurrentPoint(PageScroller).Properties.IsLeftButtonPressed)
            return;
        _draggingFontSize = true;
        _dragFontSizeChanged = false;
        _dragStartY = e.GetCurrentPoint(PageScroller).Position.Y;
        _dragStartFontSize = _viewModel.FontSize;
        PageScroller.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    /// <summary>Font-size drag move: the size follows the pointer —
    /// up grows, down shrinks, 1pt per 2px, whole points, clamped
    /// 40–450 (the window's Style 2 range). Applied live with
    /// save: false, so the settings file is not rewritten on every
    /// move; the current line stays centered, like the old wheel
    /// zoom.</summary>
    private void OnPagePointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_draggingFontSize || _viewModel is null)
            return;
        var y = e.GetCurrentPoint(PageScroller).Position.Y;
        var size = Math.Clamp(Math.Round(_dragStartFontSize + (_dragStartY - y) / 2.0), 40, 450);
        if (Math.Abs(size - _viewModel.FontSize) < 0.5)
            return;
        _dragFontSizeChanged = true;
        _viewModel.ApplyFocusSize(size, save: false);
        ScrollToCurrentLine(animate: false);
        e.Handled = true;
    }

    private void OnPagePointerReleased(object sender, PointerRoutedEventArgs e)
        => FinishFontSizeDrag();

    private void OnPagePointerCaptureLost(object sender, PointerRoutedEventArgs e)
        => FinishFontSizeDrag();

    /// <summary>End a font-size drag (release, cancel, or lost
    /// capture): write the final size once. Idempotent — release and
    /// capture-lost can both fire for one drag — and a plain click
    /// (no move) writes nothing.</summary>
    private void FinishFontSizeDrag()
    {
        if (!_draggingFontSize)
            return;
        _draggingFontSize = false;
        if (_dragFontSizeChanged)
            _viewModel?.SaveFocusSize();
    }

    private void OnViewModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_viewModel is null)
            return;
        switch (e.PropertyName)
        {
            case nameof(RsvpReaderViewModel.ReadingStyle):
                Refresh();
                break;
            case nameof(RsvpReaderViewModel.Style2Lines):
            case nameof(RsvpReaderViewModel.FontSize):
                RebuildLines();
                break;
            case nameof(RsvpReaderViewModel.Style2CurrentLineIndex):
                RefreshHighlight();
                ScrollToCurrentLine();
                break;
            case nameof(RsvpReaderViewModel.HighlightVisible):
                RefreshHighlight();
                break;
        }
    }

    /// <summary>Show this page only in the Basic Reading style; the
    /// host hides the RSVP reading area itself.</summary>
    private void Refresh()
    {
        Visibility = _viewModel is not null && !_viewModel.IsRsvpStyle
            ? Visibility.Visible
            : Visibility.Collapsed;
        RebuildLines();
    }

    /// <summary>Rebuild the line blocks from the view model's lines
    /// (body size = focus size x 0.4, mac ratio) and highlight the
    /// current line.</summary>
    private void RebuildLines()
    {
        _lineBorders.Clear();
        LinesPanel.Children.Clear();
        if (_viewModel is null || _viewModel.IsRsvpStyle)
            return;
        // Body size is the focus-size control scaled down (0.4x, the
        // same ratio as the Style 1 around text) — the "Focus size"
        // picker is the font zoom for this page.
        var bodySize = _viewModel.FontSize * 0.4;
        foreach (var line in _viewModel.Style2Lines)
        {
            var block = new TextBlock
            {
                Text = line,
                FontSize = bodySize,
                LineHeight = bodySize * 1.5,
                TextWrapping = TextWrapping.Wrap,
            };
            // The border is the highlight surface (TextBlock has no
            // Background in WinUI) — a little padding so the highlight
            // does not hug the text, soft corners like the mac page.
            var border = new Border
            {
                Child = block,
                Padding = new Thickness(6, 2, 6, 2),
                CornerRadius = new CornerRadius(4),
            };
            _lineBorders.Add(border);
            LinesPanel.Children.Add(border);
        }
        RefreshHighlight();
    }

    /// <summary>Highlight the current line when the highlight is on
    /// (toggle, or automatically while TTS reads); other lines stay
    /// plain.</summary>
    private void RefreshHighlight()
    {
        if (_viewModel is null)
            return;
        var highlight = _viewModel.HighlightVisible;
        var current = _viewModel.Style2CurrentLineIndex;
        for (var i = 0; i < _lineBorders.Count; i++)
            _lineBorders[i].Background = highlight && i == current ? HighlightBrush : null;
    }

    /// <summary>Keep the focused line in view as Play advances it —
    /// scrolled to the middle of the page (mac: scrollTo anchor
    /// .center). The font-size drag passes animate: false — an
    /// animated scroll on every drag move would lag behind the
    /// pointer.</summary>
    private void ScrollToCurrentLine(bool animate = true)
    {
        if (_viewModel is null)
            return;
        var index = _viewModel.Style2CurrentLineIndex;
        if (index < 0 || index >= _lineBorders.Count)
            return;
        _lineBorders[index].StartBringIntoView(new BringIntoViewOptions
        {
            VerticalAlignmentRatio = 0.5,
            AnimationDesired = animate,
        });
    }
}
