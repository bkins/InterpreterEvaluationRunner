namespace InterpreterEvaluationRunner.Interpreter.Pipeline.Evaluation;

public class EvaluationTestCase
{
    public string                     Name                       { get; set; } = "";
    public string                     UserInput                  { get; set; } = "";
    public string                     ExpectedAction             { get; set; } = "";
    public Dictionary<string, object> ExpectedParameters         { get; set; } = [];
    public bool                       ShouldRequireClarification { get; set; }
    public bool                       ShouldFail                 { get; set; }
    public bool                       ExpectsFailure             { get; set; }
    public string                     ExpectedFailureType        { get; set; } = "None";
    public string                     Category                   { get; set; } = "";
    public bool                       SkipParameterCheck         { get; set; }
}