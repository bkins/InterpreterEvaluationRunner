using InterpreterEvaluationRunner.Interpreter.Pipeline.Models;

namespace InterpreterEvaluationRunner.Interpreter.Pipeline.Normalization;

public class NormalizationResult
{
    public bool Success { get; init; }

    public ModelInterpreterResponse? Response { get; init; }

    public List<string> Warnings { get; init; } = [];

    public string? Error { get; init; }
}