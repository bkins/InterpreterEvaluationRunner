namespace InterpreterEvaluationRunner.Interpreter.Pipeline.Models;

public static class FailureTypes
{
    public const string None               = "None";
    public const string AmbiguousIntent    = "AmbiguousIntent";
    public const string AmbiguousRequest   = "AmbiguousRequest";
    public const string UnsupportedRequest = "UnsupportedRequest";
    public const string MissingParameters  = "MissingParameters";
    public const string ParsingError       = "ParsingError";
}