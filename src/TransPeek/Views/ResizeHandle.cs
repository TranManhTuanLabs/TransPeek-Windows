// A thin vertical drag handle that resizes the Settings NavigationView
// pane. Shows the horizontal-resize cursor.
//
// UIElement.ProtectedCursor is protected, so the cursor cannot be set on
// a plain element from outside (CS0122) — this Grid subclass sets it
// from inside, the same pattern as CrosshairGrid. The handle itself is
// just the hit area + cursor; the visible 2 px strip is a Border child
// in the XAML, and the drag logic lives in SettingsWindow (it owns the
// NavigationView whose OpenPaneLength is being changed).
//
// NOTE: the horizontal-resize shape is SizeWestEast in WinUI 3
// (Microsoft.UI.Input.InputSystemCursorShape, value 10 — "a dual arrow
// pointing left and right, typically used for horizontal sizing").
// It is NOT SizeWe (that is the WPF name, CS0117 in CI #83) and NOT
// SizeEastWest (that was a guess from a third-party site, CS0117 in
// CI #84). The order is West-East, per the Microsoft Learn API
// reference for InputSystemCursorShape.

using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace TransPeek.Views;

public sealed class ResizeHandle : Grid
{
    public ResizeHandle()
    {
        Loaded += (_, _) => ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
    }
}
