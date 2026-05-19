
using InterpreterEvaluationRunner.Interpreter.Pipeline.Normalization;

namespace InterpreterEvaluationRunner.Interpreter.Pipeline.Validation;

public interface IContractValidator
{
    Task<ValidationResult> ValidateAsync( NormalizedActionResponse response);
}