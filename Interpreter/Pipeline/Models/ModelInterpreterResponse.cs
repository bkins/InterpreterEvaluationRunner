namespace InterpreterEvaluationRunner.Interpreter.Pipeline.Models;

public class ModelInterpreterResponse
{
    public string                     ActionName         { get; set; } = "None";
    public double                     Confidence         { get; set; }
    public Dictionary<string, string> Parameters         { get; set; } = [];
    public List<string>               MissingParameters  { get; set; } = [];
    public string                     ClarifyingQuestion { get; set; } = "";
    public string                     FailureType        { get; set; } = "";
    public List<CandidateAction>      CandidateActions   { get; set; } = [];
}