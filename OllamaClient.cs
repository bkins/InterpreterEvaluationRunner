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

    public async Task<string> GenerateAsync( string             model
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
                                              , num_predict    = 256
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

        return document.RootElement
                       .GetProperty("response")
                       .GetString() ?? "";
    }
}