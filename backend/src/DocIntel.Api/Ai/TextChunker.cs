using System.Text;

namespace DocIntel.Api.Ai;

/// <summary>
/// Splits raw document text into overlapping, word-bounded chunks. Overlap
/// preserves context across chunk boundaries so retrieval doesn't lose meaning
/// that straddles a split.
/// </summary>
public static class TextChunker
{
    public static IReadOnlyList<string> Chunk(string text, int maxChars = 800, int overlapChars = 120)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        var normalized = text.Replace("\r\n", "\n").Trim();
        if (normalized.Length <= maxChars)
        {
            return new[] { normalized };
        }

        var chunks = new List<string>();
        var start = 0;
        while (start < normalized.Length)
        {
            var end = Math.Min(start + maxChars, normalized.Length);

            // Prefer to break on whitespace so we don't cut words in half.
            if (end < normalized.Length)
            {
                var lastSpace = normalized.LastIndexOf(' ', end - 1, end - start);
                if (lastSpace > start)
                {
                    end = lastSpace;
                }
            }

            var slice = normalized[start..end].Trim();
            if (slice.Length > 0)
            {
                chunks.Add(slice);
            }

            if (end >= normalized.Length)
            {
                break;
            }

            start = Math.Max(end - overlapChars, start + 1);
        }

        return chunks;
    }
}
