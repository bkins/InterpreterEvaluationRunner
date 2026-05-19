
using InterpreterEvaluationRunner.Interpreter.Pipeline.Normalization;
using InterpreterEvaluationRunner.Interpreter.Pipeline.Repair.Models;
using InterpreterEvaluationRunner.Interpreter.Pipeline.Validation;

namespace InterpreterEvaluationRunner.Interpreter.Pipeline;

public class PipelineResult
{
    public RepairResult        RepairResult        { get; init; } = new();
    public NormalizationResult NormalizationResult { get; init; } = new();
    public ValidationResult    ValidationResult    { get; init; } = new();

    public bool Success => NormalizationResult.Success
                        && ValidationResult.IsValid;

    public NormalizedActionResponse? Response => NormalizationResult.Response;
}