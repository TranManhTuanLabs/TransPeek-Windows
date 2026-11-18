// Searchable language picker shared by Settings (TranslationPage) and
// the translate panel (TranslatePanelWindow): one helper, no copy-paste
// of the filter logic (Manh 2026-11-19, 249 languages).
//
// Behaviour (Manh's pick): the dropdown does NOT expand to the full
// list — MaxDropDownHeight caps it so it scrolls inside. The ComboBox
// is editable: the text field IS the search box, typing filters the
// list live (native name, English name, or code). The 20 popular
// languages come first, then the full list A–Z. Opening the dropdown
// always shows the full list again; closing without a pick restores
// the saved language.

using Microsoft.UI.Xaml.Controls;
using TransPeek.Core.Translation;

namespace TransPeek.Views;

public sealed class LanguageComboBoxHelper
{
    private static readonly HashSet<string> PopularSet = new(LanguageCode.Popular, StringComparer.Ordinal);
    private static readonly Dictionary<string, (string Name, string EnglishName)> ByCode =
        LanguageCode.Supported.ToDictionary(
            lang => lang.Code,
            lang => (lang.Name, lang.EnglishName),
            StringComparer.Ordinal);

    private readonly ComboBox _combo;
    private readonly bool _includeAuto;
    private readonly Func<string> _getCommittedTag;
    private readonly string _autoLabel;
    private bool _suppressFilter;
    private string? _lastSelectedTag;

    /// <param name="combo">The ComboBox to turn into a language picker.</param>
    /// <param name="includeAuto">Prepend the "Automatic" item (source only).</param>
    /// <param name="getCommittedTag">Returns the currently saved language
    /// code, for restoring the selection when the dropdown closes without
    /// a pick.</param>
    /// <param name="autoLabel">Label for the Automatic item.</param>
    public LanguageComboBoxHelper(ComboBox combo, bool includeAuto, Func<string> getCommittedTag, string autoLabel)
    {
        _combo = combo;
        _includeAuto = includeAuto;
        _getCommittedTag = getCommittedTag;
        _autoLabel = autoLabel;
        combo.IsEditable = true;
        combo.IsTextSearchEnabled = false;
        combo.MaxDropDownHeight = 320;
        combo.TextChanged += OnTextChanged;
        combo.DropDownOpened += (_, _) => RebuildFullList();
        combo.DropDownClosed += (_, _) => RestoreCommitted();
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
                _lastSelectedTag = tag;
        };
        Refresh(_getCommittedTag());
    }

    /// <summary>Rebuild the full list (popular first) and select the
    /// given tag. Call when the saved language changes elsewhere
    /// (e.g. the panel's swap button).</summary>
    public void Refresh(string committedTag)
    {
        _suppressFilter = true;
        try
        {
            RebuildFullList();
            SelectTag(committedTag);
        }
        finally
        {
            _suppressFilter = false;
        }
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressFilter)
            return;
        // Text set by picking an item (not by typing): the text equals
        // the selected item's name — do not filter the list down to it.
        if (_combo.SelectedItem is ComboBoxItem selected
            && string.Equals(_combo.Text, selected.Content as string, StringComparison.Ordinal))
            return;
        Rebuild(_combo.Text);
    }

    private void RebuildFullList() => Rebuild(null);

    private void RestoreCommitted()
    {
        // The user typed without picking: put the full list back and
        // reselect the saved language (or the last picked one, which
        // the page has already saved by now).
        Refresh(_lastSelectedTag ?? _getCommittedTag());
    }

    private void Rebuild(string? query)
    {
        _suppressFilter = true;
        try
        {
            _combo.Items.Clear();
            if (_includeAuto && AutoMatches(query))
                _combo.Items.Add(new ComboBoxItem { Content = _autoLabel, Tag = LanguageCode.Auto });
            foreach (var code in LanguageCode.Popular)
            {
                var (name, english) = ByCode[code];
                if (Matches(name, english, code, query))
                    _combo.Items.Add(new ComboBoxItem { Content = name, Tag = code });
            }
            foreach (var (code, name, english) in LanguageCode.Supported)
            {
                if (PopularSet.Contains(code))
                    continue;
                if (Matches(name, english, code, query))
                    _combo.Items.Add(new ComboBoxItem { Content = name, Tag = code });
            }
            // Full list only: keep the selection highlight on the saved
            // language. Never reselect while filtering — setting the
            // selection would overwrite the typed query with the item
            // name.
            if (query is null && _lastSelectedTag is not null)
                SelectTag(_lastSelectedTag);
        }
        finally
        {
            _suppressFilter = false;
        }
    }

    private bool AutoMatches(string? query)
        => query is null
        || _autoLabel.Contains(query, StringComparison.OrdinalIgnoreCase)
        || "Automatic".Contains(query, StringComparison.OrdinalIgnoreCase)
        || "Auto Detect".Contains(query, StringComparison.OrdinalIgnoreCase);

    private static bool Matches(string name, string english, string code, string? query)
        => query is null
        || name.Contains(query, StringComparison.OrdinalIgnoreCase)
        || english.Contains(query, StringComparison.OrdinalIgnoreCase)
        || code.StartsWith(query, StringComparison.OrdinalIgnoreCase);

    private void SelectTag(string tag)
    {
        foreach (var item in _combo.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag as string, tag, StringComparison.Ordinal))
            {
                _combo.SelectedItem = item;
                return;
            }
        }
        if (_combo.Items.Count > 0)
            _combo.SelectedIndex = 0;
    }
}
