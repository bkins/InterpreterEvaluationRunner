namespace InterpreterEvaluationRunner;

public interface IModelClient
{
    Task<string> GenerateAsync( string            model
                              , string            prompt
                              , CancellationToken cancellationToken = default );
}