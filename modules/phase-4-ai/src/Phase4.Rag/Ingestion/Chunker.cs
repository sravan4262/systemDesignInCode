using System.Text;

namespace Phase4.Rag.Ingestion;

public record Chunk(int Index, string Heading, string Text);

// Splits a Markdown document into chunks small enough to embed and to fit many into one prompt.
// 1. Split by structure (headings), so one chunk = one topic.
// 2. A section longer than maxChars is cut into pieces that overlap, so a sentence on the cut isn't lost.
// 3. Every piece starts with its heading, so "30 days" still says WHAT is 30 days.
public class Chunker(int maxChars = 2000, int overlapChars = 200)   // ~500 tokens, ~50 tokens overlap
{
    public List<Chunk> Split(string markdown)
    {
        var chunks = new List<Chunk>();
        foreach (var (heading, body) in SplitByHeadings(markdown))
        {
            for (int start = 0; start < body.Length; start += maxChars - overlapChars)
            {
                var piece = body.Substring(start, Math.Min(maxChars, body.Length - start));
                chunks.Add(new(chunks.Count, heading, $"{heading}\n{piece}"));
                if (start + maxChars >= body.Length) break;   // last piece reached the end
            }
        }
        return chunks;
    }

    // "# Refunds\nYou can return..." → ("Refunds", "You can return...")
    private static IEnumerable<(string Heading, string Body)> SplitByHeadings(string markdown)
    {
        var heading = "";
        var body = new StringBuilder();
        foreach (var line in markdown.Split('\n'))
        {
            if (line.StartsWith('#'))
            {
                if (body.ToString().Trim() is { Length: > 0 } text) yield return (heading, text);
                heading = line.TrimStart('#', ' ').Trim();
                body.Clear();
            }
            else body.AppendLine(line);
        }
        if (body.ToString().Trim() is { Length: > 0 } last) yield return (heading, last);
    }
}
