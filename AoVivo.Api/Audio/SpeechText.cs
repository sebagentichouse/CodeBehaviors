using System.Text;
using System.Text.RegularExpressions;

namespace AoVivo.Api.Audio;

/// <summary>
/// Prepares the agent's text answer to be read aloud. The agent writes for a chat window (markdown, links,
/// web-search citations), which sounds bad when spoken: "open bracket apnews dot com ...".
/// </summary>
public static partial class SpeechText
{
    private const string EndPunctuation = ".!?:;,…";

    /// <summary>Removes citations, URLs and markdown, and ends every line with punctuation so the voice pauses.</summary>
    public static string ForSpeech(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        string cleaned = CitationMarker().Replace(text, string.Empty);   // 【4:0†source】
        cleaned = DomainLink().Replace(cleaned, string.Empty);           // [apnews.com](https://...) citations
        cleaned = MarkdownLink().Replace(cleaned, "$1");                 // [text](url) -> text
        cleaned = BareUrl().Replace(cleaned, string.Empty);
        cleaned = EmptyParentheses().Replace(cleaned, string.Empty);     // "( , )" left by removed citations

        var lines = new List<string>();
        foreach (string rawLine in cleaned.Split('\n'))
        {
            if (TableSeparator().IsMatch(rawLine))
                continue;

            string line = LineMarkup().Replace(rawLine, string.Empty);   // headings, quotes, bullets
            line = InlineMarkup().Replace(line, string.Empty);           // **bold**, *italic*, `code`, ~~strike~~
            line = TableCell().Replace(line, ", ").Trim(' ', ',', '\t', '\r');
            line = Spaces().Replace(line, " ");
            line = SpaceBeforePunctuation().Replace(line, "$1").Trim();

            if (line.Length == 0)
                continue;
            if (!EndPunctuation.Contains(line[^1]))
                line += ".";
            lines.Add(line);
        }

        return string.Join('\n', lines);
    }

    /// <summary>
    /// Splits <paramref name="text"/> into pieces of at most <paramref name="maxChars"/> characters, at sentence
    /// boundaries when possible (text-to-speech requests have an input limit).
    /// </summary>
    public static IReadOnlyList<string> Split(string text, int maxChars)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxChars, 1);

        var parts = new List<string>();
        var current = new StringBuilder();

        foreach (string rawSentence in SentenceBoundary().Split(text))
        {
            string sentence = rawSentence.Trim();
            while (sentence.Length > 0)
            {
                // A sentence longer than the limit is cut at the last space that fits.
                string piece;
                if (sentence.Length <= maxChars)
                {
                    piece = sentence;
                    sentence = string.Empty;
                }
                else
                {
                    int cut = sentence.LastIndexOf(' ', maxChars);
                    if (cut <= 0)
                        cut = maxChars;
                    piece = sentence[..cut].TrimEnd();
                    sentence = sentence[cut..].TrimStart();
                }

                if (piece.Length == 0)
                    continue;
                if (current.Length > 0 && current.Length + 1 + piece.Length > maxChars)
                {
                    parts.Add(current.ToString());
                    current.Clear();
                }
                if (current.Length > 0)
                    current.Append(' ');
                current.Append(piece);
            }
        }

        if (current.Length > 0)
            parts.Add(current.ToString());
        return parts;
    }

    [GeneratedRegex(@"【[^】]*】")]
    private static partial Regex CitationMarker();

    [GeneratedRegex(@"\[[^\]\s]+\.[^\]\s]+\]\([^)\s]*\)")]
    private static partial Regex DomainLink();

    [GeneratedRegex(@"\[([^\]]+)\]\([^)\s]*\)")]
    private static partial Regex MarkdownLink();

    [GeneratedRegex(@"https?://\S+")]
    private static partial Regex BareUrl();

    [GeneratedRegex(@"\(\s*[,;\s]*\)")]
    private static partial Regex EmptyParentheses();

    [GeneratedRegex(@"^\s*\|?[\s:|-]*-[\s:|-]*$")]
    private static partial Regex TableSeparator();

    [GeneratedRegex(@"^\s*(?:#{1,6}\s*|>\s?|[-*+•]\s+)")]
    private static partial Regex LineMarkup();

    [GeneratedRegex(@"\*\*|__|~~|[*`]")]
    private static partial Regex InlineMarkup();

    [GeneratedRegex(@"\s*\|\s*")]
    private static partial Regex TableCell();

    [GeneratedRegex(@"[ \t]+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"\s+([.,;:!?])")]
    private static partial Regex SpaceBeforePunctuation();

    [GeneratedRegex(@"(?<=[.!?…:;])\s+")]
    private static partial Regex SentenceBoundary();
}
