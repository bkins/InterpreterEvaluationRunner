namespace InterpreterEvaluationRunner.Interpreter.Pipeline.Validation;

public class ValidationResult
{
    public bool IsValid => Errors.Count == 0;

    public List<ValidationError> Errors { get; set; } = [];
}