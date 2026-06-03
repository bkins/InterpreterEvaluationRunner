using InterpreterEvaluationRunner.Interpreter.Pipeline.Models;

namespace InterpreterEvaluationRunner.Interpreter.Pipeline.Evaluation;

public class EvaluationResult
{
    public string       TestName                { get; set; } = "";
    public string       ModelName               { get; set; } = "";
    public bool         JsonParsedSuccessfully  { get; set; }
    public bool         ValidationSucceeded     { get; set; }
    public long         LatencyMs               { get; set; }
    public string       RawResponse             { get; set; } = "";
    public string       ErrorMessage            { get; set; } = "";
    public int          Score                   { get; set; }
    public List<string> Failures                { get; set; } = [];
    public bool         ActionWasCorrect        { get; set; }
    public bool         ParametersWereCorrect   { get; set; }
    public bool         FailureTypeWasCorrect   { get; set; }
    public bool         RepairWasRequired       { get; set; }
    public bool         ClarificationWasCorrect { get; set; }
    public bool         UsedFallbackReasoning   { get; set; }
    public string                PromptVersion             { get; set; } = "";
    public bool                  JsonExtractionWasRequired { get; set; }
    public string?               StackTrace                { get; set; }
    public List<FailureCategory> FailureCategories         { get; set; } = [];
    public double?               TokensPerSecond           { get; set; }
}