using WikiQA.Evals;
using WikiQA.Agent.Agent;
using WikiQA.Agent.Prompts;
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var agentLogger = loggerFactory.CreateLogger<WikipediaAgent>();

var promptBuilder = new PromptBuilder(Path.Combine(AppContext.BaseDirectory, "Prompts"));

if (args is ["--eval", var suiteName])
{
    var agent = new WikipediaAgent(promptBuilder, agentLogger, loggerFactory);
    var executor = new EvalExecutor(
        Path.Combine(AppContext.BaseDirectory, "Suites"),
        agent);
    var run = await executor.RunAsync(suiteName);
    Console.WriteLine($"Run ID : {run.RunId}");
    Console.WriteLine($"Suite  : {run.SuiteName}");
    Console.WriteLine($"Ran At : {run.RunAt:u}");
    Console.WriteLine($"Pass   : {run.PassCount}  Fail: {run.FailCount}");
    Console.WriteLine();
    foreach (var r in run.Results)
    {
        var status = r.Passed ? "PASS" : "FAIL";
        Console.WriteLine($"[{status}] {r.CaseId}: {r.Query}");
        Console.WriteLine($"       Response: {r.ActualAnswer}");
        if (r.ReferencedUrls.Count > 0)
            Console.WriteLine($"       Sources: {string.Join(", ", r.ReferencedUrls)}");
        foreach (var reason in r.FailureReasons)
            Console.WriteLine($"       {reason}");
    }
    return;
}

Console.WriteLine("WikiQA Agent");
Console.WriteLine("============");
Console.Write("Enter your question: ");
var query = Console.ReadLine() ?? string.Empty;

var response = await new WikipediaAgent(promptBuilder, agentLogger, loggerFactory).AnswerAsync(query);
Console.WriteLine($"\nAnswer: {response.Answer}");
