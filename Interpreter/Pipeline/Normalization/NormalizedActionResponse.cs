using InterpreterEvaluationRunner.Interpreter.Pipeline.Models;

namespace InterpreterEvaluationRunner.Interpreter.Pipeline.Normalization;

public class NormalizedActionResponse
{
    public string                     ActionName         { get; set; }
    public double                     Confidence         { get; set; }
    public Dictionary<string, object> Parameters         { get; set; }
    public List<string>               MissingParameters  { get; set; }
    public string                     FailureType        { get; set; }
    public string                     ClarifyingQuestion { get; set; }
    public List<CandidateAction>      CandidateActions   { get; set; } = new();
}