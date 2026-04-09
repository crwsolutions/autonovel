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

public class PipelineOrchestratorTests
{
    private readonly Mock<IGenerationClient> _mockClient;
    private readonly Mock<IEvaluator> _mockEvaluator;
    private readonly Mock<IStateManager> _mockStateManager;
    private readonly Mock<IVersionControl> _mockVC;
    private readonly Mock<IFileManager> _mockFileManager;
    private readonly Mock<IRevisionEngine> _mockRevisionEngine;
    private readonly Mock<ICanonService> _mockCanonService;
    private readonly PipelineOrchestrator _orchestrator;
    private readonly string _testDir;

    public PipelineOrchestratorTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"autonovel_orchestrator_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(Path.Combine(_testDir, "chapters"));
        
        _mockClient = new Mock<IGenerationClient>();
        _mockEvaluator = new Mock<IEvaluator>();
        _mockStateManager = new Mock<IStateManager>();
        _mockVC = new Mock<IVersionControl>();
        _mockFileManager = new Mock<IFileManager>();
        _mockRevisionEngine = new Mock<IRevisionEngine>();
        _mockCanonService = new Mock<ICanonService>();
        _orchestrator = new PipelineOrchestrator(
            _mockClient.Object,
            _mockEvaluator.Object,
            _mockStateManager.Object,
            _mockVC.Object,
            _mockFileManager.Object,
            _mockRevisionEngine.Object,
            _mockCanonService.Object);
    }

    [Fact]
    public async Task RunFoundationAsync_Succeeds_WhenScoresAboveThreshold()
    {
        var config = new PipelineConfig(
            MaxIterations: 5,
            FoundationThreshold: 7.5f,
            LoreThreshold: 7.0f);

        _mockStateManager.Setup(x => x.UpdateStateAsync(
            It.IsAny<Func<PipelineState, PipelineState>>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockFileManager.Setup(x => x.ReadFileAsync("seed.txt"))
            .ReturnsAsync("A fantasy story about dragons");
        _mockFileManager.Setup(x => x.ReadFileAsync("voice.md"))
            .ReturnsAsync("Voice guide");
        _mockFileManager.Setup(x => x.ReadFileAsync("CRAFT.md"))
            .ReturnsAsync("Craft guidelines");
        _mockFileManager.Setup(x => x.ReadFileAsync("MYSTERY.md"))
            .ReturnsAsync("Mystery outline");

        _mockClient.Setup(x => x.GenerateAsync(
            It.IsAny<GenerationRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync("World content");

        _mockClient.Setup(x => x.GenerateAsync(
            It.IsAny<GenerationRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync("Characters content");

        _mockClient.Setup(x => x.GenerateAsync(
            It.IsAny<GenerationRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync("Outline content");

        _mockEvaluator.Setup(x => x.EvaluateFoundationAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FoundationEvaluationResult(
                DimensionScores: new Dictionary<string, DimensionScore>(),
                SlopInPlanningDocs: new List<string>(),
                ContradictionsFound: new List<string>(),
                OverallScore: 8.5,
                LoreScore: 8.0,
                WeakestDimension: "pacing",
                Top3Improvements: new List<string>(),
                IdentifiedGaps: new List<string>()));

        var result = await _orchestrator.RunFoundationAsync(config);

        Assert.True(result.Success);
        Assert.Equal("foundation", result.Phase);
        Assert.Equal(8.5, result.Score);
        Assert.Contains("passed", result.Message.ToLower());
    }

    [Fact]
    public async Task RunFoundationAsync_Fails_WhenScoresBelowThreshold()
    {
        var config = new PipelineConfig(
            MaxIterations: 2,
            FoundationThreshold: 7.5f,
            LoreThreshold: 7.0f);

        _mockStateManager.Setup(x => x.UpdateStateAsync(
            It.IsAny<Func<PipelineState, PipelineState>>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockFileManager.Setup(x => x.ReadFileAsync(It.IsAny<string>()))
            .ReturnsAsync("Content");

        _mockClient.Setup(x => x.GenerateAsync(
            It.IsAny<GenerationRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync("Content");

        _mockEvaluator.Setup(x => x.EvaluateFoundationAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FoundationEvaluationResult(
                DimensionScores: new Dictionary<string, DimensionScore>(),
                SlopInPlanningDocs: new List<string>(),
                ContradictionsFound: new List<string>(),
                OverallScore: 6.0,
                LoreScore: 5.5,
                WeakestDimension: "pacing",
                Top3Improvements: new List<string>(),
                IdentifiedGaps: new List<string>()));

        _mockVC.Setup(x => x.HardResetAsync())
            .Returns(Task.CompletedTask);

        var result = await _orchestrator.RunFoundationAsync(config);

        Assert.False(result.Success);
        Assert.Contains("failed", result.Message.ToLower());
    }

    [Fact]
    public async Task RunDraftAsync_Succeeds_WhenScoreAboveThreshold()
    {
        var config = new PipelineConfig(DraftThreshold: 6.0f);

        _mockStateManager.Setup(x => x.UpdateStateAsync(
            It.IsAny<Func<PipelineState, PipelineState>>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockFileManager.Setup(x => x.ReadFileAsync("voice.md"))
            .ReturnsAsync("Voice");
        _mockFileManager.Setup(x => x.ReadFileAsync("world.md"))
            .ReturnsAsync("World");
        _mockFileManager.Setup(x => x.ReadFileAsync("characters.md"))
            .ReturnsAsync("Characters");
        _mockFileManager.Setup(x => x.ReadFileAsync("outline.md"))
            .ReturnsAsync("Outline");
        _mockFileManager.Setup(x => x.ReadFileAsync("canon.md"))
            .ReturnsAsync("Canon");
        _mockFileManager.Setup(x => x.ReadFileAsync("chapters/ch_00.md"))
            .ReturnsAsync("");

        _mockClient.Setup(x => x.GenerateAsync(
            It.IsAny<GenerationRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync("Chapter content");

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
                RawJudgeScore: 8.0,
                WeakestDimension: "pacing",
                Top3Revisions: new List<string>(),
                NewCanonEntries: new List<string>()));

        var result = await _orchestrator.RunDraftAsync(1, config);

        Assert.True(result.Success);
        Assert.Equal("drafting", result.Phase);
        Assert.Equal(1, result.Chapter);
        Assert.Equal(7.5, result.Score);
    }

    [Fact]
    public async Task RunDraftAsync_Retries_WhenScoreBelowThreshold()
    {
        var config = new PipelineConfig(DraftThreshold: 7.0f, MaxIterations: 3);

        _mockStateManager.Setup(x => x.UpdateStateAsync(
            It.IsAny<Func<PipelineState, PipelineState>>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockFileManager.Setup(x => x.ReadFileAsync(It.IsAny<string>()))
            .ReturnsAsync("Content");

        _mockClient.Setup(x => x.GenerateAsync(
            It.IsAny<GenerationRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync("Chapter content");

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
                OverallScore: 5.5,
                RawJudgeScore: 6.0,
                WeakestDimension: "pacing",
                Top3Revisions: new List<string>(),
                NewCanonEntries: new List<string>()));

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
                RawJudgeScore: 8.0,
                WeakestDimension: "pacing",
                Top3Revisions: new List<string>(),
                NewCanonEntries: new List<string>()));

        var result = await _orchestrator.RunDraftAsync(1, config);

        Assert.True(result.Success);
        Assert.Equal(7.5, result.Score);
    }

    [Fact]
    public async Task RunRevisionAsync_CompletesCycles()
    {
        var config = new PipelineConfig(RevisionMaxCycles: 3);

        var mockRevisionResult = new RevisionResult(
            Cycle: 1,
            RevisionsApplied: new List<ChapterRevision>(),
            NovelScore: 7.5);

        _mockRevisionEngine.Setup(x => x.RunRevisionCycleAsync(
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockRevisionResult);

        var result = await _orchestrator.RunRevisionAsync(3, config);

        Assert.NotNull(result);
        Assert.Equal("revision", result.Phase);
    }

    [Fact]
    public async Task RunExportAsync_GeneratesManuscript()
    {
        var config = new PipelineConfig();

        _mockFileManager.Setup(x => x.ListChaptersAsync())
            .ReturnsAsync(new List<int> { 1, 2, 3 });

        _mockFileManager.Setup(x => x.ReadFileAsync("chapters/ch_01.md"))
            .ReturnsAsync("Chapter 1");
        _mockFileManager.Setup(x => x.ReadFileAsync("chapters/ch_02.md"))
            .ReturnsAsync("Chapter 2");
        _mockFileManager.Setup(x => x.ReadFileAsync("chapters/ch_03.md"))
            .ReturnsAsync("Chapter 3");

        var result = await _orchestrator.RunExportAsync(config);

        Assert.NotNull(result);
        Assert.Equal("export", result.Phase);
    }

    [Fact]
    public async Task RunFoundationAsync_UpdatesStateOnSuccess()
    {
        var config = new PipelineConfig(FoundationThreshold: 7.5f, LoreThreshold: 7.0f);

        _mockStateManager.Setup(x => x.UpdateStateAsync(
            It.IsAny<Func<PipelineState, PipelineState>>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockFileManager.Setup(x => x.ReadFileAsync(It.IsAny<string>()))
            .ReturnsAsync("Content");

        _mockClient.Setup(x => x.GenerateAsync(
            It.IsAny<GenerationRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync("Content");

        _mockEvaluator.Setup(x => x.EvaluateFoundationAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FoundationEvaluationResult(
                DimensionScores: new Dictionary<string, DimensionScore>(),
                SlopInPlanningDocs: new List<string>(),
                ContradictionsFound: new List<string>(),
                OverallScore: 8.0,
                LoreScore: 7.5,
                WeakestDimension: "pacing",
                Top3Improvements: new List<string>(),
                IdentifiedGaps: new List<string>()));

        await _orchestrator.RunFoundationAsync(config);

        _mockStateManager.Verify(x => x.UpdateStateAsync(
            It.IsAny<Func<PipelineState, PipelineState>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Constructor_InjectsDependencies()
    {
        var orchestrator = new PipelineOrchestrator(
            _mockClient.Object,
            _mockEvaluator.Object,
            _mockStateManager.Object,
            _mockVC.Object,
            _mockFileManager.Object,
            _mockRevisionEngine.Object,
            _mockCanonService.Object);
        Assert.NotNull(orchestrator);
    }

    [Fact]
    public async Task RunFoundationAsync_HandlesCancellation()
    {
        var config = new PipelineConfig();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _orchestrator.RunFoundationAsync(config, cts.Token));
    }

    [Fact]
    public async Task RunDraftAsync_HandlesCancellation()
    {
        var config = new PipelineConfig();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _orchestrator.RunDraftAsync(1, config, cts.Token));
    }
}
