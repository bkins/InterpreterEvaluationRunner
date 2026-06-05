using System.Text;
using System.Text.Json;
using InterpreterEvaluationRunner.Interpreter.Pipeline.Evaluation;
using InterpreterEvaluationRunner.Interpreter.Pipeline.Models;
using Microsoft.Extensions.Configuration;
using Spectre.Console;

namespace InterpreterEvaluationRunner;

public class ResultExporter
{
    private readonly IConfiguration _configuration;

    public ResultExporter(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task ExportAsync(List<EvaluationResult> results)
    {
        Directory.CreateDirectory("Output");

        var json = JsonSerializer.Serialize(results
                                           , new JsonSerializerOptions
                                             {
                                                     WriteIndented = true
                                             });

        var fileName = $"Output/results-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";

        await File.WriteAllTextAsync(fileName, json);

        AnsiConsole.MarkupLine($"\nResults exported to {fileName}");

        var reportDir = _configuration["Evaluation:ReportOutputDir"];

        if (!string.IsNullOrWhiteSpace(reportDir))
        {
            Directory.CreateDirectory(reportDir);

            var reportPath = Path.Combine(reportDir, $"eval-{DateTime.UtcNow:yyyy-MM-dd}.txt");

            // Open with FileShare.ReadWrite so Tee-Object's read handle on the same
            // file does not cause an IOException.
            await using var fs = new FileStream(reportPath
                                              , FileMode.Create
                                              , FileAccess.Write
                                              , FileShare.ReadWrite);
            await using var sw = new StreamWriter(fs, Encoding.UTF8);
            await sw.WriteAsync(BuildTextReport(results));

            AnsiConsole.MarkupLine($"Text report saved to {reportPath}");
        }
    }

    private static string BuildTextReport(List<EvaluationResult> results)
    {
        var sb = new StringBuilder();
        var now = DateTime.UtcNow;

        sb.AppendLine("INTERPRETER EVALUATION RUNNER — RESULTS");
        sb.AppendLine($"Generated : {now:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine(new string('=', 80));
        sb.AppendLine();

        var grouped = results.GroupBy(r => r.ModelName).ToList();

        sb.AppendLine("MODEL SUMMARY");
        sb.AppendLine(new string('-', 80));

        var headerFmt = "{0,-20} {1,9} {2,7} {3,9} {4,11} {5,10} {6,9} {7,8} {8,8}";
        sb.AppendLine(string.Format(headerFmt
                                  , "Model", "AvgScore", "Tests", "JSON Fail"
                                  , "IntentFail", "ParamFail", "FailType", "Timeouts", "Tok/s"));
        sb.AppendLine(new string('-', 80));

        foreach (var group in grouped)
        {
            var list        = group.ToList();
            var avgScore    = list.Average(r => r.Score);
            var avgToks     = list.Where(r => r.TokensPerSecond.HasValue).Select(r => r.TokensPerSecond!.Value).ToList();
            var toksDisplay = avgToks.Count > 0 ? avgToks.Average().ToString("F1") : "—";

            sb.AppendLine(string.Format(headerFmt
                                      , group.Key
                                      , avgScore.ToString("F1")
                                      , list.Count
                                      , list.Count(r => r.FailureCategories.Contains(FailureCategory.JsonParseFailure))
                                      , list.Count(r => r.FailureCategories.Contains(FailureCategory.WrongIntent))
                                      , list.Count(r => r.FailureCategories.Contains(FailureCategory.ParameterMismatch))
                                      , list.Count(r => r.FailureCategories.Contains(FailureCategory.WrongFailureType))
                                      , list.Count(r => r.FailureCategories.Contains(FailureCategory.Timeout))
                                      , toksDisplay));
        }

        sb.AppendLine();
        sb.AppendLine(new string('=', 80));
        sb.AppendLine();

        foreach (var group in grouped)
        {
            sb.AppendLine($"MODEL: {group.Key}");
            sb.AppendLine(new string('-', 60));

            var rowFmt = "  {0,-40} {1,5} {2,7} {3,9} {4,9}";
            sb.AppendLine(string.Format(rowFmt, "Test", "Score", "Parsed", "Action", "Params"));
            sb.AppendLine("  " + new string('-', 72));

            foreach (var r in group)
            {
                sb.AppendLine(string.Format(rowFmt
                                          , r.TestName.Length > 40 ? r.TestName[..40] : r.TestName
                                          , r.Score
                                          , r.JsonParsedSuccessfully ? "yes" : "no"
                                          , r.ActionWasCorrect       ? "correct" : "wrong"
                                          , r.ParametersWereCorrect  ? "correct" : "wrong"));

                if (r.Failures.Count > 0)
                {
                    foreach (var f in r.Failures)
                        sb.AppendLine($"      ! {f}");
                }
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}
