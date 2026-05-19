namespace InterpreterEvaluationRunner.Interpreter.Pipeline.Evaluation;

public enum EvaluationFailureType
{
    None
  , JsonParseFailure
  , UnknownAction
  , InvalidFailureType
  , WrongAction
  , MissingRequiredParameter
  , IncorrectParameterValue
  , UnexpectedClarification
  , MissingClarification
  , HallucinatedAction
}