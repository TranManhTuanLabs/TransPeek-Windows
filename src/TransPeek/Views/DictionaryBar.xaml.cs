// Dictionary links at the bottom of the float panel (SPEC §3.2, mirror
// of mac Phase 1). Each link opens in the default browser. Favicons load
// asynchronously via FaviconService (host favicon.ico → Google s2 →
// DuckDuckGo); while loading (or on failure) the button shows the first
// letter of the site name in a rounded tile.

using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using TransPeek.Core.Dictionary;
using Windows.System;

namespace TransPeek.Views;

public sealed partial class DictionaryBar : UserControl
{
    private string _sourceText = "";
    private bool _dragging;
    private bool _dragMoved;
    private double _dragStartX;
    private double _dragStartOffset;

    public DictionaryBar()
    {
        InitializeComponent();
        // Mouse wheel: a horizontal ScrollViewer ignores the wheel by
        // default, so translate it into horizontal scrolling (0.1.3.49
        // test: the wheel did nothing on the dictionary bar). WinUI has
        // no wheel-delta property on the event args — it lives on the
        // pointer point (MouseWheelDelta, 120 per notch).
        BarScroll.PointerWheelChanged += (_, e) =>
        {
            var delta = e.GetCurrentPoint(BarScroll).Properties.MouseWheelDelta;
            BarScroll.ChangeView(BarScroll.HorizontalOffset - delta, null, null);
            e.Handled = true;
        };
        // Mouse drag pans like a touch swipe (0.1.3.49 test request):
        // press and drag to scroll; a plain click still opens the link.
        BarScroll.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(BarScroll).Properties.IsLeftButtonPressed)
                return;
            _dragging = true;
            _dragMoved = false;
            _dragStartX = e.GetCurrentPoint(BarScroll).Position.X;
            _dragStartOffset = BarScroll.HorizontalOffset;
            BarScroll.CapturePointer(e.Pointer);
        };
        BarScroll.PointerMoved += (_, e) =>
        {
            if (!_dragging)
                return;
            var dx = e.GetCurrentPoint(BarScroll).Position.X - _dragStartX;
            if (Math.Abs(dx) > 4)
                _dragMoved = true;
            if (_dragMoved)
                BarScroll.ChangeView(_dragStartOffset - dx, null, null);
        };
        void EndDrag(object _, PointerRoutedEventArgs e)
        {
            if (!_dragging)
                return;
            _dragging = false;
            BarScroll.ReleasePointerCapture(e.Pointer);
            // A real drag must not open the link under the pointer.
            if (_dragMoved)
                e.Handled = true;
        }
        BarScroll.PointerReleased += EndDrag;
        BarScroll.PointerCanceled += EndDrag;
    }

    /// <summary>Rebuild the buttons for the current links + display mode.
    /// <paramref name="displayMode"/> is "IconAndText" or "IconOnly".</summary>
    public void SetLinks(IReadOnlyList<DictionaryLink> links, string displayMode, string sourceText)
    {
        _sourceText = sourceText;
        LinksPanel.Children.Clear();
        // No text to look up (mac renders EmptyView): show no buttons.
        if (string.IsNullOrWhiteSpace(sourceText))
            return;
        var iconOnly = string.Equals(displayMode, "IconOnly", StringComparison.OrdinalIgnoreCase);
        foreach (var link in links)
        {
            var button = BuildButton(link, iconOnly);
            LinksPanel.Children.Add(button);
        }
    }

    private Button BuildButton(DictionaryLink link, bool iconOnly)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var icon = new Border
        {
            Width = 16,
            Height = 16,
            CornerRadius = new CornerRadius(3),
            Background = new SolidColorBrush(Microsoft.UI.Colors.LightGray),
        };
        var letter = new TextBlock
        {
            Text = link.Name.Length > 0 ? link.Name[..1].ToUpperInvariant() : "?",
            FontSize = 10,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.DarkSlateGray),
        };
        icon.Child = letter;
        content.Children.Add(icon);
        if (!iconOnly)
        {
            content.Children.Add(new TextBlock
            {
                Text = link.Name,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        var button = new Button
        {
            Content = content,
            Padding = new Thickness(8, 4, 8, 4),
            CornerRadius = new CornerRadius(6),
        };
        // Tooltip with the link name: in IconOnly mode this is the only
        // way to tell which dictionary an icon is (mac: .help(link.name)).
        ToolTipService.SetToolTip(button, link.Name);
        var url = link.UrlFor(_sourceText);
        button.IsEnabled = url is not null;
        button.Click += async (_, _) =>
        {
            var target = link.UrlFor(_sourceText);
            if (target is not null)
                await Launcher.LaunchUriAsync(target);
        };
        // Favicon: load async, replace the letter tile when it arrives.
        _ = LoadFaviconAsync(link, icon);
        return button;
    }

    private static async Task LoadFaviconAsync(DictionaryLink link, Border icon)
    {
        try
        {
            var bytes = await FaviconService.Shared.GetImageAsync(link);
            if (bytes is null || bytes.Length == 0)
                return;
            var image = new BitmapImage();
            // NOTE: do not dispose the stream — BitmapImage decodes lazily
            // and needs the source stream to stay open.
            var stream = new MemoryStream(bytes).AsRandomAccessStream();
            image.SetSource(stream); // throws for ICO bytes — caught below
            icon.Child = new Image
            {
                Source = image,
                Width = 16,
                Height = 16,
                Stretch = Stretch.Uniform,
            };
            icon.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }
        catch
        {
            // Keep the letter tile — a missing favicon never breaks the panel.
        }
    }
}
