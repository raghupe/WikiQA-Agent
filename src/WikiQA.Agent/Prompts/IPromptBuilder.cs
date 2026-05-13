namespace WikiQA.Agent.Prompts;

public interface IPromptBuilder
{
    string Load(string category, int version);
}
