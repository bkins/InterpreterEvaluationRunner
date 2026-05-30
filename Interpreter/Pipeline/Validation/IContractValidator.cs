
using InterpreterEvaluationRunner.Interpreter.Pipeline.Models;
using InterpreterEvaluationRunner.Interpreter.Pipeline.Normalization;

namespace InterpreterEvaluationRunner.Interpreter.Pipeline.Validation;

public interface IContractValidator
{
    Task<ValidationResult> ValidateAsync( ModelInterpreterResponse response);
}