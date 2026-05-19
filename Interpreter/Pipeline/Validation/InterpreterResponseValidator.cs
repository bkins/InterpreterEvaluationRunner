
using InterpreterEvaluationRunner.Interpreter.Pipeline.Models;
using InterpreterEvaluationRunner.Interpreter.Pipeline.Normalization;

namespace InterpreterEvaluationRunner.Interpreter.Pipeline.Validation;

public class InterpreterResponseValidator : IContractValidator
{
    public Task<ValidationResult> ValidateAsync( NormalizedActionResponse response )
    {
        var result = new ValidationResult();

        ValidateActionName(response, result);
        ValidateConfidence(response, result);
        ValidateFailureType(response, result);
        ValidateCandidateActions(response, result);

        return Task.FromResult(result);
    }

    private static void ValidateActionName( NormalizedActionResponse response
                                          , ValidationResult         result )
    {
        if (string.IsNullOrWhiteSpace(response.ActionName))
        {
            result.Errors.Add(new ValidationError
                              {
                                      PropertyName   = nameof(response.ActionName)
                                    , ErrorMessage   = "ActionName cannot be empty"
                                    , AttemptedValue = response.ActionName
                              });
        }
    }

    private static void ValidateConfidence( NormalizedActionResponse response
                                          , ValidationResult         result )
    {
        if (response.Confidence is < 0 or > 1)
        {
            result.Errors.Add(new ValidationError
                              {
                                      PropertyName   = nameof(response.Confidence)
                                    , ErrorMessage = "Confidence must be between 0 and 1"
                                    , AttemptedValue = response.Confidence
                              });
        }
    }

    private static void ValidateFailureType( NormalizedActionResponse response
                                           , ValidationResult         result )
    {
        var validFailureTypes = new[]
                                {
                                        FailureTypes.None
                                      , FailureTypes.AmbiguousIntent
                                      , FailureTypes.AmbiguousRequest
                                      , FailureTypes.UnsupportedRequest
                                      , FailureTypes.MissingParameters
                                };

        if (!validFailureTypes.Contains(response.FailureType))
        {
            result.Errors.Add(new ValidationError
                              {
                                      PropertyName = nameof(response.FailureType)
                                    , ErrorMessage = "Unknown failure type"
                                    , AttemptedValue = response.FailureType
                              });
        }
    }

    private static void ValidateCandidateActions( NormalizedActionResponse response
                                                , ValidationResult         result )
    {
        foreach (var candidate in response.CandidateActions)
        {
            if (string.IsNullOrWhiteSpace(candidate.ActionName))
            {
                result.Errors.Add(new ValidationError
                                  {
                                          PropertyName = "CandidateAction.ActionName", ErrorMessage = "Candidate action name cannot be empty"
                                  });
            }

            if (candidate.Confidence < 0 || candidate.Confidence > 1)
            {
                result.Errors.Add(new ValidationError
                                  {
                                          PropertyName   = "CandidateAction.Confidence", ErrorMessage = "Candidate confidence must be between 0 and 1"
                                        , AttemptedValue = candidate.Confidence
                                  });
            }
        }
    }
}