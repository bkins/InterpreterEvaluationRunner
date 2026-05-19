namespace InterpreterEvaluationRunner.Interpreter.Pipeline.Repair.Rules;

public class ToolCallNoiseRemovalRule : IRepairRule
{
    public string Name => nameof(ToolCallNoiseRemovalRule);

    private static readonly string[] NoiseTokens =
    [
            "[TOOL_CALLS]"
          , "Response:"
          , "Assistant:"
          , "User Input:"
    ];

    public bool CanApply( string input )
    {
        return NoiseTokens.Any(input.Contains);
    }

    public string Apply( string input )
    {
        var cleaned = NoiseTokens.Aggregate(input
                                          , ( current, token ) => current.Replace(token, ""));

        return cleaned.Trim();
    }
}