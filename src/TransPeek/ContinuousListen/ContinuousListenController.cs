// Continuous listen (chaining, Manh 2026-11-27, word-start): after the
// click-to-listen first chunk ends, keep reading the page — probe UIA
// below the paragraph just read for the next paragraph, read it fully,
// repeat to the end of the page. Shared by hover-Speak and select-Speak
// (both enter via App.StartClickToListenAsync, like Mac where both set
// isChaining and share onEnded -> nextBlock). The chain state + probe
// geometry live in Core (ContinuousListenChain, unit-tested); this class
// only does the UIA probing and drives the speech through App's
// callbacks, so it never touches a WinUI window itself.
//
// Word-start (Manh 2026-11-27 22:32): the first chunk is the EXACT
// selected text, not the sentence containing it. Mac reads from the
// sentence start because its highlight/overlay needs a sentence
// (playOffset + AX frames); Windows v1 has no overlay, so there is
// nothing to map — the exact word is the honest start. Do NOT add a
// sentence splitter here.
//
// Capture lesson (Mac follow-up fa187c83/5259d106): the paragraph must
// be captured at SELECTION/HOVER time, not re-probed at Speak time —
// by the time Speak is clicked, the hovered block is usually gone
// (mouse-down selection stops the hover following, and hover is
// blocked while the icon shows). App stores the paragraph when the
// monitors fire it; this controller only receives it via StartChain.

using System.Drawing;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using TransPeek.Core.ContinuousListen;
using TransPeek.Services;

namespace TransPeek.ContinuousListen;

/// <summary>Drives one continuous-listen chain: holds the chain state
/// (Core), probes UIA below the paragraph just read for the next one,
/// and speaks it through App's callback. App starts the chain after
/// speaking the first chunk, calls AdvanceAsync from its SpeechEnded
/// handler while the chain is active, and cancels it from every stop
/// path (StopFloatSession). The indicator stays at the original click
/// spot for the whole chain (v1) — App shows it, this class only
/// remembers where it is.</summary>
public sealed class ContinuousListenController
{
    // Speak one chunk in the chain language (App: _iconSpeech.SpeakAsync
    // + update _floatSessionText). Returns true when playback started.
    private readonly Func<string, string, Task<bool>> _speak;
    // The chain ended on its own (end of page, probe found nothing, or
    // a chunk failed to start) — App runs the normal session cleanup
    // (hide indicator, hide same-text icon, show a waiting reminder).
    private readonly Action _chainEnded;
    // Fresh DPI scale per probe (the hidden window's scale; 1.0 at 96
    // DPI): the probe offsets are points, converted to pixels with it.
    private readonly Func<double> _getDpiScale;
    private readonly ContinuousListenChain _chain = new();

    /// <param name="speak">Speak a chunk (text, languageCode); true
    /// when playback started. Runs on the UI thread (see AdvanceAsync)
    /// so App may touch its speech service directly.</param>
    /// <param name="chainEnded">The chain ended on its own — App does
    /// the normal session cleanup. Never invoked for a Cancel (App
    /// already cleans up on those paths).</param>
    /// <param name="getDpiScale">Physical pixels per DIP (1.0 at 96
    /// DPI / 100%), read fresh per probe.</param>
    public ContinuousListenController(
        Func<string, string, Task<bool>> speak,
        Action chainEnded,
        Func<double> getDpiScale)
    {
        _speak = speak;
        _chainEnded = chainEnded;
        _getDpiScale = getDpiScale;
    }

    /// <summary>True while a chain is active (a first chunk was spoken
    /// with a paragraph context and no end/stop has happened yet).</summary>
    public bool IsActive => _chain.IsActive;

    /// <summary>Where the speaker indicator was shown for this chain
    /// (the original click spot, kept for the whole chain in v1).</summary>
    public (int X, int Y) IndicatorPos { get; private set; }

    /// <summary>Begin a chain after App spoke the first chunk: record
    /// the paragraph it came from (the probe anchor), the language
    /// (detected once, reused for the whole chain) and the indicator
    /// position. Does NOT speak — the first chunk is already playing.</summary>
    public void StartChain(TextParagraph paragraph, string language, (int X, int Y) indicatorPos)
    {
        _chain.Start(paragraph, language);
        IndicatorPos = indicatorPos;
    }

    /// <summary>Advance on SpeechEnded: probe below the paragraph just
    /// read for the next paragraph; speak it, or end the chain when
    /// there is none (end of page) or the speak fails. No
    /// ConfigureAwait(false) here on purpose: the probe runs on a
    /// background thread (Task.Run), but the continuation — the speak
    /// callback — must resume on the caller's (UI) thread, because App's
    /// speech service is driven from there.</summary>
    public async Task AdvanceAsync()
    {
        if (!_chain.IsActive)
            return;
        // UIA probing can block (a slow provider): run it off the UI
        // thread. A fresh automation per probe follows the monitors'
        // pattern (they build one inside their Task.Run loop) and avoids
        // reusing a COM object across threads.
        var nextText = await Task.Run(ProbeNextParagraphText);
        // The user may have stopped the chain while the probe ran
        // (Cancel from StopFloatSession): never speak a chunk for a
        // cancelled chain, and never fire the ended callback for one
        // App already cleaned up.
        if (!_chain.IsActive)
            return;
        // No new paragraph below: the page is done — end the chain and
        // let App run the normal session cleanup.
        if (nextText is null)
        {
            EndChain();
            return;
        }
        // Speak the next paragraph in the chain language. A failed
        // speak ends the chain the same way (the session is over
        // either way); a started one keeps the chain active, and the
        // next SpeechEnded advances again.
        var spoken = await _speak(nextText, _chain.Language);
        if (!spoken)
            EndChain();
    }

    /// <summary>End the chain without speaking further (user stopped,
    /// a new different-text session started, Fast Read opened, ...).
    /// A no-op when already idle — safe to call from every stop path.
    /// Never fires the ended callback: those paths clean up themselves.</summary>
    public void Cancel()
    {
        _chain.Cancel();
    }

    /// <summary>End the chain on its own (end of page / speak failed)
    /// and tell App to run the normal session cleanup. Only fires when
    /// the chain was active — a cancelled chain's App already did it.</summary>
    private void EndChain()
    {
        if (!_chain.IsActive)
            return;
        _chain.Cancel();
        _chainEnded();
    }

    /// <summary>Find the next paragraph's text to speak, or null when
    /// there is none below. Runs on a background thread (see
    /// AdvanceAsync). Probes below the current paragraph's rect at the
    /// increasing offsets until a DIFFERENT paragraph turns up; a point
    /// with no paragraph (or the same paragraph again) just moves to
    /// the next offset. AcceptCandidate records the new paragraph as
    /// current when it accepts it.</summary>
    private string? ProbeNextParagraphText()
    {
        var current = _chain.CurrentParagraph;
        if (current is null)
            return null;
        using var automation = new UIA3Automation();
        foreach (var point in ContinuousListenChain.NextProbePoints(current.Rect, _getDpiScale()))
        {
            var found = TryGetParagraphAt(automation, point);
            // Chain diagnosis (CI272: Word select reads only the
            // sentence): log each probe point — a ladder that finds
            // nothing means the next paragraph is not below the current
            // rect (or the provider gives no paragraph there).
            WindowDiagnostics.AppendLine(
                $"[Chain] probe point=({point.X},{point.Y}) found={(found is not null ? $"len={found.Text.Length}" : "null")}");
            // AcceptCandidate returns the text when this is a NEW
            // paragraph (and records it as current); null means same
            // paragraph / no paragraph here — try the next offset down.
            if (found is not null && _chain.AcceptCandidate(found) is { } text)
                return text;
        }
        WindowDiagnostics.AppendLine(
            $"[Chain] advance none rect=({current.Rect.X},{current.Rect.Y},{current.Rect.Width}x{current.Rect.Height})");
        return null;
    }

    /// <summary>One point's paragraph, same pattern as
    /// BlockTextHoverService.TryGetParagraph: FromPoint ->
    /// TextPattern.RangeFromPoint -> ExpandToEnclosingUnit(Paragraph)
    /// -> GetText + GetBoundingRectangles (union). NO DocumentRange /
    /// ValuePattern / Name fallbacks: those are for single controls,
    /// not document paragraphs — a failed paragraph probe here means
    /// "no paragraph at this point", and the chain tries the next
    /// offset (or ends). Every step is guarded; null = no paragraph.</summary>
    private static TextParagraph? TryGetParagraphAt(UIA3Automation automation, (int X, int Y) point)
    {
        try
        {
            var element = automation.FromPoint(new Point(point.X, point.Y));
            if (element is null)
                return null;
            // Own windows must never feed the chain: the Reader is ours
            // (the alternative icon owns Reader selections and does not
            // chain), and the panel/icon could overlap the page below
            // the paragraph. Fail CLOSED on pid 0 (unknown) here — unlike
            // the monitors' fail-open: a missed next paragraph just ends
            // the chain gracefully, while chaining into our own UI text
            // would read the wrong app aloud.
            var pid = element.Properties.ProcessId.ValueOrDefault;
            if (pid == 0 || pid == Environment.ProcessId)
                return null;
            var pattern = element.Patterns.Text.PatternOrDefault;
            var range = pattern?.RangeFromPoint(new Point(point.X, point.Y));
            if (range is null)
                return null;
            try
            {
                range.ExpandToEnclosingUnit(TextUnit.Paragraph);
            }
            catch
            {
                // Expand failed (provider has no Paragraph unit): the
                // range stays as-is, and its text is still the text at
                // the point — better than nothing, but only usable when
                // it is a real paragraph (checked by the caller through
                // IsNewParagraph, and the rect must be non-empty).
            }
            var text = range.GetText(-1);
            if (string.IsNullOrWhiteSpace(text))
                return null;
            var rect = UnionRects(range.GetBoundingRectangles());
            if (rect.IsEmpty)
                return null;
            return new TextParagraph(text.Trim(), rect);
        }
        catch
        {
            // Any UIA failure is swallowed: a broken accessibility tree
            // must never crash the chain (or the app) — the chain just
            // tries the next offset, or ends.
            return null;
        }
    }

    /// <summary>Union of a range's line rects = the range's rect (the
    /// same helper as BlockTextHoverService.UnionRects, mirrored here
    /// so the working hover service is left untouched). Empty when the
    /// provider returns no usable rects.</summary>
    private static Rectangle UnionRects(Rectangle[]? rects)
    {
        var union = Rectangle.Empty;
        if (rects is null)
            return union;
        foreach (var rect in rects)
        {
            if (!rect.IsEmpty)
                union = union.IsEmpty ? rect : Rectangle.Union(union, rect);
        }
        return union;
    }
}
