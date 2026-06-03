using System.Net.Http.Json;
using System.Text.Json;

namespace InterpreterEvaluationRunner;

public class OllamaClient : IModelClient
{
    private readonly HttpClient _httpClient;

    public OllamaClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<GenerationResult> GenerateAsync( string             model
                                                      , string            prompt
                                                      , CancellationToken cancellationToken = default)
    {
        var request = new
                      {
                              model
                            , prompt
                            , stream     = false
                            , keep_alive = "0m"
                            , options = new
                                        {
                                                num_ctx        = 2048 // expand to: 4096 -> 8192 -> 16384
                                              , temperature    = 0
                                              , num_predict    = 512
                                              , top_p          = 1
                                              , repeat_penalty = 1
                                        }
                      };

        using var response = await _httpClient.PostAsJsonAsync("/api/generate"
                                                             , request
                                                             , cancellationToken);

        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Ollama error ({(int)response.StatusCode}): {json}");
        }

        using var document = JsonDocument.Parse(json);

        var root = document.RootElement;

        var text = root.GetProperty("response").GetString() ?? "";

        double? tokensPerSecond = null;

        if (root.TryGetProperty("eval_count",    out var evalCount) &&
            root.TryGetProperty("eval_duration", out var evalDuration))
        {
            var tokens   = evalCount.GetInt64();
            var durationNs = evalDuration.GetInt64();

            if (durationNs > 0)
            {
                tokensPerSecond = tokens / (durationNs / 1_000_000_000.0);
            }
        }

        return new GenerationResult(text, tokensPerSecond);
    }
}