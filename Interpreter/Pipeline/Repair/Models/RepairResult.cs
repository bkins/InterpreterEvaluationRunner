namespace InterpreterEvaluationRunner.Interpreter.Pipeline.Repair.Models;

public class RepairResult
{
    public string       OriginalText   { get; init; } = "";
    public string       RepairedText   { get; init; } = "";
    public bool         WasModified    { get; init; }
    public List<string> AppliedRepairs { get; init; } = [];
}