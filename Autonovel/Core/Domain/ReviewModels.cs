namespace Autonovel.Core.Domain;

public record OpusReviewResult(
    double? StarRating,
    string CriticSummary,
    List<ReviewItem> ProfessorItems,
    int TotalItems,
    int MajorItems,
    int QualifiedItems,
    bool ShouldStop,
    string StopReason,
    string RawText,
    string Timestamp,
    string Title,
    int WordCount
);

public record ReviewItem(
    int Number,
    string Title,
    string Severity,
    string Type,
    bool Qualified,
    string Suggestion,
    string FullText
);
