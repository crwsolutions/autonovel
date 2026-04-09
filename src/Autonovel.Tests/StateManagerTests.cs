namespace Autonovel.Tests;

using Autonovel.Core.Domain;
using Autonovel.Core.Services;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

public class StateManagerTests
{
    private readonly string _testDir;
    private readonly string _stateFile;

    public StateManagerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"autonovel_test_{Guid.NewGuid()}");
        _stateFile = Path.Combine(_testDir, "state.json");
        Directory.CreateDirectory(_testDir);
    }

    [Fact]
    public void Load_CreatesDefault_WhenFileDoesNotExist()
    {
        // Arrange
        var manager = new StateManager(_stateFile);

        // Act
        var state = manager.Load();

        // Assert
        Assert.NotNull(state);
        Assert.Equal("foundation", state.Phase);
        Assert.Equal("", state.CurrentPhase);
        Assert.Equal(0, state.Iteration);
        Assert.True(File.Exists(_stateFile) == false);
    }

    [Fact]
    public void Load_LoadsExistingFile()
    {
        // Arrange
        var existingState = new PipelineState
        {
            Phase = "foundation",
            CurrentPhase = "foundation",
            CurrentFocus = "world_building",
            Iteration = 5,
            FoundationScore = 8.5,
            LoreScore = 7.8,
            LastScore = 8.0,
            ChaptersDrafted = 3,
            ChaptersTotal = 12,
            NovelScore = 0.0,
            RevisionCycle = 0,
            Debts = new List<string> { "fix pacing" },
            PropagationDebts = new List<string>()
        };
        var json = JsonSerializer.Serialize(existingState, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_stateFile, json);

        var manager = new StateManager(_stateFile);

        // Act
        var state = manager.Load();

        // Assert
        Assert.Equal("foundation", state.Phase);
        Assert.Equal("foundation", state.CurrentPhase);
        Assert.Equal("world_building", state.CurrentFocus);
        Assert.Equal(5, state.Iteration);
        Assert.Equal(8.5, state.FoundationScore);
        Assert.Equal(7.8, state.LoreScore);
        Assert.Contains("fix pacing", state.Debts);
    }

    [Fact]
    public void Save_SerializesStateToFile()
    {
        // Arrange
        var manager = new StateManager(_stateFile);
        var state = new PipelineState
        {
            Phase = "drafting",
            CurrentPhase = "drafting",
            Iteration = 2,
            ChaptersDrafted = 5,
            ChaptersTotal = 12,
            FoundationScore = 8.0,
            LoreScore = 7.5,
            LastScore = 7.8,
            NovelScore = 0.0,
            RevisionCycle = 0,
            Debts = new List<string>(),
            PropagationDebts = new List<string>()
        };

        // Act
        manager.Save(state);

        // Assert
        Assert.True(File.Exists(_stateFile));
        var json = File.ReadAllText(_stateFile);
        var loaded = JsonSerializer.Deserialize<PipelineState>(json);
        Assert.Equal("drafting", loaded.Phase);
        Assert.Equal(2, loaded.Iteration);
        Assert.Equal(5, loaded.ChaptersDrafted);
    }

    [Fact]
    public async Task UpdateStateAsync_IncrementsIteration()
    {
        // Arrange
        var manager = new StateManager(_stateFile);
        var state = new PipelineState
        {
            Phase = "foundation",
            CurrentPhase = "foundation",
            Iteration = 3,
            ChaptersDrafted = 0,
            ChaptersTotal = 12,
            FoundationScore = 0,
            LoreScore = 0,
            LastScore = 0,
            NovelScore = 0,
            RevisionCycle = 0,
            Debts = new List<string>(),
            PropagationDebts = new List<string>()
        };
        manager.Save(state);

        // Act
        await manager.UpdateStateAsync(s => s with { Iteration = s.Iteration + 1 });

        // Assert
        var newState = manager.Load();
        Assert.Equal(4, newState.Iteration);
    }

    [Fact]
    public async Task UpdateStateAsync_PersistsPhaseChanges()
    {
        // Arrange
        var manager = new StateManager(_stateFile);
        var state = new PipelineState
        {
            Phase = "foundation",
            CurrentPhase = "foundation",
            Iteration = 0,
            ChaptersDrafted = 0,
            ChaptersTotal = 12,
            FoundationScore = 0,
            LoreScore = 0,
            LastScore = 0,
            NovelScore = 0,
            RevisionCycle = 0,
            Debts = new List<string>(),
            PropagationDebts = new List<string>()
        };
        manager.Save(state);

        // Act
        await manager.UpdateStateAsync(s => s with { CurrentPhase = "revision" });

        // Assert
        var newState = manager.Load();
        Assert.Equal("revision", newState.CurrentPhase);
    }

    [Fact]
    public async Task UpdateStateAsync_AddsDebt()
    {
        // Arrange
        var manager = new StateManager(_stateFile);
        var state = new PipelineState
        {
            Phase = "foundation",
            CurrentPhase = "foundation",
            Iteration = 0,
            ChaptersDrafted = 0,
            ChaptersTotal = 12,
            FoundationScore = 0,
            LoreScore = 0,
            LastScore = 0,
            NovelScore = 0,
            RevisionCycle = 0,
            Debts = new List<string>(),
            PropagationDebts = new List<string>()
        };
        manager.Save(state);

        // Act
        await manager.UpdateStateAsync(s =>
        {
            var newDebts = new List<string>(s.Debts) { "new debt" };
            return s with { Debts = newDebts };
        });

        // Assert
        var newState = manager.Load();
        Assert.Contains("new debt", newState.Debts);
    }

    [Fact]
    public void Load_InvalidJson_ThrowsException()
    {
        // Arrange
        File.WriteAllText(_stateFile, "invalid json content");
        var manager = new StateManager(_stateFile);

        // Act & Assert
        Assert.ThrowsAny<Exception>(() => manager.Load());
    }

    [Fact]
    public void Load_EmptyFile_ReturnsDefault()
    {
        // Arrange
        File.WriteAllText(_stateFile, "");
        var manager = new StateManager(_stateFile);

        // Act
        var state = manager.Load();

        // Assert
        Assert.NotNull(state);
        Assert.Equal("foundation", state.Phase);
    }

    [Fact]
    public void StateManager_CreatesDirectoryIfNeeded()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"autonovel_test_{Guid.NewGuid()}", "state.json");

        // Act
        var manager = new StateManager(nonExistentPath);
        var state = new PipelineState
        {
            Phase = "foundation",
            CurrentPhase = "foundation",
            Iteration = 0,
            ChaptersDrafted = 0,
            ChaptersTotal = 12,
            FoundationScore = 0,
            LoreScore = 0,
            LastScore = 0,
            NovelScore = 0,
            RevisionCycle = 0,
            Debts = new List<string>(),
            PropagationDebts = new List<string>()
        };
        manager.Save(state);

        // Assert
        Assert.True(File.Exists(nonExistentPath));
        File.Delete(nonExistentPath);
    }

    [Fact]
    public async Task MultipleUpdates_AppliedSequentially()
    {
        // Arrange
        var manager = new StateManager(_stateFile);
        var state = new PipelineState
        {
            Phase = "foundation",
            CurrentPhase = "foundation",
            Iteration = 0,
            ChaptersDrafted = 0,
            ChaptersTotal = 12,
            FoundationScore = 0,
            LoreScore = 0,
            LastScore = 0,
            NovelScore = 0,
            RevisionCycle = 0,
            Debts = new List<string>(),
            PropagationDebts = new List<string>()
        };
        manager.Save(state);

        // Act
        await manager.UpdateStateAsync(s => s with { Iteration = s.Iteration + 1 });
        await manager.UpdateStateAsync(s => s with { Iteration = s.Iteration + 1 });
        await manager.UpdateStateAsync(s => s with { Iteration = s.Iteration + 1 });

        // Assert
        var finalState = manager.Load();
        Assert.Equal(3, finalState.Iteration);
    }

 }
