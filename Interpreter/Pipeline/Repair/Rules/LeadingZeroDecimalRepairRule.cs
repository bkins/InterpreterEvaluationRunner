using System.Text.RegularExpressions;

namespace InterpreterEvaluationRunner.Interpreter.Pipeline.Repair.Rules;

// phi3:mini emits e.g. 00.95 or 00.99 as confidence values — invalid JSON number literals.
// Replace any run of 2+ leading zeros before a decimal point with a single zero.
public class LeadingZeroDecimalRepairRule : IRepairRule
{
    private static readonly Regex Pattern = new(@"(?<!\d)0{2,}\.", RegexOptions.Compiled);

    public string Name => nameof(LeadingZeroDecimalRepairRule);

    public bool CanApply(string input) => Pattern.IsMatch(input);

    public string Apply(string input) => Pattern.Replace(input, "0.");
}
