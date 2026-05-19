
using InterpreterEvaluationRunner.Interpreter.Pipeline.Normalization;
using InterpreterEvaluationRunner.Interpreter.Pipeline.Repair.Engine;
using InterpreterEvaluationRunner.Interpreter.Pipeline.Validation;

namespace InterpreterEvaluationRunner.Interpreter.Pipeline;

public class InterpreterPipeline : IInterpreterPipeline
{
    private readonly IResponseRepairEngine _repairEngine;
    private readonly INormalizationLayer   _normalizationLayer;
    private readonly IContractValidator    _validator;

    public InterpreterPipeline( IResponseRepairEngine repairEngine
                              , INormalizationLayer   normalizationLayer
                              , IContractValidator    validator )
    {
        _repairEngine       = repairEngine;
        _normalizationLayer = normalizationLayer;
        _validator          = validator;
    }

    public async Task<PipelineResult> ProcessAsync( string rawResponse )
    {
        var repairResult        = _repairEngine.Repair(rawResponse);
        var normalizationResult = _normalizationLayer.Normalize(repairResult.RepairedText);

        ValidationResult validationResult;

        if (normalizationResult.Response != null)
        {
            validationResult = await _validator.ValidateAsync(normalizationResult.Response);
        }
        else
        {
            validationResult = new ValidationResult();

            validationResult.Errors.Add(new ValidationError
                                        {
                                                PropertyName = "Response"
                                              , ErrorMessage = "Normalization failed"
                                        });
        }

        return new PipelineResult
               {
                       RepairResult        = repairResult
                     , NormalizationResult = normalizationResult
                     , ValidationResult    = validationResult
               };
    }
}