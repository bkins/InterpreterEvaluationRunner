namespace InterpreterEvaluationRunner.Interpreter.Pipeline.Evaluation;

public interface IEvaluationRunner
{
    Task RunAsync(int maxTestCase = -1);
}