using InterpreterEvaluationRunner.Interpreter.Pipeline.Evaluation;
using InterpreterEvaluationRunner.Interpreter.Pipeline.Models;
using InterpreterEvaluationRunner.Interpreter.Pipeline.Normalization;

namespace InterpreterEvaluationRunner;

public class ResultScorer
{
    public EvaluationResult Score( EvaluationTestCase        testCase
                                 , NormalizedActionResponse? response
                                 , bool                      parsedSuccessfully
                                 , string                    rawResponse
                                 , long                      latencyMs )
    {
        var result = new EvaluationResult
                     {
                             TestName               = testCase.Name
                           , JsonParsedSuccessfully = parsedSuccessfully
                           , RawResponse            = rawResponse
                           , LatencyMs              = latencyMs
                     };

        if ( ! parsedSuccessfully 
             || response == null)
        {
            result.Failures.Add("JSON parsing failed");
            
            return result;
        }

        var score = 0;

        score += 20;

        if (response.ActionName == testCase.ExpectedAction)
        {
            score                   += 40;
            result.ActionWasCorrect =  true;
        }
        else
        {
            result.Failures.Add($"Expected action '{testCase.ExpectedAction}' but got '{response.ActionName}'");
        }

        if (response.FailureType == testCase.ExpectedFailureType)
        {
            score                        += 20;
            result.FailureTypeWasCorrect =  true;
        }
        else
        {
            result.Failures.Add($"Expected failure type '{testCase.ExpectedFailureType}' but got '{response.FailureType}'");
        }

        var parametersCorrect = CompareParameters(testCase.ExpectedParameters
                                                , response.Parameters);

        if (parametersCorrect)
        {
            score                        += 20;
            result.ParametersWereCorrect =  true;
        }
        else
        {
            result.Failures.Add("Parameter mismatch");
        }

        result.Score = score;

        return result;
    }

    private bool CompareParameters( Dictionary<string, object> expected
                                  , Dictionary<string, object> actual )
    {
        foreach (var kvp in expected)
        {
            if (!actual.TryGetValue(kvp.Key
                                  , out var value))
            {
                return false;
            }

            if ( ! string.Equals(kvp.Value.ToString()
                               , value.ToString()
                               , StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            
            if (expected.Count != actual.Count)
            {
                return false;
            }
        }

        return true;
    }
}