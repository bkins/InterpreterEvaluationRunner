
using InterpreterEvaluationRunner.Interpreter.Pipeline.Models;
using InterpreterEvaluationRunner.Interpreter.Pipeline.Normalization;
using InterpreterEvaluationRunner.Interpreter.Pipeline.Repair.Models;
using InterpreterEvaluationRunner.Interpreter.Pipeline.Validation;

namespace InterpreterEvaluationRunner.Interpreter.Pipeline;

public class PipelineResult
{
    public RepairResult RepairResult { get; init; } = new();

    public ValidationResult ValidationResult { get; init; } = new();

    public bool JsonParsedSuccessfully { get; init; }

    public ModelInterpreterResponse? Response => JsonParsedSuccessfully
                                                         ? ModelInterpreterResponse
                                                         : null;

    public ModelInterpreterResponse ModelInterpreterResponse { get; init; } = new();

    public bool ValidationSucceeded => ValidationResult.IsValid;
}

// public class PipelineResult
// {
//     public RepairResult             RepairResult        { get; init; } = new();
//     public ModelInterpreterResponse ModelInterpreterResponse { get; init; } = new();
//     public ValidationResult         ValidationResult    { get; init; } = new();
//
//     public bool Success => ModelInterpreterResponse.FailureType == FailureTypes.None
//                         && ValidationResult.IsValid;
//
//     public ModelInterpreterResponse? Response => ModelInterpreterResponse;
// }