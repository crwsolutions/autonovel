using System.Text;
using Autonovel.Core.Domain;

namespace Autonovel.Core.Services;

public interface ICanonService
{
    Task<string> GenerateCanonAsync(string seed, string worldContent, string charContent, CancellationToken ct = default);
}

public class CanonService : ICanonService
{
    private readonly IGenerationClient _client;

    public CanonService(IGenerationClient client)
    {
        _client = client;
    }

    public async Task<string> GenerateCanonAsync(string seed, string worldContent, string charContent, CancellationToken ct = default)
    {
        var systemPrompt = @"You are a continuity editor extracting hard facts from fantasy novel planning documents. You are precise, exhaustive, and never invent facts that aren't in the source material. Every entry must be traceable to a specific statement in the source documents.";

        var userPrompt = BuildCanonPrompt(seed, worldContent, charContent);

        return await _client.GenerateAsync(new GenerationRequest(
            SystemPrompt: systemPrompt,
            UserPrompt: userPrompt,
            Temperature: 0.5f), ct);
    }

    private static string BuildCanonPrompt(string seed, string world, string characters)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Extract EVERY hard fact from these planning documents into a structured canon database.");
        sb.AppendLine("A 'hard fact' is anything a writer must not contradict: names, ages, dates, physical descriptions,");
        sb.AppendLine("rules of the magic system, geography, relationships, established events.");
        sb.AppendLine();
        sb.AppendLine("SOURCE DOCUMENTS:");
        sb.AppendLine();
        sb.AppendLine("=== SEED.TXT ===");
        sb.AppendLine(seed);
        sb.AppendLine();
        sb.AppendLine("=== WORLD.MD ===");
        sb.AppendLine(world);
        sb.AppendLine();
        sb.AppendLine("=== CHARACTERS.MD ===");
        sb.AppendLine(characters);
        sb.AppendLine();
        sb.AppendLine("FORMAT THE OUTPUT AS CANON.MD with these categories:");
        sb.AppendLine();
        sb.AppendLine("## Geography");
        sb.AppendLine("- Specific facts about locations, distances, physical properties");
        sb.AppendLine();
        sb.AppendLine("## Timeline");
        sb.AppendLine("- Dated events, ages, durations");
        sb.AppendLine();
        sb.AppendLine("## Magic System Rules");
        sb.AppendLine("- Hard rules of the magic system (costs, limitations)");
        sb.AppendLine("- Protagonist's gift/special ability specifics");
        sb.AppendLine();
        sb.AppendLine("## Character Facts");
        sb.AppendLine("- Ages, physical descriptions, habits, relationships");
        sb.AppendLine("- One entry per fact (not paragraphs)");
        sb.AppendLine();
        sb.AppendLine("## Political / Factional");
        sb.AppendLine("- Who controls what, alliances, conflicts, contracts");
        sb.AppendLine();
        sb.AppendLine("## Cultural");
        sb.AppendLine("- Customs, taboos, laws, festivals, food, clothing");
        sb.AppendLine();
        sb.AppendLine("## Established In-Story");
        sb.AppendLine("- Events that have already happened in the story's past");
        sb.AppendLine("- Key contracts, historical wars, etc.");
        sb.AppendLine();
        sb.AppendLine("RULES:");
        sb.AppendLine("- One fact per bullet point. Short. Specific. Checkable.");
        sb.AppendLine("- Include the source (world.md or characters.md) in parentheses after each fact.");
        sb.AppendLine("- Aim for 80-120 entries minimum. Be exhaustive.");
        sb.AppendLine("- If two documents give slightly different details, note the discrepancy.");
        sb.AppendLine("- DO NOT invent facts. Only record what's explicitly stated.");
        
        return sb.ToString();
    }
}
