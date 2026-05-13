namespace WikiQA.Agent.Prompts;

public class PromptBuilder(string promptsDirectory) : IPromptBuilder
{
    public string Load(string category, int version)
    {
        var path = Path.Combine(promptsDirectory, category, $"v{version}.txt");
        if (!File.Exists(path))
            throw new FileNotFoundException($"Prompt not found: {path}");
        return File.ReadAllText(path);
    }
}
