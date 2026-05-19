namespace InterpreterEvaluationRunner.Interpreter.Pipeline.Repair.Rules;

public class ExtractFirstJsonObjectRule : IRepairRule
{
    public string Name => nameof(ExtractFirstJsonObjectRule);

    public bool CanApply(string input)
    {
        return input.Contains('{');
    }

    public string Apply(string input)
    {
        var start = input.IndexOf('{');

        if (start < 0)
            return input;

        var depth      = 0;
        var inString   = false;
        var escapeNext = false;

        for (int i = start; i < input.Length; i++)
        {
            var c = input[i];

            if (escapeNext)
            {
                escapeNext = false;
                continue;
            }

            if (c == '\\')
            {
                escapeNext = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
                continue;

            if (c == '{')
                depth++;

            if (c == '}')
                depth--;

            if (depth == 0)
            {
                return input[start..(i + 1)];
            }
        }

        return input[start..];
    }
}