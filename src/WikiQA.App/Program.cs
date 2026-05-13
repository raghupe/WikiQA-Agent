using WikiQA.Evals;
using WikiQA.Agent.Agent;
using WikiQA.Agent.Models;
using WikiQA.Agent.Prompts;
using WikiQA.Agent.Transcript;

var transcriptLogger = new TranscriptLoggerProvider();
var transcriptWriter = new TranscriptWriter(
    Path.Combine(Directory.GetCurrentDirectory(), "transcripts"));
var evalResultWriter = new EvalResultWriter(
    Path.Combine(Directory.GetCurrentDirectory(), "eval-results"));
var promptBuilder = new PromptBuilder(Path.Combine(AppContext.BaseDirectory, "Prompts"));

// ── Eval mode ────────────────────────────────────────────────
if (args is ["--eval", var suiteName])
{
    var agent = new WikipediaAgent(promptBuilder, transcriptLogger, transcriptWriter);
    var executor = new EvalExecutor(
        Path.Combine(AppContext.BaseDirectory, "Suites"),
        agent,
        evalResultWriter,
        promptBuilder);

    Console.WriteLine($"Suite  : {suiteName}");
    Console.WriteLine();

    var run = await executor.RunAsync(suiteName, onResult: r =>
    {
        Console.ForegroundColor = r.Status switch
        {
            EvalStatus.Passed     => ConsoleColor.Green,
            EvalStatus.Incomplete => ConsoleColor.Yellow,
            _                     => ConsoleColor.Red
        };
        Console.WriteLine($"  [{r.Status.ToString().ToUpper()}] {r.CaseId}: {r.Query}");
        Console.ResetColor();
        if (r.JudgeMetrics.Count > 0)
        {
            Console.WriteLine("       Judge Metrics (pass@3):");
            foreach (var m in r.JudgeMetrics)
                Console.WriteLine($"         {m.Name,-16}: {m.Mean:F2} [{m.LowerBound:F2}, {m.UpperBound:F2}]  PassAtK={m.PassAtK}");
        }
        if (r.SafetyMetrics.Count > 0)
        {
            Console.WriteLine("       Safety Metrics (pass@3, strict):");
            foreach (var m in r.SafetyMetrics)
            {
                var icon = m.PassAtK ? "✓" : "✗";
                var results = string.Join(",", m.Results.Select(b => b ? "T" : "F"));
                Console.WriteLine($"         {icon} {m.Name,-22}: [{results}]");
            }
        }
        return Task.CompletedTask;
    });

    Console.WriteLine();
    Console.WriteLine("─────────────────────────────────────");
    Console.WriteLine($"  Total      : {run.Results.Count}");
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"  Pass       : {run.PassCount}");
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"  Fail       : {run.FailCount}");
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"  Incomplete : {run.IncompleteCount}");
    Console.ResetColor();
    var pct = run.Results.Count > 0 ? run.PassCount * 100.0 / run.Results.Count : 0;
    Console.WriteLine($"  Score      : {pct:F1}%");
    Console.WriteLine("─────────────────────────────────────");
    return;
}

// ── Demo mode ─────────────────────────────────────────────────
Console.WriteLine("WikiQA Agent");
Console.WriteLine("============");
Console.Write("Enter your question: ");
var query = Console.ReadLine() ?? string.Empty;

var response = await new WikipediaAgent(promptBuilder, transcriptLogger, transcriptWriter).AnswerAsync(query);
Console.WriteLine($"\nAnswer: {response.Answer}");
