namespace InterpreterEvaluationRunner.Interpreter.Pipeline.Validation;

public class ValidationError
{
    public string PropertyName { get; set; } = "";

    public string ErrorMessage { get; set; } = "";

    public object? AttemptedValue { get; set; }
}