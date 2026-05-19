using InterpreterEvaluationRunner.Interpreter.Pipeline.Repair.Models;

namespace InterpreterEvaluationRunner.Interpreter.Pipeline.Repair.Engine;

public interface IResponseRepairEngine
{
    RepairResult Repair(string   rawResponse);
    RepairResult Extract( string rawResponse );
}