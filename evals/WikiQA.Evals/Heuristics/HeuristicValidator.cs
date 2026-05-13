using System.Text.RegularExpressions;
using WikiQA.Agent.Models;

namespace WikiQA.Evals.Heuristics;

public class HeuristicValidator
{
    private static readonly Regex WikipediaUrlPattern =
        new(@"^https://en\.wikipedia\.org/wiki/.+$", RegexOptions.Compiled);

    public HeuristicResult ValidateWikipediaUrls(IReadOnlyList<string> urls)
    {
        if (urls.Count == 0)
            return new HeuristicResult("WikipediaUrlValidation", true, "No URLs to validate.");

        var invalid = urls.Where(u => !WikipediaUrlPattern.IsMatch(u)).ToList();
        return invalid.Count == 0
            ? new HeuristicResult("WikipediaUrlValidation", true, $"All {urls.Count} URLs are valid Wikipedia URLs.")
            : new HeuristicResult("WikipediaUrlValidation", false, $"Invalid URLs: {string.Join(", ", invalid)}");
    }

    public HeuristicResult ValidateWordOverlap(string answer, IReadOnlyList<WikipediaResult> sources)
    {
        if (sources.Count == 0)
            return new HeuristicResult("WordOverlap", true, "No sources to validate against.");

        var answerWords = Tokenize(answer);
        var snippetWords = sources.SelectMany(s => Tokenize(s.Snippet)).ToHashSet();
        var overlap = answerWords.Intersect(snippetWords).Count();

        return overlap >= 10
            ? new HeuristicResult("WordOverlap", true, $"Word overlap: {overlap} words.")
            : new HeuristicResult("WordOverlap", false, $"Insufficient word overlap: {overlap} (minimum 10).");
    }

    private static HashSet<string> Tokenize(string text) =>
        Regex.Split(text.ToLowerInvariant(), @"[^a-z0-9]+")
             .Where(w => w.Length > 2)
             .ToHashSet();
}
