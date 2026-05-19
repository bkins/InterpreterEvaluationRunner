using InterpreterEvaluationRunner.Interpreter.Pipeline.Repair.Models;
using InterpreterEvaluationRunner.Interpreter.Pipeline.Repair.Rules;

namespace InterpreterEvaluationRunner.Interpreter.Pipeline.Repair.Engine;

public class ResponseRepairEngine : IResponseRepairEngine
{
    private readonly IEnumerable<IRepairRule> _rules;

    public ResponseRepairEngine(IEnumerable<IRepairRule> rules)
    {
        _rules = rules;
    }

    public RepairResult Repair(string rawResponse)
    {
        var current = rawResponse;
        var applied = new List<string>();

        foreach (var rule in _rules)
        {
            if (!rule.CanApply(current))
                continue;

            var updated = rule.Apply(current);

            if (updated != current)
            {
                applied.Add(rule.Name);
                current = updated;
            }
        }

        return new RepairResult
               {
                       OriginalText   = rawResponse,
                       RepairedText   = current,
                       WasModified    = applied.Count > 0,
                       AppliedRepairs = applied
               };
    }
    
    public RepairResult Extract( string rawResponse )
    {
        var resultNotRequired = new RepairResult
                                {
                                        OriginalText   = rawResponse
                                      , RepairedText   = rawResponse
                                      , WasModified    = false
                                      , AppliedRepairs = new List<string>()
                                };
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return resultNotRequired;
        }

        var start = rawResponse.IndexOf('{');
        var end   = rawResponse.LastIndexOf('}');

        if (start < 0 
         || end   < start)
        {
            return resultNotRequired;
        }

        var extracted = rawResponse[start..(end + 1)];

        var extractionWasRequired = start != 0 
                                 || end   != rawResponse.Length - 1;

        var result = new RepairResult
                     {
                             OriginalText   = rawResponse
                           , RepairedText   = extracted
                           , WasModified    = extractionWasRequired
                           , AppliedRepairs = new List<string>()
                     };
        result.AppliedRepairs.Add(extractionWasRequired
                                          ? "Extracted JSON from surrounding text"
                                          : "No extraction needed");
        return result;
    }
}