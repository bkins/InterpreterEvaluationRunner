using System.Text.Json;
using InterpreterEvaluationRunner.Interpreter.Pipeline.Evaluation;

namespace InterpreterEvaluationRunner;

public class ResultExporter
{
    public async Task ExportAsync( List<EvaluationResult> results)
    {
        Directory.CreateDirectory("Output");

        var json = JsonSerializer.Serialize(results
                                           , new JsonSerializerOptions
                                             {
                                                     WriteIndented = true
                                             });

        var fileName = $"Output/results-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";

        await File.WriteAllTextAsync(fileName, json);

        Console.WriteLine($"\nResults exported to {fileName}");
    }
}