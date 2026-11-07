// Embedded tray icon (32x32 PNG, blue tile with a white "T").
// Stored as base64 so the repo stays text-only: the connector cannot push
// binary files, and v0.1 ships no .ico/.png assets. Decoded at runtime
// into a BitmapImage for the tray icon (see TrayIconService).

namespace TransPeek.Assets;

internal static class EmbeddedIcon
{
    public const string PngBase64 = "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAAYUlEQVR42u3XOwoAIAwD0N7HQ3p03VyUUkt/aAJuQt7gYIiYtD6GxaGbWJWqMN7lLCKq/IiILt8QfwOyyhdCckkbAN4BSFCujxAAAAAAAIC3AfgTlgCkD5MS06zEOM2Y5xPSrGYngyUtTAAAAABJRU5ErkJggg==";
}
