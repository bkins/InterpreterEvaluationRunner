using InterpreterEvaluationRunner.Interpreter.Pipeline.Evaluation;
using InterpreterEvaluationRunner.Interpreter.Pipeline.Models;
using InterpreterEvaluationRunner.Interpreter.Pipeline.Normalization;

namespace InterpreterEvaluationRunner;

public class ResultScorer
{
    public EvaluationResult Score(string                    modelName
                                , EvaluationTestCase        testCase
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
                           , ModelName              = modelName
                     };

        if ( ! parsedSuccessfully 
             || response == null)
        {
            result.Failures.Add("JSON parsing failed");
            result.FailureCategories.Add(FailureCategory.JsonParseFailure);
            
            return result;
        }

        var score = 0;

        score += 20;

        testCase.ExpectedAction = testCase.ExpectedAction == ""
                                          ? "None"
                                          : testCase.ExpectedAction;
        
        if (response.ActionName == testCase.ExpectedAction)
        {
            score                   += 40;
            result.ActionWasCorrect =  true;
        }
        else
        {
            result.Failures.Add($"Expected action '{testCase.ExpectedAction}' but got '{response.ActionName}'");
            result.FailureCategories.Add(FailureCategory.WrongFailureType);
        }

        if (response.FailureType == testCase.ExpectedFailureType)
        {
            score                        += 20;
            result.FailureTypeWasCorrect =  true;
        }
        else
        {
            result.Failures.Add($"Expected failure type '{testCase.ExpectedFailureType}' but got '{response.FailureType}'");
            result.FailureCategories.Add(FailureCategory.WrongFailureType);
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
            result.FailureCategories.Add(FailureCategory.ParameterMismatch);
        }

        result.Score = score;

        return result;
    }

    private bool CompareParameters( Dictionary<string, object> expected
                                  , Dictionary<string, object> actual )
    {
        if (expected.Count != actual.Count) return false;
        
        foreach (var kvp in expected)
        {
            if ( ! actual.TryGetValue(kvp.Key, out var value)) return false;
            
            if ( ! string.Equals(kvp.Value.ToString()
                               , value.ToString()
                               , StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}