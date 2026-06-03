namespace InterpreterEvaluationRunner;

public interface IModelClient
{
    Task<GenerationResult> GenerateAsync( string            model
                                        , string            prompt
                                        , CancellationToken cancellationToken = default );
}