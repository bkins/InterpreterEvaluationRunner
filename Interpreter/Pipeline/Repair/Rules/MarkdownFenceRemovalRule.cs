using System.Text.RegularExpressions;
using CP.Client.Core.Avails;

namespace InterpreterEvaluationRunner.Interpreter.Pipeline.Repair.Rules;

public class MarkdownFenceRemovalRule : IRepairRule
{
    public string Name => nameof(MarkdownFenceRemovalRule);

    public bool CanApply(string input)
    {
        return input.Contains("```");
    }

    public string Apply(string input)
    {
        var cleaned = Regex.Replace(input
                                  , RegexMatchingPatterns.MarkdownCodeBlockFencePattern
                                  , ""
                                  , RegexOptions.IgnoreCase);

        return cleaned.Trim();
    }
}