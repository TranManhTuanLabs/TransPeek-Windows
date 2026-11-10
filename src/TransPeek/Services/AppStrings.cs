// Localized UI strings (English base).
// Code reads strings through AppStrings.Get(key, fallback): the fallback is
// the same English text, so the UI stays English even if a resource lookup
// fails (e.g. the .pri is missing in an unpackaged build, or a key is
// missing from a future translation file). XAML reads the same strings
// through x:Uid (resw name = Uid.Property), with the English literal kept
// in the markup as the fallback. Adding a language later = adding
// Strings/<bcp47>/Resources.resw with the same keys — no code or XAML
// change. See Strings/en-US/Resources.resw for the key list and roadmap.

using Microsoft.Windows.ApplicationModel.Resources;

namespace TransPeek.Services;

public static class AppStrings
{
    private static readonly ResourceLoader Loader = new();

    /// <summary>Localized string for <paramref name="key"/>, or
    /// <paramref name="fallback"/> (English) when the lookup returns
    /// nothing. <paramref name="args"/> fills {0}, {1}, … in the value.</summary>
    public static string Get(string key, string fallback, params object?[] args)
    {
        string value;
        try
        {
            value = Loader.GetString(key);
        }
        catch
        {
            value = "";
        }
        if (string.IsNullOrEmpty(value))
            value = fallback;
        return args.Length == 0 ? value : string.Format(value, args);
    }
}
