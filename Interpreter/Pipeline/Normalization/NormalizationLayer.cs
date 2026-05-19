using System.Text.Json;
using InterpreterEvaluationRunner.Interpreter.Pipeline.Models;

namespace InterpreterEvaluationRunner.Interpreter.Pipeline.Normalization;

public class NormalizationLayer : INormalizationLayer
{
    public NormalizationResult Normalize(string raw)
    {
        if ( ! TryParse(raw, out var response))
        {
            return new NormalizationResult
                   {
                           Success = false
                         , Error   = "Failed to parse model response"
                   };
        }

        var normalized = new NormalizedActionResponse
                         {
                                 ActionName         = NormalizeActionName(response.ActionName)
                               , Confidence         = Math.Clamp(response.Confidence, 0, 1)
                               , ClarifyingQuestion = response.ClarifyingQuestion ?? ""
                               , FailureType        = NormalizeFailureType(response.FailureType)
                               , MissingParameters  = response.MissingParameters ?? []
                               , CandidateActions   = response.CandidateActions  ?? []
                               , Parameters         = response.Parameters.ToDictionary(kvp => kvp.Key
                                                                                     , kvp => (object)kvp.Value)
                         };

        return new NormalizationResult
               {
                       Success = true
                     , Response = normalized
               };
    }

    private static string NormalizeFailureType( string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return FailureTypes.None;

        return value.Trim() switch
        {
                "none" => FailureTypes.None
              , "None" => FailureTypes.None
              , _ => value.Trim()
        };
    }
    
    private static string NormalizeActionName( string? value)
    {
        return string.IsNullOrWhiteSpace(value)
                       ? "None"
                       : value.Trim();

    }
    
    
    public bool TryParse( string                        rawResponse
                         , out NormalizedActionResponse response)
    {
        response = new NormalizedActionResponse
                   {
                           ActionName = "None"
                         , Parameters = new Dictionary<string, object>()
                   };

        if (string.IsNullOrWhiteSpace(rawResponse)) return false;
        try
        {
            using var document = JsonDocument.Parse(rawResponse);

            var root = document.RootElement;

            response.ActionName = GetString(root, "actionName")
                               ?? GetString(root, "ActionName")
                               ?? "None";

            response.ClarifyingQuestion = GetString(root, "clarifyingQuestion") ?? "";
            response.FailureType        = GetString(root, "failureType")        ?? FailureTypes.None;
            response.Confidence         = GetDouble(root, "confidence");
            response.Parameters         = GetParameters(root);
            response.MissingParameters  = GetStringList(root, "missingParameters");
            response.CandidateActions   = GetCandidateActions(root);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? GetString( JsonElement root
                                    , string      propertyName )
    {
        if (root.TryGetProperty(propertyName, out var prop))
        {
            return prop.ValueKind == JsonValueKind.String
                           ? prop.GetString()
                           : prop.ToString();
        }

        return null;
    }

    private static double GetDouble( JsonElement root
                                   , string      propertyName )
    {
        if ( ! root.TryGetProperty(propertyName, out var prop)) return 0;
        if (prop.TryGetDouble(out var value))                   return Math.Clamp(value, 0, 1);
        
        return 0;
    }

    private static Dictionary<string, object> GetParameters( JsonElement root )
    {
        var result = new Dictionary<string, object>();

        if ( ! root.TryGetProperty("parameters", out var prop) 
            || prop.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var property in prop.EnumerateObject())
        {
            result[property.Name] = property.Value.ToString();
        }

        return result;
    }

    private static List<string> GetStringList( JsonElement root
                                             , string      propertyName )
    {
        var result = new List<string>();

        if ( ! root.TryGetProperty(propertyName, out var prop) 
            || prop.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        result.AddRange(prop.EnumerateArray().Select(item => item.ToString()));

        return result;
    }

    private static List<CandidateAction> GetCandidateActions( JsonElement root)
    {
        var result = new List<CandidateAction>();

        if ( ! root.TryGetProperty("candidateActions", out var prop) 
            || prop.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        result.AddRange(from item in prop.EnumerateArray()
                        let actionName = GetString(item, "actionName") ?? "Unknown"
                        let confidence = GetDouble(item, "confidence")
                        select new CandidateAction
                               {
                                       ActionName = actionName
                                     , Confidence = confidence
                               });

        return result;
    }
}