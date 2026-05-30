using InterpreterEvaluationRunner.Interpreter.Pipeline;
using InterpreterEvaluationRunner.Interpreter.Pipeline.Evaluation;
using InterpreterEvaluationRunner.Interpreter.Pipeline.Models;
using InterpreterEvaluationRunner.Interpreter.Pipeline.Normalization;

namespace InterpreterEvaluationRunner;

public class ResultScorer
{
    public EvaluationResult Score( string             modelName
                                 , EvaluationTestCase testCase
                                 , PipelineResult     pipelineResult
                                 , long               latencyMs )
    {
        var result = new EvaluationResult
                     {
                             TestName               = testCase.Name
                           , ModelName              = modelName
                           , LatencyMs              = latencyMs
                           , RawResponse            = pipelineResult.RepairResult.RepairedText
                           , JsonParsedSuccessfully = pipelineResult.JsonParsedSuccessfully
                     };

        EvaluateParsing(pipelineResult, result);

        if (pipelineResult.Response == null)
        {
            result.Failures.Add("No usable response could be recovered.");
            result.FailureCategories.Add(FailureCategory.JsonParseFailure);

            return result;
        }
        
        if (pipelineResult.JsonParsedSuccessfully)
        {
            result.Score += FailureScoring.Parsing;
        }
        else
        {
            result.Failures.Add("JSON required repair.");
        }
        
        EvaluateValidation(pipelineResult, result);

        var response = pipelineResult.Response;

        if (response == null)
        {
            result.Failures.Add("Response was null after parsing.");
            return result;
        }

        if (testCase.ExpectsFailure)
        {
            EvaluateFailureHandling(testCase
                                  , response
                                  , result);
        }
        else
        {
            EvaluateIntent(testCase
                         , response
                         , result);
            EvaluateParameters(testCase
                             , response
                             , result);
        }

        ApplyRepairPenalty(pipelineResult
                         , result);

        ComputeFinalScore(result);

        return result;
    }

    private void EvaluateParsing( PipelineResult   pipelineResult
                                , EvaluationResult result )
    {
        if ( ! pipelineResult.JsonParsedSuccessfully)
        {
            result.Failures.Add("JSON parsing failed.");
            result.FailureCategories.Add(FailureCategory.JsonParseFailure);

            return;
        }

        result.Score += FailureScoring.Parsing;
    }

    private void EvaluateValidation( PipelineResult   pipelineResult
                                   , EvaluationResult result )
    {
        if ( ! pipelineResult.ValidationSucceeded)
        {
            result.Failures.Add("Validation failed.");
            result.Score -= FailureScoring.Validation;
            
            return;
        }

        result.Score += FailureScoring.Validation;
    }

    private void EvaluateIntent( EvaluationTestCase       testCase
                               , ModelInterpreterResponse response
                               , EvaluationResult         result )
    {
        var expectedAction = string.IsNullOrWhiteSpace(testCase.ExpectedAction)
                                     ? "None"
                                     : testCase.ExpectedAction;

        if (response.ActionName.Equals(expectedAction
                                     , StringComparison.OrdinalIgnoreCase))
        {
            result.ActionWasCorrect =  true;
            result.Score            += FailureScoring.Intent;

            return;
        }

        result.Failures.Add($"Expected action '{expectedAction}' but got '{response.ActionName}'.");
        result.FailureCategories.Add(FailureCategory.WrongIntent);
    }

    private void EvaluateFailureHandling( EvaluationTestCase       testCase
                                        , ModelInterpreterResponse response
                                        , EvaluationResult         result )
    {
        if (response.FailureType.Equals(testCase.ExpectedFailureType
                                      , StringComparison.OrdinalIgnoreCase))
        {
            result.FailureTypeWasCorrect =  true;
            result.Score                 += FailureScoring.FailureType;

            return;
        }

        result.Failures.Add($"Expected failure type '{testCase.ExpectedFailureType}' but got '{response.FailureType}'.");
        result.FailureCategories.Add(FailureCategory.WrongFailureType);
    }

    private void EvaluateParameters( EvaluationTestCase       testCase
                                   , ModelInterpreterResponse response
                                   , EvaluationResult         result )
    {
        var parametersCorrect = CompareParameters(testCase.ExpectedParameters
                                                , response.Parameters);

        if (parametersCorrect)
        {
            result.ParametersWereCorrect =  true;
            result.Score                 += FailureScoring.Parameters;

            return;
        }

        result.Failures.Add("Parameter mismatch.");
        result.FailureCategories.Add(FailureCategory.ParameterMismatch);
    }

    private void ApplyRepairPenalty( PipelineResult   pipelineResult
                                   , EvaluationResult result )
    {
        if (pipelineResult.RepairResult.WasModified)
        {
            result.Failures.Add("Response required repair.");
        }
        else
        {
            result.Score += FailureScoring.NoRepairNeeded;
        }
    }

    private void ComputeFinalScore( EvaluationResult result )
    {
        result.Score = Math.Max(0, Math.Min(100, result.Score));
    }

    private bool CompareParameters( Dictionary<string, object> expected
                                  , Dictionary<string, object> actual )
    {
        if (expected.Count != actual.Count) return false;

        foreach (var kvp in expected)
        {
            if ( ! actual.TryGetValue(kvp.Key, out var actualValue))
            {
                return false;
            }

            var expectedValue = NormalizeValue(kvp.Value.ToString()?.Trim());
            var actualString  = NormalizeValue(actualValue?.ToString()?.Trim());

            if ( ! string.Equals(expectedValue
                               , actualString
                               , StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
    
    private static string NormalizeValue(object? value)
    {
        return value switch
        {
                null     => "",
                bool b   => b.ToString().ToLowerInvariant(),
                double d => d.ToString("G"),
                float f  => f.ToString("G"),
                _        => value.ToString()?.Trim().ToLowerInvariant() ?? ""
        };
    }
}

internal static class FailureScoring
{
    public const  int Parsing          = 10;
    public const  int Validation       = 10;
    public const  int Intent           = 50;
    public const  int FailureType      = 60;
    public const  int NoActionBehavior = 10;
    public const  int NoRepairNeeded   = 10;
    public const  int Parameters       = 20;
}

// public class ResultScorer
// {
//     public EvaluationResult Score( string                    modelName
//                                  , EvaluationTestCase        testCase
//                                  , PipelineResult            pipelineResult
//                                  , long                      latencyMs )
//     {
//         var parsedSuccessfully = pipelineResult.JsonParsedSuccessfully;
//         var rawResponse        = pipelineResult.RepairResult.RepairedText;
//         var response           = pipelineResult.Response;
//         
//         var result = new EvaluationResult
//                      {
//                              TestName               = testCase.Name
//                            , JsonParsedSuccessfully = pipelineResult.JsonParsedSuccessfully
//                            , RawResponse            = rawResponse
//                            , LatencyMs              = latencyMs
//                            , ModelName              = modelName
//                      };
//
//         if ( ! parsedSuccessfully 
//              || response == null)
//         {
//             result.Failures.Add("JSON parsing failed");
//             result.FailureCategories.Add(FailureCategory.JsonParseFailure);
//             
//             return result;
//         }
//
//         var score = 0;
//
//         score += 20;
//
//         testCase.ExpectedAction = testCase.ExpectedAction == ""
//                                           ? "None"
//                                           : testCase.ExpectedAction;
//         
//         if (response.ActionName == testCase.ExpectedAction)
//         {
//             score                   += 40;
//             result.ActionWasCorrect =  true;
//         }
//         else
//         {
//             result.Failures.Add($"Expected action '{testCase.ExpectedAction}' but got '{response.ActionName}'");
//             result.FailureCategories.Add(FailureCategory.WrongFailureType);
//         }
//
//         if (response.FailureType == testCase.ExpectedFailureType)
//         {
//             score                        += 20;
//             result.FailureTypeWasCorrect =  true;
//         }
//         else
//         {
//             result.Failures.Add($"Expected failure type '{testCase.ExpectedFailureType}' but got '{response.FailureType}'");
//             result.FailureCategories.Add(FailureCategory.WrongFailureType);
//         }
//
//         var parametersCorrect = CompareParameters(testCase.ExpectedParameters
//                                                 , response.Parameters);
//
//         if (parametersCorrect)
//         {
//             score                        += 20;
//             result.ParametersWereCorrect =  true;
//         }
//         else
//         {
//             result.Failures.Add("Parameter mismatch");
//             result.FailureCategories.Add(FailureCategory.ParameterMismatch);
//         }
//
//         result.Score = score;
//
//         return result;
//     }
//
//     private bool CompareParameters( Dictionary<string, object> expected
//                                   , Dictionary<string, object> actual )
//     {
//         if (expected.Count != actual.Count) return false;
//         
//         foreach (var kvp in expected)
//         {
//             if ( ! actual.TryGetValue(kvp.Key, out var value)) return false;
//             
//             if ( ! string.Equals(kvp.Value.ToString()
//                                , value.ToString()
//                                , StringComparison.OrdinalIgnoreCase))
//             {
//                 return false;
//             }
//         }
//
//         return true;
//     }
// }