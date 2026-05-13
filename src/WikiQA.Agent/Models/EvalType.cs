namespace WikiQA.Agent.Models;

[Flags]
public enum EvalType
{
    Basic         = 1,
    Deterministic = 2,
    Heuristic     = 4
}
