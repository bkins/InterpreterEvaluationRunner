using System.Diagnostics;
using System.Text.Json;
using InterpreterEvaluationRunner.Interpreter.Pipeline.Models;
using InterpreterEvaluationRunner.Interpreter.Pipeline.Normalization;
using InterpreterEvaluationRunner.Interpreter.Pipeline.Repair.Engine;
using InterpreterEvaluationRunner.Interpreter.Pipeline.Validation;

namespace InterpreterEvaluationRunner.Interpreter.Pipeline.Evaluation;

public class EvaluationRunner : IEvaluationRunner
{
    private readonly IModelClient          _modelClient;
    private readonly PromptBuilder         _promptBuilder;
    private readonly ResultScorer          _resultScorer;
    private readonly IContractValidator    _validator;
    private readonly ResultExporter        _resultExporter;
    private readonly NormalizationLayer    _normalizationLayer;
    private readonly IResponseRepairEngine _responseRepairEngine;
    private readonly IInterpreterPipeline  _pipeline;

    public EvaluationRunner( IModelClient          modelClient
                           , PromptBuilder         promptBuilder
                           , NormalizationLayer    normalizationLayer
                           , ResultScorer          resultScorer
                           , IContractValidator    validator
                           , ResultExporter        resultExporter
                           , IResponseRepairEngine responseRepairEngine
                           , IInterpreterPipeline  pipeline )
    {
        _modelClient          = modelClient;
        _promptBuilder        = promptBuilder;
        _normalizationLayer   = normalizationLayer;
        _resultScorer         = resultScorer;
        _validator            = validator;
        _resultExporter       = resultExporter;
        _responseRepairEngine = responseRepairEngine;
        _pipeline             = pipeline;
    }

    public async Task RunAsync()
    {
        var sw        = new Stopwatch();
        sw.Start();
        
        var testCases = await LoadTestCasesAsync();
        // var models = new[]
        //              {
        //                      "phi3:mini"
        //              };
        var models = new[]
                     {
                           //   "phi3:mini"
                           // , 
                             "qwen2.5:7b"
                           , "mistral"
                             //, "deepseek-r1:8b"
                           , "llama3.1:8b"
                     };

        var allResults = new List<EvaluationResult>();

        foreach (var model in models)
        {
            Console.WriteLine($"Starting benchmark for model: {model}");

            var consecutiveFailures = 0;
            //var justUseThree = testCases.Take(3);
            //Console.WriteLine($"Running only the first 3 test cases for model {model} to check for basic functionality before proceeding with the full benchmark.");
            
            foreach (var testCase in testCases)
            {
                var result = await RunSingleTestAsync(model, testCase);

                if (result.Score == 0)
                {
                    consecutiveFailures++;
                }
                else
                {
                    consecutiveFailures = 0;
                }

                if (consecutiveFailures >= 5)
                {
                    Console.WriteLine($"Skipping model {model} due to repeated failures.");
                    break;
                }
            }
        }

        await _resultExporter.ExportAsync(allResults);
        
        Console.WriteLine($"\nAll tests completed in {sw.Elapsed.TotalSeconds:F2} seconds");
    }

    private async Task<EvaluationResult> RunSingleTestAsync( string              model
                                                            , EvaluationTestCase testCase)
    {
        var prompt = _promptBuilder.BuildPrompt(testCase.UserInput);

        var stopwatch = Stopwatch.StartNew();
        
        Console.WriteLine($"""
                           RUNNING TEST
                           Model: {model}
                           Test : {testCase.Name}
                           Input: {testCase.UserInput}
                           """);
        Console.WriteLine($"PROMPT LENGTH: {prompt.Length}");
        
        string rawResponse;

        try
        {
            var beforeGenerate = stopwatch.ElapsedMilliseconds;
            
            rawResponse = await _modelClient.GenerateAsync(model, prompt);
            
            var afterGenerate = stopwatch.ElapsedMilliseconds;
            
            Console.WriteLine($"GENERATION TIME: {afterGenerate - beforeGenerate}ms");
        }
        catch (TaskCanceledException)
        {
            stopwatch.Stop();

            return HandleException(model: model
                                 , testCase: testCase);
        }
        catch (HttpRequestException)
        {
            stopwatch.Stop();

            return HandleException(model: model
                                 , testCase: testCase);
        }
        catch (JsonException)
        {
            stopwatch.Stop();

            return HandleException(model: model
                                 , testCase: testCase);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            Console.WriteLine($"""

                               ================================================
                               ERROR
                               MODEL: {model}
                               TEST : {testCase.Name}
                               ERROR: {ex.Message}
                               ================================================

                               """);

            return new EvaluationResult
                   {
                           ModelName              = model
                         , TestName               = testCase.Name
                         , JsonParsedSuccessfully = false
                         , RawResponse            = $"[ERROR] {ex.Message}"
                         , Score                  = 0
                         , Failures =
                           [
                                   $"Exception: {ex.GetType().Name}"
                           ]
                         , PromptVersion = _promptBuilder.Version
                   };
        }
        var pipelineResult = await _pipeline.ProcessAsync(rawResponse);
        var response       = pipelineResult.Response;
        var parsed         = pipelineResult.Success;
        
        if (response != null)
        {
            Console.WriteLine("DESERIALIZED RESPONSE:");
            Console.WriteLine(JsonSerializer.Serialize(response
                                                     , new JsonSerializerOptions
                                                       {
                                                               WriteIndented = true
                                                       }));
        }

        if (response != null)
        {
            var validationResult = pipelineResult.ValidationResult;
            
            if ( ! validationResult.IsValid)
            {
                foreach (var error in validationResult.Errors)
                {
                    Console.WriteLine($"""
                                       VALIDATION ERROR
                                       Property: {error.PropertyName}
                                       Message : {error.ErrorMessage}
                                       Attempted Value: {error.AttemptedValue}
                                       """);
                }
            }
        }

        var result = _resultScorer.Score(testCase
                                       , response
                                       , parsed
                                       , pipelineResult.RepairResult.RepairedText
                                       , stopwatch.ElapsedMilliseconds);
        
        result.PromptVersion             = _promptBuilder.Version;
        //Moved to Pipeline
        // result.JsonExtractionWasRequired = extractionResult.WasModified;
        
        stopwatch.Stop();

        Console.WriteLine($"""
                           ================================================
                           MODEL: {model}
                           TEST: {testCase.Name}
                           LATENCY: {stopwatch.ElapsedMilliseconds}ms
                           PARSED: {parsed}
                           RAW RESPONSE:
                           {pipelineResult.RepairResult.RepairedText}
                           ================================================
                           """);
        
        var formatedFailures = result.Failures.Count != 0
                              ? string.Join(Environment.NewLine
                                          , result.Failures.Select(failures => $"- {failures}"))
                              : "None";
        
        Console.WriteLine($"""
                           SCORE: {result.Score}
                           ACTION CORRECT: {result.ActionWasCorrect}
                           PARAMETERS CORRECT: {result.ParametersWereCorrect}
                           FAILURES:
                           {formatedFailures}
                           
                           """);
        return result;
    }

    private EvaluationResult HandleException( string             model
                                            , EvaluationTestCase testCase )
    {

        Console.WriteLine($"""

                           ================================================
                           TIMEOUT
                           MODEL: {model}
                           TEST : {testCase.Name}
                           ================================================

                           """);

        return new EvaluationResult
               {
                       ModelName              = model
                     , TestName               = testCase.Name
                     , JsonParsedSuccessfully = false
                     , RawResponse            = "[TIMEOUT]"
                     , Score                  = 0
                     , Failures =
                       [
                               "Model timeout"
                       ]
                     , PromptVersion = _promptBuilder.Version
               };
    }

    private async Task<List<EvaluationTestCase>> LoadTestCasesAsync()
    {
        var benchmarkDirectory = Path.Combine("Data"
                                            , "benchmark");

        if ( ! Directory.Exists(benchmarkDirectory))
        {
            throw new DirectoryNotFoundException($"Benchmark directory not found: {benchmarkDirectory}");
        }

        var files = Directory.GetFiles(benchmarkDirectory
                                     , "*.json"
                                     , SearchOption.AllDirectories);

        var allTestCases = new List<EvaluationTestCase>();

        foreach (var file in files)
        {
            Console.WriteLine($"Loading benchmark file: {file}");

            var json = await File.ReadAllTextAsync(file);

            //var testCases = JsonSerializer.Deserialize<List<EvaluationTestCase>>(json);
            var testCases = JsonSerializer.Deserialize<List<EvaluationTestCase>>(json
                                                                               , new JsonSerializerOptions
                                                                                 {
                                                                                         PropertyNameCaseInsensitive = true
                                                                                 });
            if (testCases == null)
            {
                Console.WriteLine($"WARNING: `testCases` is null in file {file}");
                continue;
            }
            
            foreach (var testCase in testCases)
            {
                if (string.IsNullOrWhiteSpace(testCase.Name))
                {
                    Console.WriteLine($"WARNING: Missing test name in file {file}");
                }

                if (string.IsNullOrWhiteSpace(testCase.UserInput))
                {
                    Console.WriteLine($"WARNING: Missing user input in file {file}");
                }
            }

            allTestCases.AddRange(testCases);

        }

        return allTestCases;
    }
}