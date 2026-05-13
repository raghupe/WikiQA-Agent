namespace WikiQA.Agent.Models;

public record WikipediaResult(
    string Title,
    string Snippet,
    string PageUrl
);
