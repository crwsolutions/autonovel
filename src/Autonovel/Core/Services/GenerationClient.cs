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
        var options = new Microsoft.Extensions.AI.ChatOptions
        {
            Temperature = request.Temperature,
            AdditionalProperties = new(),
        };

        if (request.MaxTokens.HasValue)
        {
            options.AdditionalProperties["max_tokens"] = request.MaxTokens.Value;
        }

        var response = await _chatClient.GetResponseAsync(
        [
            new ChatMessage(ChatRole.System, request.SystemPrompt),
            new ChatMessage(ChatRole.User, request.UserPrompt)
        ], options);

        var text = response.Messages.FirstOrDefault()?.Text ?? "";

        // Post-generation token-based truncation as a safety net
        if (request.MaxTokens.HasValue && text.Length > 0)
        {
            // Rough heuristic: ~1.3 tokens per word, so max_tokens=5000 ~ 3846 words ~ ~16000 chars
            // But since we're measuring chars not tokens, use a conservative ratio:
            // ~4 chars per token on average for English text
            var maxChars = request.MaxTokens.Value * 4;
            if (text.Length > maxChars)
            {
                // Truncate at a paragraph boundary
                var truncated = text.Substring(0, maxChars);
                var lastParagraphEnd = truncated.LastIndexOf("\n\n");
                if (lastParagraphEnd > maxChars * 0.8)
                {
                    truncated = truncated.Substring(0, lastParagraphEnd);
                }
                return truncated.TrimEnd();
            }
        }

        return text;
    }
}
