namespace InterpreterEvaluationRunner.Interpreter.Pipeline.Repair.Rules;

public interface IRepairRule
{
    bool CanApply(string input);

    string Apply(string input);

    string Name { get; }
}