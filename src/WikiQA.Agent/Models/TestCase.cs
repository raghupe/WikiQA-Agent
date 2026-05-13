namespace WikiQA.Agent.Models;

public record TestCase(
    string Id,
    string Category,
    string Query,
    IReadOnlyList<string> ExpectedKeywords,
    IReadOnlyList<string> AnyReferencedUrls
);
