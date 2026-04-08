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

public record AdversarialEditResult(
    List<EditCut> Cuts = null!,
    int TotalCuttableWords = 0,
    string TightestPassage = "",
    string LoosestPassage = "",
    double OverallFatPercentage = 0.0,
    string OneSentenceVerdict = ""
);

public record EditCut(
    string Quote = "",
    string Type = "",
    string Reason = "",
    string Action = "",
    string? Rewrite = null
);

public record ReaderPanelResult(
    Dictionary<string, ReaderResponse> ReaderResponses = null!,
    List<ReaderDisagreement> Disagreements = null!
);

public record ReaderResponse(
    string MomentumLoss = "",
    string EarnedEnding = "",
    string CutCandidate = "",
    string MissingScene = "",
    string ThinnestCharacter = "",
    string BestScene = "",
    string WorstScene = "",
    string WouldRecommend = "",
    string HauntsYou = "",
    string NextBook = ""
);

public record ReaderDisagreement(
    string Question = "",
    int Chapter = 0,
    List<string> FlaggedBy = null!,
    List<string> NotFlagged = null!
);

public record RevisionResult(
    int Cycle = 0,
    List<ChapterRevision> RevisionsApplied = null!,
    double NovelScore = 0.0
);

public record ChapterRevision(
    int Chapter = 0,
    string Issue = "",
    double PreScore = 0.0,
    double PostScore = 0.0,
    bool Success = false
);

public record ConsensusItem(
    int Chapter = 0,
    string Issue = "",
    List<string> FlaggedBy = null!
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