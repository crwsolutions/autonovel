namespace Autonovel.Core.Domain;

public record PipelineState
{
    public string Phase { get; init; } = "foundation";
    public string CurrentPhase { get; init; } = "";
    public string? CurrentFocus { get; init; }
    public int Iteration { get; init; }
    public double FoundationScore { get; init; }
    public double LoreScore { get; init; }
    public double LastScore { get; init; }
    public int ChaptersDrafted { get; init; }
    public int ChaptersTotal { get; init; }
    public double NovelScore { get; init; }
    public int RevisionCycle { get; init; }
    public List<string> Debts { get; init; } = new();
    public List<string> PropagationDebts { get; init; } = new();
}

public static class PipelineStateDefaults
{
    public static PipelineState Default() => new();
}