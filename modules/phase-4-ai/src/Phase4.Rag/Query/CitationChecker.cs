using System.Text.RegularExpressions;

namespace Phase4.Rag.Query;

public record Citation(int Number, string Title, string Heading, string Url);

// The model writes "[1]", "[2]"; we sent the sources, so we know what each number means.
// It can still invent "[7]" when there were only 5 sources: drop those, and map the rest to real links.
public class CitationChecker
{
    private static readonly Regex Marker = new(@"\[(\d+)\]");

    public (string Answer, List<Citation> Citations) Check(string answer, List<SearchHit> sources)
    {
        var used = new SortedSet<int>();
        var cleaned = Marker.Replace(answer, m =>
        {
            if (!int.TryParse(m.Groups[1].Value, out var n) || n < 1 || n > sources.Count) return "";   // invented
            used.Add(n);
            return m.Value;
        });

        var citations = used.Select(n => new Citation(n, sources[n - 1].Title, sources[n - 1].Heading, sources[n - 1].Url))
                            .ToList();
        return (cleaned, citations);
    }
}
