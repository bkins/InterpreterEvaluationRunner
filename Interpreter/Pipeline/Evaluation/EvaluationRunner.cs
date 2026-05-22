using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using InterpreterEvaluationRunner.Interpreter.Pipeline.Models;
using InterpreterEvaluationRunner.Interpreter.Pipeline.Normalization;
using InterpreterEvaluationRunner.Interpreter.Pipeline.Repair.Engine;
using InterpreterEvaluationRunner.Interpreter.Pipeline.Validation;
using Spectre.Console;

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
        
        Console.WriteLine("------------------------------------------------");
        Console.WriteLine($"Models to be tested: \n - {string.Join("\n - ", models)}");;
        Console.WriteLine("------------------------------------------------");
        
        foreach (var model in models)
        {
            
            Console.WriteLine("");
            Console.WriteLine($"Starting benchmark for model: {model}");
            Console.WriteLine($"");
            
            var consecutiveFailures = 0;
            //var justUseThree = testCases.Take(3);
            //Console.WriteLine($"Running only the first 3 test cases for model {model} to check for basic functionality before proceeding with the full benchmark.");
            
            foreach (var testCase in testCases)
            {
                var result = await RunSingleTestAsync(model, testCase);
                
                allResults.Add(result);
                
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

        PrintSummary(allResults);
        await _resultExporter.ExportAsync(allResults);
        
        var formattedTime = sw.Elapsed.ToString(@"hh\:mm\:ss");
        
        Console.WriteLine($"\nAll tests completed in {formattedTime} seconds");
        
    }

    private void PrintSummary(List<EvaluationResult> results)
    {
        Console.WriteLine();
        Console.WriteLine("================================================");
        Console.WriteLine("FINAL SUMMARY");
        Console.WriteLine("================================================");
        Console.WriteLine();

        Console.WriteLine(
            $"{"MODEL",-15} {"AVG",-8} {"JSON",-8} {"INTENT",-8} {"PARAMS",-8} {"FAILTYPE",-10} {"TIMEOUTS",-10}");

        Console.WriteLine(new string('-', 75));

        var grouped = results.GroupBy(r => r.ModelName);

        foreach (var modelGroup in grouped)
        {
            var modelResults        = modelGroup.ToList();
            var avgScore            = modelResults.Average(result => result.Score);
            var jsonFailures        = modelResults.Count(result => result.FailureCategories.Contains(FailureCategory.JsonParseFailure));
            var intentFailures      = modelResults.Count(result => result.FailureCategories.Contains(FailureCategory.WrongIntent));
            var parameterFailures   = modelResults.Count(result => result.FailureCategories.Contains(FailureCategory.ParameterMismatch));
            var failureTypeFailures = modelResults.Count(result => result.FailureCategories.Contains(FailureCategory.WrongFailureType));
            var timeouts            = modelResults.Count(result => result.FailureCategories.Contains(FailureCategory.Timeout));

            Console.WriteLine($"{modelGroup.Key,-15} "
                            + $"{avgScore,-8:F1} "
                            + $"{jsonFailures,-8} "
                            + $"{intentFailures,-8} "
                            + $"{parameterFailures,-8} "
                            + $"{failureTypeFailures,-10} "
                            + $"{timeouts,-10}");
        }

        Console.WriteLine();
    }
    
    private async Task<EvaluationResult> RunSingleTestAsync( string              model
                                                            , EvaluationTestCase testCase)
    {
        var prompt = _promptBuilder.BuildPrompt(testCase.UserInput);

        var stopwatch = Stopwatch.StartNew();
        
        Console.WriteLine($"""
                           Next Test:
                           -------------------------------------------
                           Model         : {model}
                           Test          : {testCase.Name}
                           Input         : {testCase.UserInput}
                           PROMPT LENGTH : {prompt.Length}
                           """);
        Console.WriteLine($"");
        
        string rawResponse;

        try
        {
            var beforeGenerate = stopwatch.ElapsedMilliseconds / 1000.0;
            
            Console.WriteLine($"Sending prompt to model...");
            rawResponse = await _modelClient.GenerateAsync(model, prompt);
            
            var afterGenerate = stopwatch.ElapsedMilliseconds / 1000.0;
            
            Console.WriteLine($"GENERATION TIME: {afterGenerate - beforeGenerate} seconds");
        }
        catch (TaskCanceledException tce)
        {
            stopwatch.Stop();

            return HandleException(model:     model
                                 , testCase:  testCase
                                 , exception: tce);
        }
        catch (HttpRequestException hre)
        {
            stopwatch.Stop();

            return HandleException(model:     model
                                 , testCase:  testCase
                                 , exception: hre);
        }
        catch (JsonException je)
        {
            stopwatch.Stop();

            return HandleException(model:     model
                                 , testCase:  testCase
                                 , exception: je);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            return new EvaluationResult
                   {
                           ModelName              = model
                         , TestName               = testCase.Name
                         , JsonParsedSuccessfully = false
                         , RawResponse            = $"[ERROR] {ex.Message}"
                         , StackTrace             = ex.StackTrace
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
            Console.WriteLine("");
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

        var result = _resultScorer.Score(model
                                       , testCase
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
                           
                           {testCase.Name} -- DONE
                           
                           """);
        return result;
    }

    private EvaluationResult HandleException( string                model
                                            , EvaluationTestCase    testCase
                                            , Exception             exception )
    {
        var tab = "\t";
        var innerException = exception.InnerException is not null
                                     ? $"\n{tab}Inner Exception: {exception.InnerException.Message}"
                                     : string.Empty;
        Console.WriteLine(
$"""

{tab}================================================
{tab}ERROR: {exception.Message}{innerException}
{tab}MODEL: {model}
{tab}TEST : {testCase.Name}
{tab}================================================

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

        if ( ! Directory.Exists(benchmarkDirectory))
        {
            throw new DirectoryNotFoundException($"Benchmark directory not found: {benchmarkDirectory}");
        }

        var files = Directory.GetFiles(benchmarkDirectory
                                     , "*.json"
                                     , SearchOption.AllDirectories);

        var allTestCases = new List<EvaluationTestCase>();

        Console.WriteLine($"Loading benchmark files:");
        foreach (var file in files)
        {
            Console.WriteLine($" - {file}");

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
        
        Console.WriteLine($"Total test cases loaded: {allTestCases.Count} in {files.Length} files");
        return allTestCases;
    }
}