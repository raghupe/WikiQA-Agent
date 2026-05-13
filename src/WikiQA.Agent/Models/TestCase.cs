namespace WikiQA.Agent.Models;

public record TestCase(
    string Id,
    string Category,
    string Query,
    IReadOnlyList<string> ExpectedKeywords,
    IReadOnlyList<string> AnyKeywords,
    IReadOnlyList<string> ProhibitedKeywords,
    IReadOnlyList<string> ExpectedReferenceUrls,
    IReadOnlyList<string> AnyReferencedUrls,
    bool IsWikiUseExpected,
    int MinExpectedSearchCount
);
