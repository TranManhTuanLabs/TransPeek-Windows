// Hand cursor for clickable buttons (Manh 2026-11-19, About tab):
// WinUI 3 has no public Cursor property — UIElement.ProtectedCursor
// is protected (Microsoft Learn: UIElement.ProtectedCursor, Windows
// App SDK), so it can only be set from inside a subclass of the
// control. These two tiny subclasses set the hand cursor when the
// element loads (ProtectedCursor cannot be set until the visual tree
// is loaded). Used for every clickable button/link in About, and for
// the share buttons built by ShareRowBuilder (About + panel left).

using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace TransPeek.Views;

/// <summary>A Button that shows the hand pointer on hover.</summary>
public sealed class HandCursorButton : Button
{
    public HandCursorButton()
    {
        Loaded += (_, _) => ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
    }
}

/// <summary>A HyperlinkButton that shows the hand pointer on hover.</summary>
public sealed class HandCursorHyperlinkButton : HyperlinkButton
{
    public HandCursorHyperlinkButton()
    {
        Loaded += (_, _) => ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
    }
}
