namespace InterpreterEvaluationRunner.Interpreter.Pipeline.Models;

public enum FailureCategory
{
    None
  , JsonParseFailure
  , WrongIntent
  , WrongParameters
  , HallucinatedAction
  , MissingFailureType
  , Timeout
  , UnsupportedRequestFailure
   , ParameterMismatch
   , WrongFailureType
}