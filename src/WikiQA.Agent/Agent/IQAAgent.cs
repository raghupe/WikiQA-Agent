using WikiQA.Agent.Models;

namespace WikiQA.Agent.Agent;

public interface IQAAgent
{
    Task<AgentResponse> AnswerAsync(string query, string? correlationId = null);
}
