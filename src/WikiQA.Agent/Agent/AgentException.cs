namespace WikiQA.Agent.Agent;

public class AgentException(string message, string transcriptPath, Exception inner)
    : Exception(message, inner)
{
    public string TranscriptPath { get; } = transcriptPath;
}
