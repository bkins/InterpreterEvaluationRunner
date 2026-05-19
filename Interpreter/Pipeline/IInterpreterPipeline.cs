namespace InterpreterEvaluationRunner.Interpreter.Pipeline;

public interface IInterpreterPipeline
{
    Task<PipelineResult> ProcessAsync( string rawResponse );
}