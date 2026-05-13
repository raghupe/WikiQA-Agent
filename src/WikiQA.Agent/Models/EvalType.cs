namespace WikiQA.Agent.Models;

[Flags]
public enum EvalType
{
    Basic         = 1,
    Deterministic = 2,
    Heuristic     = 4,
    LLMJudge      = 8,
    Safety        = 16
}
