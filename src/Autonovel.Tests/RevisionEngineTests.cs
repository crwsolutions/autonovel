namespace Autonovel.Tests;

using Autonovel.Core.Domain;
using Autonovel.Core.Services;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

public class RevisionEngineTests
{
    private readonly Mock<IGenerationClient> _mockClient;
    private readonly Mock<IEvaluator> _mockEvaluator;
    private readonly Mock<IFileManager> _mockFileManager;
    private readonly Mock<IVersionControl> _mockVC;
    private readonly RevisionEngine _engine;
    private readonly string _testDir;

    public RevisionEngineTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"autonovel_revision_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(Path.Combine(_testDir, "chapters"));
        Directory.CreateDirectory(Path.Combine(_testDir, "edit_logs"));

        _mockClient = new Mock<IGenerationClient>();
        _mockEvaluator = new Mock<IEvaluator>();
        _mockFileManager = new Mock<IFileManager>();
        _mockVC = new Mock<IVersionControl>();
        _engine = new RevisionEngine(_mockClient.Object, _mockEvaluator.Object, _mockFileManager.Object, _mockVC.Object);
    }

    [Fact]
    public async Task RunRevisionCycleAsync_CompletesSuccessfully()
    {
        _mockFileManager.Setup(x => x.ListChaptersAsync())
            .ReturnsAsync(new List<int> { 1, 2 });

        _mockFileManager.Setup(x => x.ReadFileAsync("chapters/ch_01.md"))
            .ReturnsAsync("Chapter 1 content");
        _mockFileManager.Setup(x => x.ReadFileAsync("chapters/ch_02.md"))
            .ReturnsAsync("Chapter 2 content");

        _mockClient.Setup(x => x.GenerateAsync(
            It.IsAny<GenerationRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(@"{
                ""cuts"": [],
                ""total_cuttable_words"": 0,
                ""tightest_passage"": """",
                ""loosest_passage"": """",
                ""overall_fat_percentage"": 5.0,
                ""one_sentence_verdict"": ""Tight prose""
            }");

        _mockClient.Setup(x => x.GenerateAsync(
            It.IsAny<GenerationRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(@"{
                ""momentum_loss"": """",
                ""earned_ending"": """",
                ""cut_candidate"": """",
                ""missing_scene"": """",
                ""thinnest_character"": """",
                ""best_scene"": """",
                ""worst_scene"": """",
                ""would_recommend"": ""Yes"",
                ""haunts_you"": """",
                ""next_book"": ""Maybe""
            }");

        _mockEvaluator.Setup(x => x.EvaluateChapterAsync(
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChapterEvaluationResult(
                DimensionScores: new Dictionary<string, DimensionScore>(),
                ThreeWeakestSentences: new List<string>(),
                ThreeStrongestSentences: new List<string>(),
                AIPatternsDetected: new List<string>(),
                OverallScore: 7.5,
                RawJudgeScore: 7.5,
                WeakestDimension: "pacing",
                Top3Revisions: new List<string>(),
                NewCanonEntries: new List<string>()));

        var result = await _engine.RunRevisionCycleAsync(1, 5);

        Assert.NotNull(result);
        Assert.Equal(1, result.Cycle);
        Assert.Equal(7.5, result.NovelScore);
    }

    [Fact]
    public async Task RunRevisionCycleAsync_ParsesAdversarialEditResults()
    {
        _mockFileManager.Setup(x => x.ListChaptersAsync())
            .ReturnsAsync(new List<int> { 1 });

        _mockFileManager.Setup(x => x.ReadFileAsync("chapters/ch_01.md"))
            .ReturnsAsync("Chapter content");

        var mockEditResponse = @"{
            ""cuts"": [
                {
                    ""quote"": ""The sword was sharp"",
                    ""type"": ""redundancy"",
                    ""reason"": ""Triple adjective stack"",
                    ""action"": ""cut"",
                    ""rewrite"": ""The sword was sharp.""
                }
            ],
            ""total_cuttable_words"": 50,
            ""tightest_passage"": ""Chapter opening is tight"",
            ""loosest_passage"": ""Middle section drags"",
            ""overall_fat_percentage"": 12.5,
            ""one_sentence_verdict"": ""Good prose with minor issues""
        }";

        _mockClient.Setup(x => x.GenerateAsync(
            It.IsAny<GenerationRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockEditResponse);

        _mockClient.Setup(x => x.GenerateAsync(
            It.IsAny<GenerationRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(@"{
                ""momentum_loss"": """",
                ""earned_ending"": """",
                ""cut_candidate"": """",
                ""missing_scene"": """",
                ""thinnest_character"": """",
                ""best_scene"": """",
                ""worst_scene"": """",
                ""would_recommend"": ""Yes"",
                ""haunts_you"": """",
                ""next_book"": """"
            }");

        _mockEvaluator.Setup(x => x.EvaluateChapterAsync(
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChapterEvaluationResult(
                DimensionScores: new Dictionary<string, DimensionScore>(),
                ThreeWeakestSentences: new List<string>(),
                ThreeStrongestSentences: new List<string>(),
                AIPatternsDetected: new List<string>(),
                OverallScore: 8.0,
                RawJudgeScore: 8.0,
                WeakestDimension: "voice",
                Top3Revisions: new List<string>(),
                NewCanonEntries: new List<string>()));

        var result = await _engine.RunRevisionCycleAsync(1, 5);

        Assert.NotNull(result);
        Assert.Equal(8.0, result.NovelScore);
    }

    [Fact]
    public async Task RunRevisionCycleAsync_SavesEditLogs()
    {
        _mockFileManager.Setup(x => x.ListChaptersAsync())
            .ReturnsAsync(new List<int> { 1 });
        _mockFileManager.Setup(x => x.ReadFileAsync("chapters/ch_01.md"))
            .ReturnsAsync("Chapter content");
        _mockClient.Setup(x => x.GenerateAsync(
            It.IsAny<GenerationRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(@"{""cuts"": [], ""total_cuttable_words"": 0, ""tightest_passage"": """", ""loosest_passage"": """", ""overall_fat_percentage"": 5.0, ""one_sentence_verdict"": ""Good""}");
        _mockClient.Setup(x => x.GenerateAsync(
            It.IsAny<GenerationRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(@"{""momentum_loss"": """", ""earned_ending"": """", ""cut_candidate"": """", ""missing_scene"": """", ""thinnest_character"": """", ""best_scene"": """", ""worst_scene"": """", ""would_recommend"": ""Yes"", ""haunts_you"": """", ""next_book"": """"}");
        _mockEvaluator.Setup(x => x.EvaluateChapterAsync(
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChapterEvaluationResult(
                DimensionScores: new Dictionary<string, DimensionScore>(),
                ThreeWeakestSentences: new List<string>(),
                ThreeStrongestSentences: new List<string>(),
                AIPatternsDetected: new List<string>(),
                OverallScore: 7.0,
                RawJudgeScore: 7.0,
                WeakestDimension: "pacing",
                Top3Revisions: new List<string>(),
                NewCanonEntries: new List<string>()));

        await _engine.RunRevisionCycleAsync(1, 5);

        _mockFileManager.Verify(x => x.WriteFileAsync(
            "edit_logs/ch01_cycle_cuts.json",
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void Constructor_InjectsDependencies()
    {
        var engine = new RevisionEngine(
            _mockClient.Object,
            _mockEvaluator.Object,
            _mockFileManager.Object,
            _mockVC.Object);
        Assert.NotNull(engine);
    }

    [Fact]
    public async Task RunRevisionCycleAsync_HandlesMissingChapters()
    {
        _mockFileManager.Setup(x => x.ListChaptersAsync())
            .ReturnsAsync(new List<int>());

        var result = await _engine.RunRevisionCycleAsync(1, 5);

        Assert.NotNull(result);
        Assert.Equal(1, result.Cycle);
        Assert.Equal(0.0, result.NovelScore);
    }
}
