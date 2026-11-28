// Pure chain state + geometry for continuous listen (chaining, Manh
// 2026-11-27, word-start): after the click-to-listen first chunk ends,
// keep reading the page — probe UIA below the paragraph just read for
// the next paragraph, read it fully, repeat to the end of the page.
// Shared by hover-Speak and select-Speak (both enter via
// App.StartClickToListenAsync, like Mac where both set isChaining and
// share onEnded -> nextBlock). NO UIA / WinUI here — the app controller
// feeds it probe outcomes, it decides what to read next. Pure so the
// test project (which only references Core) can unit-test the probe
// offsets, the paragraph-change rule and the state transitions; the
// UIA probing itself lives in the app controller and is not tested on
// CI Linux.
//
// Word-start (Manh 2026-11-27 22:32): the first chunk is the EXACT
// selected text, not the sentence containing it. Mac reads from the
// sentence start because its highlight/overlay needs a sentence
// (playOffset + AX frames); Windows v1 has no overlay, so there is
// nothing to map — the exact word is the honest start. Do NOT add a
// sentence splitter here.

using System.Drawing;
using System.Text;

namespace TransPeek.Core.ContinuousListen;

/// <summary>Pure state + geometry for one continuous-listen chain: the
/// paragraph just read, the language (detected once, reused for the
/// whole chain), and whether the chain is still active. The app
/// controller owns the UIA probing and the speech; this class only
/// decides probe points and whether a probed paragraph is new.</summary>
public sealed class ContinuousListenChain
{
    // Next-paragraph probe offsets below the current paragraph's
    // bottom edge, in POINTS — Mac's ContinuousSpeakController
    // nextBlockProbeOffsets [4, 24, 48, 80] (step2-trial-plan.md
    // §367-380, ContinuousSpeakController.nextBlock). Points become
    // pixels at 1pt = 4/3px at 96 DPI (100%), scaled by the display's
    // DPI scale — so [4, 24, 48, 80]pt is [5, 32, 64, 107]px at 100%.
    // Four increasing offsets (not one): the gap to the next paragraph
    // varies (tight lines vs. a blank line vs. a heading), so the
    // probe climbs until a paragraph turns up or the ladder runs out
    // (end of page).
    private static readonly double[] NextParagraphProbeOffsetsPt = { 4, 24, 48, 80 };

    // Probe inset inside the paragraph rect's LEFT edge, in physical
    // pixels (not scaled with DPI — a few pixels inside the first
    // character is enough at any scale to clear the anti-aliased
    // edge). See NextProbePoints for why the probe sits here, not at
    // the centre.
    private const int LeftProbeInsetPx = 8;

    /// <summary>True while the chain is active (a first chunk was
    /// spoken with a paragraph context, and no end/stop has happened
    /// yet). False when idle, ended (end of page), or cancelled.</summary>
    public bool IsActive { get; private set; }

    /// <summary>The paragraph just read (the anchor for probing the
    /// next one below it), or null when the chain is idle.</summary>
    public TextParagraph? CurrentParagraph { get; private set; }

    /// <summary>Language detected once from the first chunk and reused
    /// for the whole chain (the page is in one language).</summary>
    public string Language { get; private set; } = "";

    /// <summary>Begin a chain after the first chunk was spoken: record
    /// the paragraph it came from (the probe anchor) and the language.
    /// The first chunk itself is spoken by the caller, not here.</summary>
    public void Start(TextParagraph paragraph, string language)
    {
        CurrentParagraph = paragraph;
        Language = language;
        IsActive = true;
    }

    /// <summary>Offer a probed candidate paragraph: when it is a NEW
    /// paragraph (see IsNewParagraph), record it as current and return
    /// its text to speak; otherwise return null and leave the current
    /// paragraph alone (the caller tries the next probe offset). A
    /// null candidate (no paragraph at that point) also returns null.</summary>
    public string? AcceptCandidate(TextParagraph? candidate)
    {
        if (!IsActive || candidate is null)
            return null;
        if (!IsNewParagraph(CurrentParagraph?.Text, candidate.Text))
            return null;
        CurrentParagraph = candidate;
        return candidate.Text;
    }

    /// <summary>End the chain without speaking further (user stopped,
    /// a new different-text session started, Fast Read opened, ...).
    /// A no-op when already idle — safe to call from every stop path.</summary>
    public void Cancel()
    {
        IsActive = false;
        CurrentParagraph = null;
        Language = "";
    }

    /// <summary>Probe points for the next paragraph, below the current
    /// paragraph's rect (physical pixels): just inside the rect's LEFT
    /// edge (8px in), at each offset below the bottom edge. Empty when
    /// the rect is empty (no anchor to probe from). <paramref
    /// name="dpiScale"/> is physical pixels per DIP (1.0 at 96 DPI /
    /// 100%): the point offsets are converted to pixels with it, so the
    /// ladder matches Mac at any scale.</summary>
    public static IReadOnlyList<(int X, int Y)> NextProbePoints(Rectangle paragraphRect, double dpiScale)
    {
        if (paragraphRect.IsEmpty)
            return Array.Empty<(int X, int Y)>();
        // Just inside the LEFT edge: the rect is the text's own union
        // (TryGetParagraphAt), so its left edge IS the text's left
        // edge — the lines below start there too (left-aligned text).
        // The centre (what this used to probe, and what Mac uses at
        // block.frame.midX) MISSES ragged text: log 274 proved it — a
        // 947px-wide line's centre (x=1006) is blank space on the
        // shorter lines below, so every probe returned null and the
        // chain never advanced. Clamped to the centre so a very narrow
        // rect never probes past its own middle.
        var x = Math.Min(paragraphRect.Left + LeftProbeInsetPx, paragraphRect.Left + paragraphRect.Width / 2);
        var points = new List<(int X, int Y)>(NextParagraphProbeOffsetsPt.Length);
        foreach (var offsetPt in NextParagraphProbeOffsetsPt)
        {
            // Points to pixels: 1pt = 1/72 inch, 1 inch = 96px at 96
            // DPI, so 1pt = 4/3px at scale 1.0 — rounded, because UIA
            // points are whole physical pixels.
            var offsetPx = (int)Math.Round(offsetPt * 4.0 / 3.0 * dpiScale);
            points.Add((x, paragraphRect.Bottom + offsetPx));
        }
        return points;
    }

    /// <summary>A candidate is a new paragraph only when it has real
    /// text and differs from the paragraph just read (whitespace-
    /// normalized — UIA text often carries trailing newlines/
    /// whitespace). Same text = the same paragraph re-found at a lower
    /// offset (or a repeated heading) — never re-read it; the caller
    /// tries the next offset instead. This is Mac's isSameBlock check
    /// (text + frame), reduced to text: the probe is already below the
    /// current rect, so a different rect is implied.</summary>
    public static bool IsNewParagraph(string? currentText, string? candidateText)
    {
        if (string.IsNullOrWhiteSpace(candidateText))
            return false;
        if (string.IsNullOrWhiteSpace(currentText))
            return true;
        return !string.Equals(NormalizeWhitespace(currentText), NormalizeWhitespace(candidateText), StringComparison.Ordinal);
    }

    /// <summary>Whether a captured paragraph context may start a chain
    /// for the text about to be spoken. The context must be real (text
    /// + rect) and must contain the spoken text — otherwise it is stale
    /// (a different selection/hover, or a Reader selection whose
    /// paragraph was never captured) and chaining would read the wrong
    /// page. A hovered block's text EQUALS its paragraph text; a
    /// selection is a SUBSTRING of its paragraph. When this is false,
    /// the caller reads just the text and stops (the old behaviour) —
    /// never chain on a guess.</summary>
    public static bool CanChain(string? paragraphText, Rectangle paragraphRect, string spokenText)
    {
        if (string.IsNullOrWhiteSpace(paragraphText) || paragraphRect.IsEmpty)
            return false;
        if (string.IsNullOrWhiteSpace(spokenText))
            return false;
        var paragraph = NormalizeWhitespace(paragraphText);
        var spoken = NormalizeWhitespace(spokenText);
        return string.Equals(paragraph, spoken, StringComparison.Ordinal)
            || paragraph.Contains(spoken, StringComparison.Ordinal);
    }

    // Whitespace-normalized text for comparisons: UIA returns the same
    // words with different whitespace per provider/range (Word uses
    // non-breaking spaces and \r line breaks; a selection range and
    // its paragraph range can disagree), so an exact ordinal compare
    // wrongly rejects a selection that IS inside its paragraph (log
    // 275: Word select 5 chars in a 58-char paragraph, canChain=False).
    // Collapsing every whitespace run to one plain space makes the
    // compare about words, not UIA whitespace artifacts.
    private static string NormalizeWhitespace(string text)
    {
        var sb = new StringBuilder(text.Length);
        var inWhitespace = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!inWhitespace)
                {
                    sb.Append(' ');
                    inWhitespace = true;
                }
            }
            else
            {
                sb.Append(c);
                inWhitespace = false;
            }
        }
        return sb.ToString().Trim();
    }
}
