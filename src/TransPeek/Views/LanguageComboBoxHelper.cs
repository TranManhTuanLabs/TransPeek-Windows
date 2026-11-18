// Searchable language picker shared by Settings (TranslationPage) and
// the translate panel (TranslatePanelWindow): one helper, no copy-paste
// of the filter logic (Manh 2026-11-19, 249 languages).
//
// NOTE (2026-11-19): the picker is an AutoSuggestBox, NOT a ComboBox.
// The first version used an editable ComboBox + TextChanged — but
// WinUI ComboBox HAS no TextChanged event (CS1061, CI #189; ComboBox
// only has TextSubmitted, which fires on Enter/focus-loss, so it
// cannot filter live). AutoSuggestBox is the native control built for
// type-to-search (Microsoft Learn: "Use an AutoSuggestBox to provide
// a list of suggestions for a user to select from as they type").
// The file keeps its old name because a snapshot push cannot rename
// or delete files — rename it to LanguageSuggestBoxHelper.cs on the
// next browser-side edit (see DECISIONS.md, CI #189).
//
// Behaviour (Manh's pick): the suggestion list does NOT expand to the
// full list — MaxSuggestionListHeight caps it so it scrolls inside.
// Typing filters the list live (native name, English name, or code).
// The 20 popular languages come first, then the full list A–Z.
// Focusing the box shows the full list again; leaving it without a
// pick restores the saved language.

using Microsoft.UI.Xaml.Controls;
using TransPeek.Core.Translation;

namespace TransPeek.Views;

public sealed class LanguageSuggestBoxHelper
{
    /// <summary>One suggestion: the language code and the name shown
    /// in the box and the list (native name, as before).</summary>
    public record LanguageItem(string Code, string DisplayName);

    private static readonly HashSet<string> PopularSet = new(LanguageCode.Popular, StringComparer.Ordinal);
    private static readonly Dictionary<string, (string Name, string EnglishName)> ByCode =
        LanguageCode.Supported.ToDictionary(
            lang => lang.Code,
            lang => (lang.Name, lang.EnglishName),
            StringComparer.Ordinal);

    private readonly AutoSuggestBox _box;
    private readonly bool _includeAuto;
    private readonly Func<string> _getCommittedTag;
    private readonly Action<string> _onPicked;
    private readonly string _autoLabel;
    private bool _suppress;

    /// <summary>The last committed language code (what the box shows).
    /// Pages read this for the swap button instead of the box text,
    /// which may hold uncommitted typing.</summary>
    public string CommittedTag { get; private set; } = LanguageCode.Auto;

    /// <param name="box">The AutoSuggestBox to turn into a language picker.</param>
    /// <param name="includeAuto">Prepend the "Automatic" item (source only).</param>
    /// <param name="getCommittedTag">Returns the currently saved language
    /// code, for restoring the text when the user leaves without a pick.</param>
    /// <param name="autoLabel">Label for the Automatic item.</param>
    /// <param name="onPicked">Called (once) when the user picks a
    /// language — the page saves it there.</param>
    public LanguageSuggestBoxHelper(
        AutoSuggestBox box,
        bool includeAuto,
        Func<string> getCommittedTag,
        string autoLabel,
        Action<string> onPicked)
    {
        _box = box;
        _includeAuto = includeAuto;
        _getCommittedTag = getCommittedTag;
        _autoLabel = autoLabel;
        _onPicked = onPicked;
        box.TextMemberPath = "DisplayName";
        box.DisplayMemberPath = "DisplayName";
        box.UpdateTextOnSelect = true;
        box.MaxSuggestionListHeight = 320;
        box.TextChanged += OnTextChanged;
        box.SuggestionChosen += OnSuggestionChosen;
        box.QuerySubmitted += OnQuerySubmitted;
        box.GotFocus += (_, _) => ShowFullList();
        box.LostFocus += (_, _) => RestoreCommitted();
        Refresh(_getCommittedTag());
    }

    /// <summary>Show the full list (popular first) for the given tag.
    /// Call when the saved language changes elsewhere (e.g. the
    /// panel's swap button).</summary>
    public void Refresh(string committedTag)
    {
        CommittedTag = committedTag;
        _suppress = true;
        try
        {
            _box.ItemsSource = FullList();
            SetTextFor(committedTag);
            _box.IsSuggestionListOpen = false;
        }
        finally
        {
            _suppress = false;
        }
    }

    private void OnTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (_suppress || args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
            return;
        // Live filter while typing; an empty query shows the full list.
        // NOTE: AutoSuggestBoxTextChangedEventArgs has NO QueryText
        // (CI #190) — only Reason. The text lives on the control
        // itself (sender.Text); QueryText belongs to
        // AutoSuggestBoxQuerySubmittedEventArgs only.
        _box.ItemsSource = FilteredList(sender.Text);
    }

    private void OnSuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is LanguageItem item)
        {
            // TextMemberPath + UpdateTextOnSelect already put the item
            // name in the box; only the commit is ours.
            CommittedTag = item.Code;
            _onPicked(item.Code);
        }
    }

    private void OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        // Enter with a highlighted suggestion: SuggestionChosen already
        // handled it. Enter with typed text only: pick the exact match
        // (name, English name, or code) if there is one, else restore.
        if (args.ChosenSuggestion is LanguageItem)
            return;
        var query = args.QueryText?.Trim();
        if (!string.IsNullOrEmpty(query))
        {
            // Exact match only (full name, English name, or code).
            // Note: the Auto item has no ByCode entry — look the
            // English name up safely instead of indexing ByCode.
            var exact = FullList().FirstOrDefault(item =>
                string.Equals(item.DisplayName, query, StringComparison.OrdinalIgnoreCase)
                || (ByCode.TryGetValue(item.Code, out var en)
                    && string.Equals(en.EnglishName, query, StringComparison.OrdinalIgnoreCase))
                || string.Equals(item.Code, query, StringComparison.OrdinalIgnoreCase));
            if (exact is not null)
            {
                CommittedTag = exact.Code;
                SetTextFor(exact.Code);
                _onPicked(exact.Code);
                return;
            }
        }
        RestoreCommitted();
    }

    private void ShowFullList()
    {
        if (_suppress)
            return;
        _box.ItemsSource = FullList();
        _box.IsSuggestionListOpen = true;
    }

    private void RestoreCommitted()
    {
        // The user typed without picking: put the full list back and
        // restore the saved language text.
        _box.ItemsSource = FullList();
        SetTextFor(CommittedTag);
        _box.IsSuggestionListOpen = false;
    }

    private List<LanguageItem> FullList() => FilteredList(null);

    private List<LanguageItem> FilteredList(string? query)
    {
        var list = new List<LanguageItem>();
        if (_includeAuto && AutoMatches(query))
            list.Add(new LanguageItem(LanguageCode.Auto, _autoLabel));
        foreach (var code in LanguageCode.Popular)
        {
            var (name, english) = ByCode[code];
            if (Matches(name, english, code, query))
                list.Add(new LanguageItem(code, name));
        }
        foreach (var (code, name, english) in LanguageCode.Supported)
        {
            if (PopularSet.Contains(code))
                continue;
            if (Matches(name, english, code, query))
                list.Add(new LanguageItem(code, name));
        }
        return list;
    }

    private bool AutoMatches(string? query)
        => string.IsNullOrWhiteSpace(query)
        || _autoLabel.Contains(query, StringComparison.OrdinalIgnoreCase)
        || "Automatic".Contains(query, StringComparison.OrdinalIgnoreCase)
        || "Auto Detect".Contains(query, StringComparison.OrdinalIgnoreCase);

    private static bool Matches(string name, string english, string code, string? query)
        => string.IsNullOrWhiteSpace(query)
        || name.Contains(query, StringComparison.OrdinalIgnoreCase)
        || english.Contains(query, StringComparison.OrdinalIgnoreCase)
        || code.StartsWith(query, StringComparison.OrdinalIgnoreCase);

    private void SetTextFor(string tag)
    {
        // Setting Text fires TextChanged with Reason ProgrammaticChange,
        // which OnTextChanged ignores — no _suppress flag needed here.
        if (_includeAuto && string.Equals(tag, LanguageCode.Auto, StringComparison.Ordinal))
        {
            _box.Text = _autoLabel;
            return;
        }
        if (ByCode.TryGetValue(tag, out var entry))
        {
            _box.Text = entry.Name;
            return;
        }
        // Unknown saved code (should not happen): fall back to the
        // first item, as the old ComboBox helper did.
        if (_box.ItemsSource is IReadOnlyList<LanguageItem> items && items.Count > 0)
            _box.Text = items[0].DisplayName;
    }
}
