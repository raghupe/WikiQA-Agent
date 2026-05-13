namespace WikiQA.Agent.Models;

public record AgentResponse(
    string Answer,
    int PromptVersion,
    IReadOnlyList<WikipediaResult> Sources
);
