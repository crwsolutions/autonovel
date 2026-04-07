namespace Autonovel.Core.Domain;

public record SlopScore(
    Dictionary<string, int> Tier1Hits,
    Dictionary<string, int> Tier2Hits,
    int Tier2ClusterCount,
    List<(string Pattern, int Count)> Tier3Hits,
    int FictionAITellCount,
    int StructuralTicCount,
    int TellingCount,
    double EmDashDensity,
    double SentenceLengthCV,
    double TransitionOpenerRatio,
    double SlopPenalty
);

public record GenerationRequest(
    string SystemPrompt,
    string UserPrompt,
    float? Temperature = null,
    int? MaxTokens = null
);

public record DimensionScore(
    int Score,
    string Gap,
    string Fix,
    string Note
);

public record FoundationEvaluationResult(
    Dictionary<string, DimensionScore> DimensionScores,
    List<string> SlopInPlanningDocs,
    List<string> ContradictionsFound,
    double OverallScore,
    double LoreScore,
    string WeakestDimension,
    List<string> Top3Improvements,
    List<string> IdentifiedGaps
);

public record ChapterEvaluationResult(
    Dictionary<string, DimensionScore> DimensionScores,
    List<string> ThreeWeakestSentences,
    List<string> ThreeStrongestSentences,
    List<string> AIPatternsDetected,
    double OverallScore,
    double RawJudgeScore,
    string WeakestDimension,
    List<string> Top3Revisions,
    List<string> NewCanonEntries
);

public record FullNovelEvaluationResult(
    Dictionary<string, DimensionScore> DimensionScores,
    double NovelScore,
    string WeakestDimension,
    int WeakestChapter,
    string TopSuggestion
);

public record PipelineConfig(
    int? MaxIterations = null,
    float? FoundationThreshold = null,
    float? LoreThreshold = null,
    float? DraftThreshold = null,
    int? RevisionMaxCycles = null
);

public record PipelineResult(
    string Phase = "",
    bool Success = false,
    double Score = 0.0,
    int Chapter = 0,
    string Message = ""
);