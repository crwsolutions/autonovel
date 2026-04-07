using System.Net.Http;
using System.Text.Json;
using Autonovel.Core.Domain;

namespace Autonovel.Core.Services;

public interface IGenerationClient
{
    Task<string> GenerateAsync(GenerationRequest request, CancellationToken ct = default);
}

public class OpenAiGenerationClient : IGenerationClient
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly string _modelId;
    private readonly string _apiKey;

    public OpenAiGenerationClient(HttpClient httpClient, string endpoint, string modelId, string apiKey)
    {
        _httpClient = httpClient;
        _endpoint = endpoint.TrimEnd('/');
        _modelId = modelId;
        _apiKey = apiKey;
    }

    public async Task<string> GenerateAsync(GenerationRequest request, CancellationToken ct = default)
    {
        var payload = new
        {
            model = _modelId,
            messages = new[]
            {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user", content = request.UserPrompt }
            },
            temperature = request.Temperature ?? 0.7,
            max_tokens = request.MaxTokens
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync($"{_endpoint}/v1/chat/completions", content, ct);
        response.EnsureSuccessStatusCode();
        
        var jsonResult = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<CompletionResponse>(jsonResult);
        return result?.Choices?.FirstOrDefault()?.Message?.Content ?? "";
    }
}

public class CompletionResponse
{
    public List<Choice>? Choices { get; set; }
}

public class Choice
{
    public Message? Message { get; set; }
}

public class Message
{
    public string? Content { get; set; }
}