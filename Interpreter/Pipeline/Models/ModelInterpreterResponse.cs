namespace InterpreterEvaluationRunner.Interpreter.Pipeline.Models;

public class ModelInterpreterResponse
{
    public string                     ActionName          { get; set; } = "None";
    public double                     Confidence          { get; set; }
    public Dictionary<string, object> Parameters          { get; set; } = [];
    public List<string>               MissingParameters   { get; set; } = [];
    public string                     ClarifyingQuestion  { get; set; } = "";
    public List<CandidateAction>      CandidateActions    { get; set; } = [];
    public string                     FailureType         { get; set; } = "";
    public string                     FailureDescription  { get; set; } = "";
    public string?                    ExpectedFailureType { get; set; }
    public bool                       ExpectsFailure      { get; set; }
}