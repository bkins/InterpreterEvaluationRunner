
using System.Diagnostics;
using System.Text.Json;
using ConsoleUtilities.Spinners;
using InterpreterEvaluationRunner.Interpreter.Pipeline.Models;
using InterpreterEvaluationRunner.Interpreter.Pipeline.Normalization;
using InterpreterEvaluationRunner.Interpreter.Pipeline.Repair.Engine;
using InterpreterEvaluationRunner.Interpreter.Pipeline.Validation;
using Microsoft.Extensions.Configuration;
using Spectre.Console;

namespace InterpreterEvaluationRunner.Interpreter.Pipeline.Evaluation;

public class EvaluationRunner : IEvaluationRunner
{
    /*
     * Technical Debt / Future Enhancements

    Scoring:
        - weighted semantic scoring
        - partial parameter credit
        - repair penalties
        - structural vs semantic subscores
        - confidence calibration scoring
    
    Benchmarking:
        - larger benchmark datasets
        - adversarial tests
        - ambiguity tests
        - hallucination tests
        - conversational memory tests
    
    Metrics:
        - confusion matrices
        - per-action accuracy
        - repair-rate metrics
        - latency distributions
        - validation error analytics
        - Evaluation Intelligence
        - candidate action ranking quality
        - clarification quality scoring
        - probabilistic scoring
        - threshold tuning
    
    Visualization:
        - charts
        - trend tracking
        - model comparisons over time
        - regression detection
     */
    
    /*
     * Next steps:
     * 1. Multi-turn clarification flow
     * 2. Context/memory
     * 3. Action generalization
     *
     * More specifically:
     *  | Priority | Area                        |
        | -------- | --------------------------- |
        | 1        | Multi-turn clarification    |
        | 2        | Conversation state          |
        | 3        | Context persistence         |
        | 4        | Action abstraction          |
        | 5        | Benchmark expansion         |
        | 6        | Identity conditioning       |
        | 7        | Training dataset generation |
        | 8        | Fine-tuning experiments     |

     */
    private readonly IModelClient          _modelClient;
    private readonly PromptBuilder         _promptBuilder;
    private readonly ResultScorer          _resultScorer;
    private readonly ResultExporter        _resultExporter;
    private readonly IInterpreterPipeline  _pipeline;
    private readonly IConfiguration        _configuration;

    public EvaluationRunner( IModelClient          modelClient
                           , PromptBuilder         promptBuilder
                           , NormalizationLayer    normalizationLayer
                           , ResultScorer          resultScorer
                           , IContractValidator    validator
                           , ResultExporter        resultExporter
                           , IResponseRepairEngine responseRepairEngine
                           , IInterpreterPipeline  pipeline
                           , IConfiguration        configuration )
    {
        _modelClient          = modelClient;
        _promptBuilder        = promptBuilder;
        _resultScorer         = resultScorer;
        _resultExporter       = resultExporter;
        _pipeline             = pipeline;
        _configuration        = configuration;
    }

    public async Task RunAsync()
    {
        var overallStopwatch = Stopwatch.StartNew();
        var testCases        = await LoadTestCasesAsync();

        // Phase 1 benchmark models — ordered by recommendation: primary, secondary, conditional, GPU-only, disqualified
        // Override via appsettings.json Evaluation:Models
        var models = _configuration.GetSection("Evaluation:Models").Get<string[]>()
                     ?? ["phi3:mini", "llama3.1:8b", "qwen2.5:7b", "qwen2.5:14b", "mistral"];

        var allResults = new List<EvaluationResult>();

        RenderHeader("INTERPRETER EVALUATION RUNNER");

        var modelsPanel = new Panel(string.Join(Environment.NewLine
                                              , models.Select(model => $"[cyan]-[/] {model}"))).Header("[bold yellow]MODELS[/]")
                                                                                               .Border(BoxBorder.Rounded)
                                                                                               .BorderColor(Color.Grey);

        AnsiConsole.Write(modelsPanel);

        foreach (var model in models)
        {
            RenderSectionHeader($"STARTING MODEL: {model}");

            var consecutiveFailures = 0;

            foreach (var testCase in testCases)
            {
                var result = await RunSingleTestAsync(model
                                                    , testCase);

                allResults.Add(result);

                consecutiveFailures = result.Score == 0
                                              ? consecutiveFailures + 1
                                              : 0;

                if (consecutiveFailures < 5) continue; 
                
                var skipPanelTitle = $"[bold red]Skipping model due to repeated failures[/]";
                AnsiConsole.Write(new Panel(skipPanelTitle).Header($"[red]{model}[/]")
                                                           .Border(BoxBorder.Heavy));

                break;
            }
        }

        PrintSummary(allResults);

        await _resultExporter.ExportAsync(allResults);

        overallStopwatch.Stop();

        var finalPanelTitle = $"[bold green]All tests completed in {overallStopwatch.Elapsed:hh\\:mm\\:ss}[/]";
        var finalPanel      = new Panel(finalPanelTitle).Border(BoxBorder.Double)
                                                        .BorderColor(Color.Green);;

        AnsiConsole.Write(finalPanel);
    }

    private async Task<EvaluationResult> RunSingleTestAsync( string             model
                                                           , EvaluationTestCase testCase )
    {
        var prompt    = _promptBuilder.BuildPrompt(testCase.UserInput);
        var stopwatch = Stopwatch.StartNew();

        RenderTestHeader(model
                       , testCase
                       , prompt.Length);

        GenerationResult generationResult;

        try
        {
            generationResult = await GenerateResponseAsync(model
                                                         , prompt
                                                         , testCase);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            return HandleException(model
                                 , testCase
                                 , ex);
        }

        var pipelineResult         = await _pipeline.ProcessAsync(generationResult.Text);
        var jsonParsedSuccessfully = pipelineResult.JsonParsedSuccessfully;
        var validationSucceeded    = pipelineResult.ValidationSucceeded;
        var response               = pipelineResult.Response;
        var parsed                 = pipelineResult.JsonParsedSuccessfully;

        RenderPipelineDetails(pipelineResult, response);

        var result = _resultScorer.Score(model
                                       , testCase
                                       , pipelineResult
                                       , stopwatch.ElapsedMilliseconds);

        result.PromptVersion   = _promptBuilder.Version;
        result.TokensPerSecond = generationResult.TokensPerSecond;

        stopwatch.Stop();

        RenderResultSummary(model
                          , testCase
                          , stopwatch.ElapsedMilliseconds
                          , parsed
                          , result
                          , pipelineResult.RepairResult.RepairedText);

        return result;
    }

    private async Task<GenerationResult> GenerateResponseAsync( string             model
                                                               , string             prompt
                                                               , EvaluationTestCase testCase )
    {
        return await SpinnerRunner.RunAsync($"Model: {model}"
                                          , async report =>
                                            {
                                                report($"Test: {testCase.Name}");

                                                var generationWatch = Stopwatch.StartNew();
                                                var result          = await _modelClient.GenerateAsync(model, prompt);

                                                generationWatch.Stop();

                                                var toksLabel = result.TokensPerSecond.HasValue
                                                                        ? $" @ {result.TokensPerSecond:F1} tok/s"
                                                                        : "";

                                                report($"Completed in {generationWatch.Elapsed:mm\\:ss}{toksLabel}");

                                                return result;
                                            }
                                          , Color.Cyan
                                          , $"Generating with {model}");
    }

    private void RenderTestHeader( string             model
                                 , EvaluationTestCase testCase
                                 , int                promptLength )
    {
        var grid = new Grid();

        grid.AddColumn();
        grid.AddColumn();

        grid.AddRow("[grey]Model[/]", $"[cyan]{Escape(model)}[/]");
        grid.AddRow("[grey]Test[/]", $"[yellow]{Escape(testCase.Name)}[/]");
        grid.AddRow("[grey]Prompt Length[/]", $"[green]{promptLength}[/]");
        grid.AddRow("[grey]Input[/]", Escape(testCase.UserInput));

        var panel = new Panel(grid).Header("[bold white]TEST CASE[/]")
                                   .Border(BoxBorder.Rounded)
                                   .BorderColor(Color.Blue);

        AnsiConsole.Write(panel);
    }

    private static void RenderPipelineDetails( PipelineResult            pipelineResult
                                             , ModelInterpreterResponse? response )
    {
        if (response != null)
        {
            var json = JsonSerializer.Serialize(response
                                              , new JsonSerializerOptions
                                                {
                                                        WriteIndented = true
                                                });

            var responsePanel = new Panel(new Markup(Escape(json))).Header("[green]DESERIALIZED RESPONSE[/]")
                                                                   .Border(BoxBorder.Rounded)
                                                                   .BorderColor(Color.Green);

            AnsiConsole.Write(responsePanel);
        }

        if (response == null) return;

        var validationResult = pipelineResult.ValidationResult;

        if (validationResult.IsValid) return;

        foreach (var validationPanel
                 in validationResult.Errors
                                    .Select(GetFormattedValidationError)
                                    .Select(errorText => new Panel(new Markup(errorText)).Header("[bold red]VALIDATION ERROR[/]")
                                                                                         .Border(BoxBorder.Heavy)
                                                                                         .BorderColor(Color.Red)))
        {
            AnsiConsole.Write(validationPanel);
        }
    }

    private static string GetFormattedValidationError( ValidationError error ) => $"""
                                                                                   [yellow]Property:[/] {Escape(error.PropertyName)}

                                                                                   [yellow]Message:[/] {Escape(error.ErrorMessage)}

                                                                                   [yellow]Attempted:[/] {Escape(error.AttemptedValue?.ToString() ?? "null")}
                                                                                   """;

    private void RenderResultSummary( string             model
                                    , EvaluationTestCase testCase
                                    , long               elapsedMilliseconds
                                    , bool               parsed
                                    , EvaluationResult   result
                                    , string             repairedResponse )
    {
        var failures = result.Failures.Count != 0
                               ? string.Join(Environment.NewLine
                                           , result.Failures.Select(f => $"[red]-[/] {Escape(f)}"))
                               : "[green]None[/]";

        var summary = $"""
                       [grey]Model:[/] {Escape(model)}
                       [grey]Test:[/] {Escape(testCase.Name)}
                       [grey]Latency:[/] {elapsedMilliseconds}ms
                       [grey]Parsed:[/] {(parsed ? "[green]Yes[/]" : "[red]No[/]")}

                       [grey]Score:[/] {(result.Score > 0 ? $"[green]{result.Score}[/]" : $"[red]{result.Score}[/]")}
                       [grey]Action Correct:[/] {(result.ActionWasCorrect ? "[green]Yes[/]" : "[red]No[/]")}
                       [grey]Parameters Correct:[/] {(result.ParametersWereCorrect ? "[green]Yes[/]" : "[red]No[/]")}

                       [grey]Failures:[/]
                       {failures}
                       """;

        var panel =
                new Panel(new Markup(summary)).Header("[bold white]RESULT SUMMARY[/]")
                                              .Border(BoxBorder.Rounded)
                                              .BorderColor(result.Score > 0
                                                                   ? Color.Green
                                                                   : Color.Red);

        AnsiConsole.Write(panel);

        var rawResponsePanel = new Panel(new Markup(Escape(repairedResponse))).Header("[grey]RAW RESPONSE[/]")
                                                                              .Border(BoxBorder.Rounded)
                                                                              .BorderColor(Color.Grey);

        AnsiConsole.Write(rawResponsePanel);
    }

    private void PrintSummary( List<EvaluationResult> results )
    {
        RenderSectionHeader("FINAL SUMMARY");

        var table = new Table().Border(TableBorder.Rounded)
                               .BorderColor(Color.Grey);

        table.AddColumn("[bold cyan]MODEL[/]");
        table.AddColumn("[bold green]AVG SCORE[/]");
        table.AddColumn("[bold blue]AVG MS[/]");
        table.AddColumn("[bold blue]TOK/S[/]");
        table.AddColumn("[bold yellow]JSON FAIL[/]");
        table.AddColumn("[bold yellow]INTENT FAIL[/]");
        table.AddColumn("[bold yellow]PARAM FAIL[/]");
        table.AddColumn("[bold yellow]FAILTYPE[/]");
        table.AddColumn("[bold red]TIMEOUTS[/]");

        var grouped = results.GroupBy(result => result.ModelName);

        foreach (var modelGroup in grouped)
        {
            var modelResults = modelGroup.ToList();
            var avgScore   = modelResults.Average(result => result.Score);
            var avgLatency = modelResults.Average(result => result.LatencyMs);

            var toksValues = modelResults
                             .Where(r => r.TokensPerSecond.HasValue)
                             .Select(r => r.TokensPerSecond!.Value)
                             .ToList();

            var avgToks = toksValues.Count > 0 ? (double?)toksValues.Average() : null;

            var jsonFailures = modelResults.Count(result => result.FailureCategories
                                                                  .Contains(FailureCategory.JsonParseFailure));

            var intentFailures = modelResults.Count(result => result.FailureCategories
                                                                    .Contains(FailureCategory.WrongIntent));

            var parameterFailures = modelResults.Count(result => result.FailureCategories
                                                                       .Contains(FailureCategory.ParameterMismatch));

            var failureTypeFailures = modelResults.Count(result => result.FailureCategories
                                                                         .Contains(FailureCategory.WrongFailureType));

            var timeouts = modelResults.Count(result => result.FailureCategories
                                                              .Contains(FailureCategory.Timeout));

            table.AddRow(Escape(modelGroup.Key)
                       , avgScore.ToString("F1")
                       , $"{avgLatency:F0} ms"
                       , avgToks.HasValue ? $"{avgToks:F1}" : "—"
                       , jsonFailures.ToString()
                       , intentFailures.ToString()
                       , parameterFailures.ToString()
                       , failureTypeFailures.ToString()
                       , timeouts.ToString());
        }

        AnsiConsole.Write(table);
    }

    private EvaluationResult HandleException( string             model
                                            , EvaluationTestCase testCase
                                            , Exception          exception )
    {
        var exceptionPanel =
                new Panel($"""
                           [red]{Escape(exception.Message)}[/]

                           [grey]Model:[/] {Escape(model)}
                           [grey]Test:[/] {Escape(testCase.Name)}
                           """).Header("[bold red]ERROR[/]")
                               .Border(BoxBorder.Heavy)
                               .BorderColor(Color.Red);

        AnsiConsole.Write(exceptionPanel);

        return new EvaluationResult
               {
                       ModelName              = model
                     , TestName               = testCase.Name
                     , JsonParsedSuccessfully = false
                     , RawResponse            = "[TIMEOUT]"
                     , Score                  = 0
                     , Failures =
                       [
                               "Model may have timed out"
                       ]
                     , FailureCategories =
                       [
                               FailureCategory.Timeout
                       ]
                     , PromptVersion = _promptBuilder.Version
               };
    }

    private async Task<List<EvaluationTestCase>> LoadTestCasesAsync()
    {
        var benchmarkDirectory = Path.Combine("Data"
                                            , "benchmark");

        if (!Directory.Exists(benchmarkDirectory))
        {
            throw new DirectoryNotFoundException($"Benchmark directory not found: {benchmarkDirectory}");
        }

        var files = Directory.GetFiles(benchmarkDirectory
                                     , "*.json"
                                     , SearchOption.AllDirectories);

        var allTestCases = new List<EvaluationTestCase>();

        RenderSectionHeader("LOADING BENCHMARK FILES");

        foreach (var file in files)
        {
            AnsiConsole.MarkupLine($"[grey]-[/] {Escape(file)}");

            var json = await File.ReadAllTextAsync(file);

            var testCases = JsonSerializer.Deserialize<List<EvaluationTestCase>>(json
                                                                               , new JsonSerializerOptions
                                                                                 {
                                                                                         PropertyNameCaseInsensitive = true
                                                                                 });

            if (testCases == null)
            {
                AnsiConsole.MarkupLine($"[red]WARNING:[/] testCases is null in {Escape(file)}");

                continue;
            }

            allTestCases.AddRange(testCases);
        }

        var panelTitle = $"[green]{allTestCases.Count}[/] test cases loaded from [cyan]{files.Length}[/] files";
        var summaryPanel = new Panel(panelTitle).Border(BoxBorder.Rounded)
                                                .BorderColor(Color.Green);

        AnsiConsole.Write(summaryPanel);

        return allTestCases;
    }

    private static void RenderHeader( string title )
    {
        AnsiConsole.Write(new Rule($"[bold cyan]{title}[/]").RuleStyle("grey")
                                                            .Centered());
    }

    private static void RenderSectionHeader( string title )
    {
        AnsiConsole.WriteLine();

        AnsiConsole.Write(new Rule($"[bold yellow]{title}[/]").RuleStyle("grey"));
    }

    private static string Escape( string? text )
    {
        return Markup.Escape(text ?? string.Empty);
    }
}