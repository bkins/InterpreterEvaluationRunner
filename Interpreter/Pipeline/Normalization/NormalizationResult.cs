namespace InterpreterEvaluationRunner.Interpreter.Pipeline.Normalization;

public class NormalizationResult
{
    public bool Success { get; init; }

    public NormalizedActionResponse? Response { get; init; }

    public List<string> Warnings { get; init; } = [];

    public string? Error { get; init; }
}