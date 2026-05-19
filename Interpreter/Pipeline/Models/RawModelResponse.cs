using System.Text.Json;

namespace InterpreterEvaluationRunner.Interpreter.Pipeline.Models;

public class RawModelResponse
{
    public string        RawText     { get; set; }
    public bool          IsValidJson { get; set; }
    public JsonDocument? ParsedJson  { get; set; }
    public string?       ParseError  { get; set; }
}