// RSVP speed-reading engine (Fast Read, feature/fast-read-rsvp).
// Pure logic — no UI, unit-tested in TransPeek.Tests. Patterns from
// docs/RESEARCH/tts-speechify-naturalreader/rsvp-report.md §6.1
// (SwiftRead legacy): chunk 1-6 (default 1), WPM default 275 max 2000,
// micro-pause multipliers — number 0.5, punctuation 0.9, sentence end
// 1.1, long word 0.5, paragraph end 1.1 — ADDED to the base delay
// (60000/WPM per word), switched off when chunk x WPM <= 100; minus
// 30 ms execution overhead per slide. ORP (optimal recognition point)
// pivot = middle character, floor(n/2), highlighted orange #F6861F
// (SwiftRead). Context +/-150 words on pause.

namespace TransPeek.Core.FastRead;

/// <summary>One RSVP slide: a chunk of 1-6 words shown together.</summary>
public sealed record RsvpSlide(
    string Text,
    IReadOnlyList<string> Words,
    int StartWordIndex,
    int PivotCharIndex,
    TimeSpan Delay,
    bool IsParagraphEnd);

/// <summary>A tokenized text: words + slides + total duration.</summary>
public sealed record RsvpDocument(
    IReadOnlyList<string> Words,
    IReadOnlyList<RsvpSlide> Slides,
    TimeSpan TotalDuration)
{
    public int TotalWords => Words.Count;
    public int TotalSlides => Slides.Count;

    /// <summary>Time left from the given slide to the end (inclusive).</summary>
    public TimeSpan RemainingDuration(int slideIndex)
    {
        if (Slides.Count == 0 || slideIndex >= Slides.Count)
            return TimeSpan.Zero;
        var total = TimeSpan.Zero;
        for (var i = Math.Clamp(slideIndex, 0, Slides.Count - 1); i < Slides.Count; i++)
            total += Slides[i].Delay;
        return total;
    }

    /// <summary>Words around the given slide (radius each side) for the
    /// pause context: the words, the index of the first current word
    /// inside them, and how many words are current.</summary>
    public RsvpContext Context(int slideIndex, int radius = RsvpEngine.DefaultContextRadius)
    {
        if (Slides.Count == 0)
            return new RsvpContext([], 0, 0);
        var index = Math.Clamp(slideIndex, 0, Slides.Count - 1);
        var slide = Slides[index];
        var from = Math.Max(0, slide.StartWordIndex - radius);
        var to = Math.Min(Words.Count, slide.StartWordIndex + slide.Words.Count + radius);
        var words = Words.Skip(from).Take(to - from).ToList();
        return new RsvpContext(words, slide.StartWordIndex - from, slide.Words.Count);
    }
}

/// <summary>Pause context: the surrounding words, where the current
/// slide starts inside them, and how many words it covers.</summary>
public sealed record RsvpContext(
    IReadOnlyList<string> Words,
    int CurrentStart,
    int CurrentCount);

/// <summary>One continuous-reading phrase: a sentence or clause
/// spoken as a single utterance (continuous reading, phương án 1 —
/// Manh 2026-11-21). Windows.Media.SpeechSynthesis has no per-word
/// callback (unlike mac's willSpeakRangeOfSpeechString), so the
/// focus follows the voice phrase by phrase, not word by word — a
/// phrase ends at a natural pause (sentence end / clause
/// punctuation), so the gaps between utterances land where a
/// speaker would pause anyway. StartWordIndex is the phrase's first
/// word in the whole text (for mapping the phrase back to its
/// slide / line). Top-level on purpose, next to the other records —
/// nested inside RsvpEngine the view model could not use the bare
/// name (CS0246, CI #224).</summary>
public sealed record RsvpPhrase(string Text, int StartWordIndex, int WordCount);

public static class RsvpEngine
{
    public const int DefaultWpm = 275;
    public const int MinWpm = 50;
    public const int MaxWpm = 2000;
    public const int DefaultChunkSize = 1;
    public const int MinChunkSize = 1;
    public const int MaxChunkSize = 6;
    public const int DefaultContextRadius = 150;
    /// <summary>A word longer than this gets the long-word pause.</summary>
    public const int LongWordLength = 13;
    /// <summary>Micro-pauses are off when chunk x WPM is at or below
    /// this (slow reading needs no extra pauses).</summary>
    public const int MicroPauseOffThreshold = 100;
    /// <summary>Execution overhead subtracted from every slide's delay
    /// (SwiftRead legacy: the timer + render take ~30 ms).</summary>
    public const double ExecutionOverheadMs = 30;
    public const double MinSlideMs = 10;

    /// <summary>Tokenize text into slides. Paragraphs are runs of
    /// non-empty lines (blank lines separate); a chunk breaks early at
    /// a sentence end or a paragraph end so a sentence is never cut
    /// mid-way. WPM and chunk size are clamped to their ranges.</summary>
    public static RsvpDocument Build(string? text, int wpm, int chunkSize)
    {
        wpm = Math.Clamp(wpm, MinWpm, MaxWpm);
        chunkSize = Math.Clamp(chunkSize, MinChunkSize, MaxChunkSize);
        var paragraphs = SplitParagraphs(text);
        var words = paragraphs.SelectMany(p => p).ToList();
        var slides = new List<RsvpSlide>();
        var total = TimeSpan.Zero;
        var index = 0;
        foreach (var paragraph in paragraphs)
        {
            var i = 0;
            while (i < paragraph.Count)
            {
                var chunk = new List<string>();
                var paragraphEnd = false;
                while (i < paragraph.Count && chunk.Count < chunkSize)
                {
                    var word = paragraph[i];
                    chunk.Add(word);
                    i++;
                    paragraphEnd = i == paragraph.Count;
                    if (EndsSentence(word) || paragraphEnd)
                        break;
                }
                var delay = ComputeSlideDelay(chunk, paragraphEnd, wpm, chunkSize);
                var slideText = string.Join(" ", chunk);
                // ORP highlight (SwiftRead, like mac): the middle word
                // of the chunk (floor(n/2)) carries the pivot — its
                // middle character, as an offset into the joined text
                // — so the orange highlight never disappears at chunk
                // 2+. (Was: pivot -1 for multi-word, Sprint Reader
                // style — the highlight vanished, Manh 2026-11-19.)
                var pivotWordIndex = chunk.Count / 2;
                // Offset of the pivot word inside the joined text:
                // every earlier word plus its joining space.
                var pivotOffset = 0;
                for (var j = 0; j < pivotWordIndex; j++)
                    pivotOffset += chunk[j].Length + 1;
                var pivot = pivotOffset + PivotCharIndex(chunk[pivotWordIndex]);
                slides.Add(new RsvpSlide(slideText, chunk, index, pivot, delay, paragraphEnd));
                total += delay;
                index += chunk.Count;
            }
        }
        return new RsvpDocument(words, slides, total);
    }

    /// <summary>Style 2 ("basic reading") lines (feature/fastread-speaker-style2,
    /// mac parity: RSVPViewModel.lines(for:maxWordsPerLine: 12)): each
    /// paragraph split into short lines of at most 12 words, so the
    /// focus can advance line by line at a readable pace. A logical
    /// split, not the visual wrap (which depends on font size and
    /// window width); matching visual lines is a later refinement.</summary>
    public static IReadOnlyList<string> BuildLines(string? text, int maxWordsPerLine = 12)
    {
        var lines = new List<string>();
        foreach (var paragraph in SplitParagraphs(text))
        {
            for (var i = 0; i < paragraph.Count; i += maxWordsPerLine)
            {
                var count = Math.Min(maxWordsPerLine, paragraph.Count - i);
                lines.Add(string.Join(" ", paragraph.GetRange(i, count)));
            }
        }
        return lines;
    }

    /// <summary>Split text into speakable phrases for continuous
    /// reading: each paragraph is walked word by word; a phrase ends
    /// after a word ending a sentence (. ? !), a clause (, ; :), or the
    /// paragraph — and, as a safety cap, at MaxWordsPerPhrase words (a
    /// comma-less monster sentence still splits, so the focus keeps
    /// moving). The phrase text keeps the original words (punctuation
    /// attached), so the voice pauses naturally at sentence ends inside
    /// the utterance too. Pure logic — unit-tested.</summary>
    public static IReadOnlyList<RsvpPhrase> BuildPhrases(string? text, int maxWordsPerPhrase = 20)
    {
        maxWordsPerPhrase = Math.Max(1, maxWordsPerPhrase);
        var phrases = new List<RsvpPhrase>();
        var index = 0;
        foreach (var paragraph in SplitParagraphs(text))
        {
            var phrase = new List<string>();
            var phraseStart = index;
            foreach (var word in paragraph)
            {
                phrase.Add(word);
                index++;
                // A phrase ends at a natural pause — or at the cap, so
                // a long sentence without punctuation still splits.
                if (EndsSentence(word) || EndsPunctuation(word) || phrase.Count >= maxWordsPerPhrase)
                {
                    phrases.Add(new RsvpPhrase(string.Join(" ", phrase), phraseStart, phrase.Count));
                    phrase = [];
                    phraseStart = index;
                }
            }
            // The paragraph's tail (no trailing punctuation) is a
            // phrase too — a paragraph end is a pause, like a
            // sentence end.
            if (phrase.Count > 0)
                phrases.Add(new RsvpPhrase(string.Join(" ", phrase), phraseStart, phrase.Count));
        }
        return phrases;
    }

    /// <summary>ORP pivot: the middle character, floor(n/2). "hello" (5)
    /// -&gt; 2, "hi" (2) -&gt; 1, "a" (1) -&gt; 0. Empty word -&gt; -1.</summary>
    public static int PivotCharIndex(string word)
        => string.IsNullOrEmpty(word) ? -1 : word.Length / 2;

    /// <summary>Display time of one slide: base delay (60000/WPM) per
    /// word, plus the micro-pause factors of its words (number 0.5,
    /// punctuation 0.9, sentence end 1.1, long word 0.5, paragraph end
    /// 1.1 — additive), minus the 30 ms execution overhead, clamped to
    /// at least 10 ms. Micro-pauses are off when chunk x WPM &lt;= 100.</summary>
    public static TimeSpan ComputeSlideDelay(IReadOnlyList<string> words, bool paragraphEnd, int wpm, int chunkSize)
    {
        wpm = Math.Clamp(wpm, MinWpm, MaxWpm);
        var baseMs = 60000.0 / wpm;
        var microPauseOn = chunkSize * wpm > MicroPauseOffThreshold;
        var ms = 0.0;
        for (var i = 0; i < words.Count; i++)
        {
            var word = words[i];
            var extra = 0.0;
            if (microPauseOn)
            {
                if (IsNumber(word))
                    extra += 0.5;
                if (EndsPunctuation(word))
                    extra += 0.9;
                if (EndsSentence(word))
                    extra += 1.1;
                if (word.Length > LongWordLength)
                    extra += 0.5;
                // The paragraph-end pause belongs to the slide's last
                // word (which is the paragraph's last word).
                if (paragraphEnd && i == words.Count - 1)
                    extra += 1.1;
            }
            ms += baseMs * (1 + extra);
        }
        ms -= ExecutionOverheadMs;
        return TimeSpan.FromMilliseconds(Math.Max(MinSlideMs, ms));
    }

    /// <summary>Index of the slide containing the given word index
    /// (for keeping the reading position when WPM/chunk changes).</summary>
    public static int SlideIndexForWord(RsvpDocument document, int wordIndex)
    {
        if (document.Slides.Count == 0)
            return 0;
        wordIndex = Math.Clamp(wordIndex, 0, document.TotalWords - 1);
        for (var i = 0; i < document.Slides.Count; i++)
        {
            var slide = document.Slides[i];
            if (wordIndex >= slide.StartWordIndex
                && wordIndex < slide.StartWordIndex + slide.Words.Count)
                return i;
        }
        return document.Slides.Count - 1;
    }

    private static List<List<string>> SplitParagraphs(string? text)
    {
        var result = new List<List<string>>();
        if (string.IsNullOrWhiteSpace(text))
            return result;
        var current = new List<string>();
        foreach (var line in text.Split('\n'))
        {
            var words = SplitWords(line);
            if (words.Count == 0)
            {
                if (current.Count > 0)
                {
                    result.Add(current);
                    current = new List<string>();
                }
            }
            else
            {
                current.AddRange(words);
            }
        }
        if (current.Count > 0)
            result.Add(current);
        return result;
    }

    private static List<string> SplitWords(string line)
    {
        var words = new List<string>();
        var start = -1;
        for (var i = 0; i < line.Length; i++)
        {
            if (char.IsWhiteSpace(line[i]))
            {
                if (start >= 0)
                {
                    words.Add(line[start..i]);
                    start = -1;
                }
            }
            else if (start < 0)
            {
                start = i;
            }
        }
        if (start >= 0)
            words.Add(line[start..]);
        return words;
    }

    /// <summary>A number: digits with optional thousand/decimal
    /// separators ("2026", "1,000", "3.14"). "1st" is not a number.</summary>
    private static bool IsNumber(string word)
    {
        var hasDigit = false;
        foreach (var c in word)
        {
            if (char.IsDigit(c))
            {
                hasDigit = true;
                continue;
            }
            if (c is '.' or ',')
                continue;
            return false;
        }
        return hasDigit;
    }

    private static bool EndsPunctuation(string word)
        => word.EndsWith(',') || word.EndsWith(';') || word.EndsWith(':');

    private static bool EndsSentence(string word)
        => word.EndsWith('.') || word.EndsWith('?') || word.EndsWith('!');
}
