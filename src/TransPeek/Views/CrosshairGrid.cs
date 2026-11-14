// A Grid that shows a crosshair (+) cursor while the pointer is over it.
//
// UIElement.ProtectedCursor is protected — a plain Grid cannot have its
// cursor set from outside (CS0122, CI #80). The documented WinUI 3 way
// is to subclass the element and set ProtectedCursor from inside the
// subclass. This grid is used by the OCR capture screen
// (RegionSelectWindow): the screen shows a screenshot, so the user needs
// a clear "drag to select" affordance — a + cursor, like Snipping Tool.
//
// ProtectedCursor cannot be set until the visual tree is loaded
// (winui-api#112), so the cursor is set in the Loaded handler, not in
// the constructor.

using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace TransPeek.Views;

public sealed class CrosshairGrid : Grid
{
    public CrosshairGrid()
    {
        Loaded += (_, _) => ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Cross);
    }
}
