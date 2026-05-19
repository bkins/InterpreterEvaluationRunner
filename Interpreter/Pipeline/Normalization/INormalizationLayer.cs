namespace InterpreterEvaluationRunner.Interpreter.Pipeline.Normalization;

public interface INormalizationLayer
{
    NormalizationResult Normalize(string repairedJson);
}