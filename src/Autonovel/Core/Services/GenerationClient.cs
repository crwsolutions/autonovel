using Autonovel.Core.Domain;
using Microsoft.Extensions.AI;

namespace Autonovel.Core.Services;

public interface IGenerationClient
{
    Task<string> GenerateAsync(GenerationRequest request, CancellationToken ct = default);
}

public class OpenAiGenerationClient : IGenerationClient
{
    private readonly IChatClient _chatClient;

    public OpenAiGenerationClient(IChatClient chatClient)
    {
        _chatClient = chatClient;
    }

    public async Task<string> GenerateAsync(GenerationRequest request, CancellationToken ct = default)
    {
        //var payload = new
        //{
        //    model = _modelId,
        //    messages = new[]
        //    {
        //        new { role = "system", content = request.SystemPrompt },
        //        new { role = "user", content = request.UserPrompt }
        //    },
        //    temperature = request.Temperature ?? 0.7,
        //    max_tokens = request.MaxTokens
        //};

        var response = await _chatClient.GetResponseAsync(
        [
            new ChatMessage(ChatRole.System, request.SystemPrompt),
            new ChatMessage(ChatRole.User, request.UserPrompt)
        ]);
        
        return response.Messages.FirstOrDefault()?.Text ?? "";

        //var jsonResult = await response.Content.ReadAsStringAsync(ct);
        //var result = JsonSerializer.Deserialize<CompletionResponse>(jsonResult);
        //return result?.Choices?.FirstOrDefault()?.Message?.Content ?? "";
    }
}
