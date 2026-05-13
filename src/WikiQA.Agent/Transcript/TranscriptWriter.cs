using System.Text.Json;
using System.Text.Json.Serialization;
using WikiQA.Agent.Models;

namespace WikiQA.Agent.Transcript;

public class TranscriptWriter(string transcriptsDirectory)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task WriteAsync(Models.Transcript transcript)
    {
        Directory.CreateDirectory(transcriptsDirectory);
        var path = Path.Combine(transcriptsDirectory, $"{transcript.CorrelationId}.json");
        var json = JsonSerializer.Serialize(transcript, JsonOptions);
        await File.WriteAllTextAsync(path, json);
    }
}
