using System.Text.Json;
using System.Text.Json.Serialization;
using WikiQA.Agent.Models;

namespace WikiQA.Agent.Transcript;

public class EvalResultWriter(string evalResultsDirectory)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task WriteAsync(EvalRunResult result, string suiteName)
    {
        Directory.CreateDirectory(evalResultsDirectory);
        var fileName = $"{suiteName}_{result.RunId}.json";
        var path = Path.Combine(evalResultsDirectory, fileName);
        var json = JsonSerializer.Serialize(result, JsonOptions);
        await File.WriteAllTextAsync(path, json);
    }
}
