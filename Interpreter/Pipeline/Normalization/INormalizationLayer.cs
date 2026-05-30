using InterpreterEvaluationRunner.Interpreter.Pipeline.Models;

namespace InterpreterEvaluationRunner.Interpreter.Pipeline.Normalization;

public interface INormalizationLayer
{
    ModelInterpreterResponse? Normalize(string repairedJson);
}