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

    public async Task ExportAsync( List<EvaluationResult> results
                                 , string                 totalTimeToComplete )
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

            var reportPath = Path.Combine(reportDir, $"eval-{DateTime.UtcNow:yyyy-MM-dd HH-mm-ss}.txt");

            // Open with FileShare.ReadWrite so Tee-Object's read handle on the same
            // file does not cause an IOException.
            await using var fileStream = new FileStream(reportPath
                                                      , FileMode.Create
                                                      , FileAccess.Write
                                                      , FileShare.ReadWrite);
            
            await using var streamWriter = new StreamWriter(fileStream, Encoding.UTF8);
            
            await streamWriter.WriteAsync(BuildTextReport(results, totalTimeToComplete));

            AnsiConsole.MarkupLine($"Text report saved to {reportPath}");
        }
    }

    private static string BuildTextReport( List<EvaluationResult> results
                                         , string                 totalTimeToComplete )
    {
        var sb = new StringBuilder();
        var now = DateTime.UtcNow;

        sb.AppendLine("INTERPRETER EVALUATION RUNNER — RESULTS");
        sb.AppendLine($"Date : {now:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine(new string('=', 80));
        sb.AppendLine();

        var grouped = results.GroupBy(result => result.ModelName).ToList();

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
            var avgScore    = list.Average(result => result.Score);
            var avgToks     = list.Where(result => result.TokensPerSecond.HasValue)
                                  .Select(result => result.TokensPerSecond!.Value)
                                  .ToList();
            var toksDisplay = avgToks.Count > 0 
                                      ? avgToks.Average().ToString("F1") 
                                      : "—";

            sb.AppendLine(string.Format(headerFmt
                                      , group.Key
                                      , avgScore.ToString("F1")
                                      , list.Count
                                      , list.Count(result => result.FailureCategories.Contains(FailureCategory.JsonParseFailure))
                                      , list.Count(result => result.FailureCategories.Contains(FailureCategory.WrongIntent))
                                      , list.Count(result => result.FailureCategories.Contains(FailureCategory.ParameterMismatch))
                                      , list.Count(result => result.FailureCategories.Contains(FailureCategory.WrongFailureType))
                                      , list.Count(result => result.FailureCategories.Contains(FailureCategory.Timeout))
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

            foreach (var result in group)
            {
                sb.AppendLine(string.Format(rowFmt
                                          , result.TestName.Length > 40 
                                                    ? result.TestName[..40] 
                                                    : result.TestName
                                          , result.Score
                                          , result.JsonParsedSuccessfully ? "yes" : "no"
                                          , result.ActionWasCorrect       ? "correct" : "wrong"
                                          , result.ParametersWereCorrect  ? "correct" : "wrong"));

                if (result.Failures.Count <= 0) continue;
                
                foreach (var failure in result.Failures)
                    sb.AppendLine($"      ! {failure}");
            }

            sb.AppendLine();
        }
        
        sb.AppendLine($"Total time to complete: {totalTimeToComplete}");

        return sb.ToString();
    }
}
