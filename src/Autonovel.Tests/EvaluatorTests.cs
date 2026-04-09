namespace Autonovel.Tests;

using Autonovel.Core.Domain;
using Autonovel.Core.Services;
using Moq;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

public class EvaluatorTests
{
    private readonly Mock<IGenerationClient> _mockClient;
    private readonly Mock<IMechanicalSlopDetector> _mockSlopDetector;
    private readonly Evaluator _evaluator;

    public EvaluatorTests()
    {
        _mockClient = new Mock<IGenerationClient>();
        _mockSlopDetector = new Mock<IMechanicalSlopDetector>();
        _evaluator = new Evaluator(_mockClient.Object, _mockSlopDetector.Object);
    }

    [Fact]
    public async Task EvaluateFoundationAsync_ParsesJsonCorrectly()
    {
        // Arrange
        var mockResponse = @"{
            ""magic_system"": {""score"": 8, ""gap"": "", ""fix"": "", ""note"": ""Good magic system""},
            ""world_history"": {""score"": 7, ""gap"": "", ""fix"": "", ""note"": """"},
            ""geography_and_culture"": {""score"": 9, ""gap"": "", ""fix"": "", ""note"": """"},
            ""lore_interconnection"": {""score"": 8, ""gap"": "", ""fix"": "", ""note"": """"},
            ""iceberg_depth"": {""score"": 7, ""gap"": "", ""fix"": "", ""note"": """"},
            ""character_depth"": {""score"": 8, ""gap"": "", ""fix"": "", ""note"": """"},
            ""character_distinctiveness"": {""score"": 9, ""gap"": "", ""fix"": "", ""note"": """"},
            ""character_secrets"": {""score"": 7, ""gap"": "", ""fix"": "", ""note"": """"},
            ""outline_completeness"": {""score"": 8, ""gap"": "", ""fix"": "", ""note"": """"},
            ""foreshadowing_balance"": {""score"": 7, ""gap"": "", ""fix"": "", ""note"": """"},
            ""internal_consistency"": {""score"": 9, ""gap"": "", ""fix"": "", ""note"": """"},
            ""voice_clarity"": {""score"": 8, ""gap"": "", ""fix"": "", ""note"": """"},
            ""canon_coverage"": {""score"": 8, ""gap"": "", ""fix"": "", ""note"": """"},
            ""slop_in_planning_docs"": {""found"": []},
            ""contradictions_found"": [],
            ""top_3_improvements"": [],
            ""overall_score"": 8.0,
            ""lore_score"": 7.8,
            ""weakest_dimension"": ""iceberg_depth""
        }";

        _mockClient.Setup(x => x.GenerateAsync(
            It.IsAny<GenerationRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _evaluator.EvaluateFoundationAsync(
            "voice content", "world content", "characters content", 
            "outline content", "canon content");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(8.0, result.OverallScore);
        Assert.Equal(7.8, result.LoreScore);
        Assert.Equal("iceberg_depth", result.WeakestDimension);
        Assert.Equal(13, result.DimensionScores.Count);
        Assert.Contains("magic_system", result.DimensionScores.Keys);
    }

    [Fact]
    public async Task EvaluateFoundationAsync_HandlesEmptyJson()
    {
        // Arrange
        _mockClient.Setup(x => x.GenerateAsync(
            It.IsAny<GenerationRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync("{}");

        // Act
        var result = await _evaluator.EvaluateFoundationAsync(
            "voice", "world", "characters", "outline", "canon");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0.0, result.OverallScore);
        Assert.Equal("parse_error", result.WeakestDimension);
    }

    [Fact]
    public async Task EvaluateChapterAsync_CalculatesSlopPenalty()
    {
        // Arrange
        var mockResponse = @"{
            ""voice_adherence"": {""score"": 8, ""gap"": "", ""fix"": "", ""note"": """"},
            ""beat_coverage"": {""score"": 7, ""gap"": "", ""fix"": "", ""note"": """"},
            ""character_voice"": {""score"": 9, ""gap"": "", ""fix"": "", ""note"": """"},
            ""plants_seeded"": {""score"": 8, ""gap"": "", ""fix"": "", ""note"": """"},
            ""prose_quality"": {""score"": 8, ""gap"": "", ""fix"": "", ""note"": """"},
            ""continuity"": {""score"": 9, ""gap"": "", ""fix"": "", ""note"": """"},
            ""canon_compliance"": {""score"": 8, ""gap"": "", ""fix"": "", ""note"": """"},
            ""lore_integration"": {""score"": 7, ""gap"": "", ""fix"": "", ""note"": """"},
            ""engagement"": {""score"": 8, ""gap"": "", ""fix"": "", ""note"": """"},
            ""three_weakest_sentences"": [],
            ""three_strongest_sentences"": [],
            ""ai_patterns_detected"": [],
            ""top_3_revisions"": [],
            ""new_canon_entries"": [],
            ""overall_score"": 8.0,
            ""weakest_dimension"": ""beat_coverage""
        }";

        _mockClient.Setup(x => x.GenerateAsync(
            It.IsAny<GenerationRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        _mockSlopDetector.Setup(x => x.Calculate(It.IsAny<string>()))
            .Returns(new SlopScore(
                new Dictionary<string, int>(),
                new Dictionary<string, int>(),
                0,
                new List<(string, int)>(),
                0, 0, 0,
                0.0, 0.0, 0.0,
                2.0));

        // Act
        var result = await _evaluator.EvaluateChapterAsync(
            "chapter text", 1, "voice", "world", "characters", 
            "canon", "outline", "tail");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(6.0, result.OverallScore); // 8.0 - 2.0 slop penalty
        Assert.Equal(8.0, result.RawJudgeScore);
    }

    [Fact]
    public async Task EvaluateChapterAsync_ParsesAllSentences()
    {
        // Arrange
        var mockResponse = @"{
            ""voice_adherence"": {""score"": 8, ""gap"": "", ""fix"": "", ""note"": """"},
            ""beat_coverage"": {""score"": 7, ""gap"": "", ""fix"": "", ""note"": """"},
            ""character_voice"": {""score"": 9, ""gap"": "", ""fix"": "", ""note"": """"},
            ""plants_seeded"": {""score"": 8, ""gap"": "", ""fix"": "", ""note"": """"},
            ""prose_quality"": {""score"": 8, ""gap"": "", ""fix"": "", ""note"": """"},
            ""continuity"": {""score"": 9, ""gap"": "", ""fix"": "", ""note"": """"},
            ""canon_compliance"": {""score"": 8, ""gap"": "", ""fix"": "", ""note"": """"},
            ""lore_integration"": {""score"": 7, ""gap"": "", ""fix"": "", ""note"": """"},
            ""engagement"": {""score"": 8, ""gap"": "", ""fix"": "", ""note"": """"},
            ""three_weakest_sentences"": [""Sentence 1"", ""Sentence 2"", ""Sentence 3""],
            ""three_strongest_sentences"": [""Sentence A"", ""Sentence B"", ""Sentence C""],
            ""ai_patterns_detected"": [""Pattern 1"", ""Pattern 2""],
            ""top_3_revisions"": [""Rev 1"", ""Rev 2"", ""Rev 3""],
            ""new_canon_entries"": [""Canon 1"", ""Canon 2""],
            ""overall_score"": 8.0,
            ""weakest_dimension"": ""beat_coverage""
        }";

        _mockClient.Setup(x => x.GenerateAsync(
            It.IsAny<GenerationRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        _mockSlopDetector.Setup(x => x.Calculate(It.IsAny<string>()))
            .Returns(new SlopScore(
                new Dictionary<string, int>(),
                new Dictionary<string, int>(),
                0,
                new List<(string, int)>(),
                0, 0, 0,
                0.0, 0.0, 0.0,
                0.0));

        // Act
        var result = await _evaluator.EvaluateChapterAsync(
            "chapter text", 1, "voice", "world", "characters",
            "canon", "outline", "tail");

        // Assert
        Assert.Equal(3, result.ThreeWeakestSentences.Count);
        Assert.Equal(3, result.ThreeStrongestSentences.Count);
        Assert.Equal(2, result.AIPatternsDetected.Count);
        Assert.Equal(3, result.Top3Revisions.Count);
        Assert.Equal(2, result.NewCanonEntries.Count);
    }

    [Fact]
    public async Task EvaluateFullNovelAsync_ParsesCorrectly()
    {
        // Arrange
        var chapterSummaries = new System.Collections.Generic.Dictionary<int, string>
        {
            { 1, "Chapter 1 summary" },
            { 2, "Chapter 2 summary" },
            { 3, "Chapter 3 summary" }
        };

        var mockResponse = @"{
            ""arc_completion"": {""score"": 8, ""note"": """"},
            ""pacing_curve"": {""score"": 7, ""note"": """"},
            ""theme_coherence"": {""score"": 9, ""note"": """"},
            ""foreshadowing_resolution"": {""score"": 8, ""note"": """"},
            ""world_consistency"": {""score"": 8, ""note"": """"},
            ""voice_consistency"": {""score"": 9, ""note"": """"},
            ""overall_engagement"": {""score"": 8, ""note"": """"},
            ""novel_score"": 8.1,
            ""weakest_dimension"": ""pacing_curve"",
            ""weakest_chapter"": 3,
            ""top_suggestion"": ""Improve pacing""
        }";

        _mockClient.Setup(x => x.GenerateAsync(
            It.IsAny<GenerationRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _evaluator.EvaluateFullNovelAsync(
            "voice", "world", "characters", "outline", chapterSummaries);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(8.1, result.NovelScore);
        Assert.Equal("pacing_curve", result.WeakestDimension);
        Assert.Equal(3, result.WeakestChapter);
        Assert.Equal("Improve pacing", result.TopSuggestion);
        Assert.Equal(7, result.DimensionScores.Count);
    }

    [Fact]
    public async Task EvaluateChapterAsync_HandlesMarkdownJson()
    {
        // Arrange
        var mockResponse = @"```json
{
    ""voice_adherence"": {""score"": 8, ""gap"": "", ""fix"": "", ""note"": """"},
    ""beat_coverage"": {""score"": 7, ""gap"": "", ""fix"": "", ""note"": """"},
    ""character_voice"": {""score"": 9, ""gap"": "", ""fix"": "", ""note"": """"},
    ""plants_seeded"": {""score"": 8, ""gap"": "", ""fix"": "", ""note"": """"},
    ""prose_quality"": {""score"": 8, ""gap"": "", ""fix"": "", ""note"": """"},
    ""continuity"": {""score"": 9, ""gap"": "", ""fix"": "", ""note"": """"},
    ""canon_compliance"": {""score"": 8, ""gap"": "", ""fix"": "", ""note"": """"},
    ""lore_integration"": {""score"": 7, ""gap"": "", ""fix"": "", ""note"": """"},
    ""engagement"": {""score"": 8, ""gap"": "", ""fix"": "", ""note"": """"},
    ""three_weakest_sentences"": [],
    ""three_strongest_sentences"": [],
    ""ai_patterns_detected"": [],
    ""top_3_revisions"": [],
    ""new_canon_entries"": [],
    ""overall_score"": 8.0,
    ""weakest_dimension"": ""beat_coverage""
}
```";

        _mockClient.Setup(x => x.GenerateAsync(
            It.IsAny<GenerationRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        _mockSlopDetector.Setup(x => x.Calculate(It.IsAny<string>()))
            .Returns(new SlopScore(
                new Dictionary<string, int>(),
                new Dictionary<string, int>(),
                0,
                new List<(string, int)>(),
                0, 0, 0,
                0.0, 0.0, 0.0,
                0.0));

        // Act
        var result = await _evaluator.EvaluateChapterAsync(
            "chapter text", 1, "voice", "world", "characters",
            "canon", "outline", "tail");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(8.0, result.OverallScore);
    }

    [Fact]
    public void Constructor_InjectsDependencies()
    {
        // Act & Assert - If constructor succeeds, dependencies are injected correctly
        var evaluator = new Evaluator(_mockClient.Object, _mockSlopDetector.Object);
        Assert.NotNull(evaluator);
    }
}
