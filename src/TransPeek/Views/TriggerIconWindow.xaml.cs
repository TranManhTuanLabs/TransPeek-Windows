// Trigger icon (SPEC §3.1): the small bubble tile that appears near
// the text selection. Hover (~500 ms, configurable) opens the float
// panel (translate); click does the configured click action (Settings
// -> General -> "Click icon": Speak, the default, or Fast Read).
// Style B (Manh 2026-11-18, mac FLOAT_ICON_UX_SPEC §3 + §6): the icon
// always previews the NEXT click — Speak = plain A (not bold, Manh
// 2026-11-19; the bubble E8BD is a backup style now), Fast Read =
// custom A+ (turmeric); Alt held flips the preview to the OTHER
// action, and the flip to Speak shows the speaker (E767). After any Fast Read trigger the icon shows A+ for
// 2.5s as a confirmation, then hides. Alt for the CLICK comes straight
// from the pointer event (mac handleIconClick reads
// event.modifierFlags); the preview also polls the GLOBAL key state
// (GetAsyncKeyState, every 0.1s while visible) and the pointer events.
// Reading Alt at tap time from the global state missed it (Manh
// 2026-11-19, Click = Read: Alt+click opened Read instead of
// speaking), so the click no longer depends on the poll.

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using TransPeek.Interop;
using Windows.System;

namespace TransPeek.Views;

public sealed partial class TriggerIconWindow : Window
{
    // Icon sizes are user settings now (Manh 2026-11-21, mac parity):
    // FloatIconSize (default 24 pt) for the selection icon,
    // AutoShowFloatIconSize (default 24 pt) for the block-text hover
    // icon. The old fixed sizes (32 px window / 30 px circle, compact
    // 24 / 22) are gone — ShowAt reads the settings live at each show,
    // so a Settings change applies to the next icon that appears.

    private readonly DispatcherTimer _hoverTimer = new();
    private readonly DispatcherTimer _autoHideTimer = new();
    /// <summary>Alt preview poll (Style B, mac §5): every 0.1s while
    /// the icon is visible, read the GLOBAL Alt state. Only a real
    /// state change acts — a no-change poll must never restart the
    /// auto-hide timer, or the icon would never auto-hide. Preview
    /// only: the CLICK reads Alt from the pointer event (see
    /// PointerPressed), so a missed poll can never turn Alt+click
    /// into a plain click.</summary>
    private readonly DispatcherTimer _altPollTimer = new();
    /// <summary>2.5s A+ confirmation after a Fast Read trigger (mac
    /// §3): the icon shows A+ while the Read window opens, then hides.
    /// Clicks are ignored until it fires.</summary>
    private readonly DispatcherTimer _readLingerTimer = new();
    private bool _visible;
    /// <summary>Last Alt state seen by the poll timer (or at ShowAt).
    /// Compared each poll so only a real change flips the preview.</summary>
    private bool _altHeld;
    /// <summary>True during the 2.5s A+ confirmation after a Fast Read
    /// trigger. App reads this (IsReadLingerActive) so it does not hide
    /// the icon early in ShowFastReadWindow.</summary>
    private bool _inReadLinger;
    /// <summary>True while the right-click menu is open: the Alt poll
    /// must not restart the auto-hide timer then, or the icon could
    /// vanish under the open menu.</summary>
    private bool _menuOpen;

    /// <summary>Raised on click, or when the hover delay elapses.</summary>
    public event Action? OpenRequested;

    /// <summary>Raised when the user clicks the icon to listen to the
    /// current selection. App starts (or toggles) the TTS session
    /// and shows the separate speaker indicator — the icon itself
    /// never shows playback state (Manh 2026-11-26, indicator split:
    /// the icon is only the trigger, the indicator is the player).</summary>
    public event Action? SpeakRequested;

    /// <summary>Raised on Alt+click, on a plain click when the click
    /// action setting is Fast Read, or from the right-click menu's
    /// "Fast Read" item: App opens the Fast Read (RSVP) window with
    /// the current selection.</summary>
    public event Action? FastReadRequested;

    /// <summary>Raised from the right-click menu's "Settings…" item.</summary>
    public event Action? SettingsRequested;

    public TriggerIconWindow()
    {
        InitializeComponent();
        // NOTE: WinUI 3 Window has NO Width/Height properties (unlike
        // WPF) — setting them is a compile error (CS0103, runs 76/77).
        // The size comes from ShowAt, which resizes via AppWindow
        // MoveAndResize in physical pixels (setting * DpiScale).
        // WinUI 3 Window has no Loaded event (Loaded lives on
        // FrameworkElement): attach to the root content element instead.
        if (Content is FrameworkElement contentRoot)
        {
            contentRoot.Loaded += (_, _) =>
            {
                // Truly transparent + frameless (TransparentWindow,
                // Action-Wheel pattern): the old MakeBorderlessTopmost +
                // MakeToolWindow left the window opaque white with a
                // shadow, so the 30 px circle was invisible and the icon
                // looked like a white square (Manh 2026-11-14, build 78).
                // MakeTransparent does the presenter, the frame strip,
                // DWM blur-behind, the transparent backdrop and the black
                // native clear in one place. Loaded runs while the window
                // is first shown, so the handle is ready; MakeTransparent
                // is idempotent per HWND.
                TransparentWindow.MakeTransparent(this);
            };
        }
        IconBorder.PointerEntered += OnPointerEntered;
        IconBorder.PointerExited += OnPointerExited;
        // Right-click menu (Manh 2026-11-17, entry point 2): Translate /
        // Listen / Fast Read / Settings. The icon is a 32 px window, so
        // the menu is the only way to discover the actions without the
        // hover delay. While the menu is open the auto-hide timer stops,
        // or the icon would vanish under the open menu.
        var menu = new MenuFlyout();
        var translateItem = new MenuFlyoutItem { Text = "Translate", Icon = new FontIcon { Glyph = "\uE70F" } };
        translateItem.Click += (_, _) => OpenRequested?.Invoke();
        var listenItem = new MenuFlyoutItem { Text = "Listen", Icon = new FontIcon { Glyph = "\uE767" } };
        listenItem.Click += (_, _) =>
        {
            _hoverTimer.Stop();
            SpeakRequested?.Invoke();
        };
        var fastReadItem = new MenuFlyoutItem { Text = "Fast Read", Icon = new FontIcon { Glyph = "\uE736" } };
        fastReadItem.Click += (_, _) =>
        {
            if (_inReadLinger)
                return;
            BeginReadLinger();
            FastReadRequested?.Invoke();
        };
        var settingsItem = new MenuFlyoutItem { Text = "Settings…", Icon = new FontIcon { Glyph = "\uE713" } };
        settingsItem.Click += (_, _) => SettingsRequested?.Invoke();
        menu.Items.Add(translateItem);
        menu.Items.Add(listenItem);
        menu.Items.Add(fastReadItem);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(settingsItem);
        menu.Opened += (_, _) =>
        {
            _menuOpen = true;
            _autoHideTimer.Stop();
        };
        menu.Closed += (_, _) =>
        {
            _menuOpen = false;
            if (_visible && !_inReadLinger)
            {
                _autoHideTimer.Interval = TimeSpan.FromSeconds(App.Settings.TriggerIconAutoHideSeconds);
                _autoHideTimer.Start();
            }
        };
        IconBorder.ContextFlyout = menu;
        // Click (mac handleIconClick + Style B): a plain click does the
        // configured click action (Speak, the default, or Fast Read);
        // Alt+click ALWAYS does the OTHER action (entry point 1 for
        // Fast Read). A click cancels the pending hover timer, so hover
        // and click never both fire — and it must NOT open the panel
        // (the old code raised OpenRequested here, and opening the
        // panel hid the icon at once, so the speaker never showed).
        // A Fast Read click also stops a running speech first, then
        // starts the 2.5s A+ linger BEFORE raising FastReadRequested,
        // so App's ShowFastReadWindow sees IsReadLingerActive and does
        // not hide the icon early. During the linger further clicks
        // are ignored (no double window, no speaking over the reader).
        // PointerPressed, not Tapped: the modifier must be read from
        // the event AT PRESS TIME (mac handleIconClick reads
        // event.modifierFlags). Tapped fires on release, and Alt
        // released just before pointer-up was missed, so Alt+click
        // behaved like a plain click (Manh 2026-11-19, Click = Read).
        // Left button only: right-click opens the context menu.
        IconBorder.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(IconBorder).Properties.IsLeftButtonPressed)
                return;
            e.Handled = true;
            if (_inReadLinger)
                return;
            var altHeld = e.KeyModifiers.HasFlag(VirtualKeyModifiers.Menu);
            _altHeld = altHeld;
            var clickReads = string.Equals(App.Settings.IconClickAction, "FastRead", StringComparison.OrdinalIgnoreCase);
            // XOR, not OR (Manh 2026-11-19, CI #191 test): Alt FLIPS
            // the action — Click=Read + Alt speaks, Click=Speak + Alt
            // reads. The old `altHeld || clickReads` always read when
            // Click=Read, so Alt+click opened Read instead of speaking
            // even though the preview (which already used XOR) flipped.
            if (altHeld != clickReads)
            {
                _hoverTimer.Stop();
                BeginReadLinger();
                FastReadRequested?.Invoke();
                return;
            }
            // Plain click = listen: the icon is only the trigger —
            // App starts a new session, or pauses/resumes the
            // session for this same text, and shows the separate
            // speaker indicator. The icon itself never shows
            // playback state (Manh 2026-11-26, indicator split), so
            // there is no speaking toggle here any more; its
            // auto-hide keeps running on the normal lifecycle.
            _hoverTimer.Stop();
            SpeakRequested?.Invoke();
        };
        // Preview from the pointer event too (mac-style): the glyph
        // flips as soon as Alt is pressed/released while the pointer
        // moves over the icon, instead of waiting for the 0.1s poll.
        // The poll below stays as the fallback for Alt pressed while
        // hovering without moving.
        IconBorder.PointerMoved += (_, e) =>
        {
            var held = e.KeyModifiers.HasFlag(VirtualKeyModifiers.Menu);
            if (held == _altHeld)
                return;
            _altHeld = held;
            UpdateGlyph();
        };
        _hoverTimer.Tick += (_, _) =>
        {
            _hoverTimer.Stop();
            OpenRequested?.Invoke();
        };
        _autoHideTimer.Tick += (_, _) =>
        {
            _autoHideTimer.Stop();
            HideIcon();
        };
        _altPollTimer.Interval = TimeSpan.FromMilliseconds(100);
        _altPollTimer.Tick += (_, _) =>
        {
            var held = IsAltHeldGlobally();
            if (held == _altHeld)
                return; // no-change poll: never restart the auto-hide.
            _altHeld = held;
            UpdateGlyph();
            if (held)
            {
                // Pause the auto-hide while Alt is held (mac §4), so
                // the A+ preview does not vanish before the user can
                // click it.
                _autoHideTimer.Stop();
            }
            else if (!_menuOpen)
            {
                // Fresh delay on release — unless the menu is open
                // (then the menu's Closed handler restarts it).
                _autoHideTimer.Interval = TimeSpan.FromSeconds(App.Settings.TriggerIconAutoHideSeconds);
                _autoHideTimer.Start();
            }
        };
        _readLingerTimer.Tick += (_, _) =>
        {
            _readLingerTimer.Stop();
            _inReadLinger = false;
            HideIcon();
        };
    }

    /// <summary>Show the icon just below-right of the selection point
    /// (physical pixels), clamped to the display work area. The window
    /// is sized in PHYSICAL pixels (AppWindow.MoveAndResize) but the
    /// content is laid out in effective pixels (DIPs): the window is
    /// scaled by the display DPI, or the circle overflows (is clipped
    /// by) the window on a scaled display — the "abnormal size" Manh
    /// saw in 0.1.3.68. Sizes come from the user settings (Manh
    /// 2026-11-21, mac parity): FloatIconSize (default 24 pt) for a
    /// selection, AutoShowFloatIconSize (default 24 pt) for a hovered
    /// block — the hover icon is smaller by default, so it stays out
    /// of the mouse's way while resting on a block.</summary>
    public void ShowAt(int x, int y, bool compact = false)
    {
        // A new selection cancels a pending Read linger (fresh icon
        // state for the new text).
        _inReadLinger = false;
        _readLingerTimer.Stop();
        // Initial Alt state at show time (mac §5): the user may already
        // be holding Alt when the icon appears — read it before the
        // glyph refresh below, so the preview is right at once.
        _altHeld = IsAltHeldGlobally();
        UpdateGlyph();
        // Sizes, applied on every show from the user settings (Manh
        // 2026-11-21, mac parity: TriggerIconPanel.size /
        // compactSize read Defaults live): the window is the setting
        // itself, the circle 2 pt smaller, and the glyphs scale with
        // the circle — A = half the diameter, speaker ~44%, "+" a
        // quarter — so a smaller circle still shows a proportional
        // letter. At the old 32 pt this gives the same glyphs as
        // before (A 15, "+" 8; speaker 13 — mac has 14, because the
        // mac circle is the full 32 pt while ours is 30). A full-size
        // show after a compact one (or the reverse) resets the sizes.
        var windowPx = compact ? App.Settings.AutoShowFloatIconSize : App.Settings.FloatIconSize;
        var circlePx = windowPx - 2;
        var aSize = (int)Math.Round(circlePx * 0.5);
        var speakerSize = (int)Math.Round(circlePx * 0.44);
        var plusSize = (int)Math.Round(circlePx * 0.25);
        IconBorder.Width = circlePx;
        IconBorder.Height = circlePx;
        IconBorder.CornerRadius = new CornerRadius(circlePx / 2.0);
        PlainAMark.FontSize = aSize;
        BubbleMark.FontSize = speakerSize;
        SpeakerMark.FontSize = speakerSize;
        APlusMarkA.FontSize = aSize;
        APlusMarkPlus.FontSize = plusSize;
        APlusMarkPlus.Margin = new Thickness(0, plusSize / 2.0, plusSize / 2.0 + 1, 0);
        var scale = WindowHelpers.DpiScale(this);
        var w = Math.Max(1, (int)Math.Round(windowPx * scale));
        var h = Math.Max(1, (int)Math.Round(windowPx * scale));
        var work = WindowHelpers.DisplayWorkArea(x, y);
        var px = Math.Clamp(x + 8, work.X, work.X + work.Width - w);
        var py = Math.Clamp(y + 8, work.Y, work.Y + work.Height - h);
        WindowHelpers.MoveAndResize(this, px, py, w, h);
        // Cut the window into a circle (Manh 2026-11-15): on machines
        // where DWM blur-behind fails, the transparent backdrop shows a
        // grey square around the white circle (Lenovo T460S). A window
        // region does not depend on DWM blur, so the icon is round even
        // then. The region is in physical pixels — same w/h as the
        // window above. Re-applied on every show (ShowAt runs each time
        // the icon appears), so a DPI change is picked up too.
        ApplyCircularRegion(w, h);
        WindowHelpers.ShowWithoutActivation(this);
        _visible = true;
        _altPollTimer.Start();
        _hoverTimer.Interval = TimeSpan.FromMilliseconds(App.Settings.HoverDelayMs);
        // Pause the auto-hide if Alt is already held (mac §4).
        if (!_altHeld)
        {
            _autoHideTimer.Interval = TimeSpan.FromSeconds(App.Settings.TriggerIconAutoHideSeconds);
            _autoHideTimer.Start();
        }
    }

    /// <summary>Clip the icon window to a circle inscribed in its
    /// (physical-pixel) bounds. SetWindowRgn takes ownership of the
    /// region on success — only DeleteObject it when the call fails.</summary>
    private void ApplyCircularRegion(int width, int height)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        if (hwnd == IntPtr.Zero)
            return;
        // Right/bottom are exclusive in CreateEllipticRgn, so +1 covers
        // the full width/height.
        var region = NativeMethods.CreateEllipticRgn(0, 0, width + 1, height + 1);
        if (region == IntPtr.Zero)
            return;
        if (NativeMethods.SetWindowRgn(hwnd, region, true) == 0)
            NativeMethods.DeleteObject(region);
    }

    public void HideIcon()
    {
        if (!_visible)
            return;
        _visible = false;
        _hoverTimer.Stop();
        _autoHideTimer.Stop();
        _altPollTimer.Stop();
        _readLingerTimer.Stop();
        _inReadLinger = false;
        WindowHelpers.Hide(this);
    }

    /// <summary>True during the 2.5s A+ confirmation after a Fast Read
    /// trigger: App must not hide the icon yet (ShowFastReadWindow
    /// skips HideIcon while this is true; the linger timer hides it).</summary>
    public bool IsReadLingerActive => _inReadLinger;

    /// <summary>True while the icon window is shown: the scroll-dismiss
    /// monitor (App) reads this from its hook thread, so a scroll with
    /// no icon up costs no dispatcher post.</summary>
    public bool IsVisible => _visible;

    /// <summary>Global Alt state (mac §5): GetAsyncKeyState reads the
    /// hardware state whatever app is frontmost. The high bit (0x8000)
    /// means the key is down.</summary>
    private static bool IsAltHeldGlobally()
        => (NativeMethods.GetAsyncKeyState(NativeMethods.VK_MENU) & 0x8000) != 0;

    /// <summary>Start the 2.5s A+ confirmation (mac §3): after ANY Fast
    /// Read trigger the icon shows A+ while the Read window opens, then
    /// hides. Call BEFORE raising FastReadRequested, so App sees
    /// IsReadLingerActive and does not hide the icon early.</summary>
    private void BeginReadLinger()
    {
        _inReadLinger = true;
        _hoverTimer.Stop();
        _altPollTimer.Stop();
        _autoHideTimer.Stop();
        UpdateGlyph();
        _readLingerTimer.Interval = TimeSpan.FromSeconds(2.5);
        _readLingerTimer.Start();
    }

    /// <summary>Translate-first icon (Manh 2026-11-20 18:09 — the
    /// "future idea" in docs/FEATURES/FLOAT_ICON.md §3, now approved;
    /// it replaces Style B's preview-the-next-click, Manh 2026-11-18):
    /// the icon's normal face is the plain A (not bold, blue #0078D4
    /// — Style 1; the bubble E8BD is a backup style only, kept defined
    /// but never shown). A+ (turmeric) and the speaker (E767, blue
    /// #0078D4) only appear as the Alt preview: Alt held previews
    /// what Alt+click will do — A+ when Click=Speak (Alt+click
    /// reads), speaker when Click=FastRead (Alt+click speaks).
    /// During the 2.5s Read linger the A+ shows. The icon NEVER
    /// shows playback state (Manh 2026-11-26, indicator split: the
    /// speaker indicator is a separate window now, so the
    /// !speaking guard is gone — hover works even while a session
    /// plays, which is the point of the split: listen and translate
    /// at the same time). Exactly one mark is visible.</summary>
    private void UpdateGlyph()
    {
        var clickReads = string.Equals(App.Settings.IconClickAction, "FastRead", StringComparison.OrdinalIgnoreCase);
        // No Alt => the plain A: hover means translate, whatever Click
        // is set to. Alt held previews the Alt+click action.
        var showRead = _inReadLinger || (_altHeld && !clickReads);
        // Alt held and Click=FastRead => Alt+click speaks => speaker.
        var showAltSpeak = !showRead && _altHeld;
        BubbleMark.Visibility = Visibility.Collapsed; // backup style — kept, not shown.
        PlainAMark.Visibility = !showRead && !showAltSpeak ? Visibility.Visible : Visibility.Collapsed;
        APlusMark.Visibility = showRead ? Visibility.Visible : Visibility.Collapsed;
        SpeakerMark.Visibility = showAltSpeak ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _autoHideTimer.Stop(); // stay while the user is hovering
        // Safety net (mac §5): re-read the Alt state on mouse-enter, in
        // case a poll was missed. The event's own modifier is the
        // primary source; the global poll is the fallback.
        var held = e.KeyModifiers.HasFlag(VirtualKeyModifiers.Menu) || IsAltHeldGlobally();
        if (held != _altHeld)
        {
            _altHeld = held;
            UpdateGlyph();
        }
        // During the Read linger the hover must not open the panel.
        // (The old !isSpeaking guard is gone — Manh 2026-11-26,
        // indicator split: hover opens the panel even while a TTS
        // session plays, so the user can translate while listening.)
        if (_inReadLinger)
            return;
        _hoverTimer.Interval = TimeSpan.FromMilliseconds(App.Settings.HoverDelayMs);
        _hoverTimer.Start();
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _hoverTimer.Stop();
        if (_inReadLinger)
            return; // the linger timer hides the icon; do not restart.
        _autoHideTimer.Interval = TimeSpan.FromSeconds(App.Settings.TriggerIconAutoHideSeconds);
        _autoHideTimer.Start();
    }
}
