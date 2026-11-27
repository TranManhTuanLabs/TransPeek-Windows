// A paragraph of text as UI Automation sees it — the unit continuous
// listen (chaining, Manh 2026-11-27) reads and probes below. The text
// is the paragraph's full text (from TextPattern, not capped — a chain
// reads whole paragraphs); the rect is the union of its line rects
// (physical pixels), used to probe for the next paragraph below it.
// Pure value type: no UIA here, so Core tests can build it directly.

using System.Drawing;

namespace TransPeek.Core.ContinuousListen;

/// <summary>A paragraph's text + screen rect, as returned by a UIA
/// paragraph probe (RangeFromPoint + ExpandToEnclosingUnit(Paragraph)
/// + GetBoundingRectangles). The rect is the union of the paragraph's
/// line rects — the anchor for probing the next paragraph below.</summary>
public sealed record TextParagraph(string Text, Rectangle Rect);
