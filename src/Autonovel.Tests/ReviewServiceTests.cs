namespace Autonovel.Tests;

using Autonovel.Core.Domain;
using Autonovel.Core.Prompts;
using Autonovel.Core.Services;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

public class ReviewServiceTests
{
    private readonly Mock<IGenerationClient> _mockClient;
    private readonly Mock<IFileManager> _mockFileManager;
    private readonly ReviewService _service;
    private readonly string _testDir;

    public ReviewServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"autonovel_review_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(Path.Combine(_testDir, "chapters"));
        
        _mockClient = new Mock<IGenerationClient>();
        _mockFileManager = new Mock<IFileManager>();
        _service = new ReviewService(_mockClient.Object, _mockFileManager.Object, _testDir);
    }

    [Fact]
    public async Task ReviewAsync_ParsesReviewCorrectly()
    {
        var mockResponse = @"CRITIC'S REVIEW
The manuscript shows promise. The prose is generally strong, though occasionally overwritten.

PROFESSOR'S REVIEW

1. [MAJOR] [COMPRESSION] Chapter 7 drags in the middle section.
   Specific suggestion: Cut the extended dialogue scene where they discuss the plan.

2. [MINOR] [MECHANICAL] Repeated use of suddenly in chapter 3.
   Watch for this tic throughout.

3. [MODERATE] [ADDITION] Chapter 12 needs more emotional payoff.
   The confrontation feels unearned.

Star rating: 4";

        _mockFileManager.Setup(x => x.ListChaptersAsync())
            .ReturnsAsync(new List<int> { 1, 2, 3 });

        _mockFileManager.Setup(x => x.ReadFileAsync("chapters/ch_01.md"))
            .ReturnsAsync("Chapter 1 content");
        _mockFileManager.Setup(x => x.ReadFileAsync("chapters/ch_02.md"))
            .ReturnsAsync("Chapter 2 content");
        _mockFileManager.Setup(x => x.ReadFileAsync("chapters/ch_03.md"))
            .ReturnsAsync("Chapter 3 content");

        _mockClient.Setup(x => x.GenerateAsync(
            It.IsAny<GenerationRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        var result = await _service.ReviewAsync();

        Assert.NotNull(result);
        Assert.Equal(4.0, result.StarRating);
        Assert.Equal(3, result.TotalItems);
        Assert.Equal(1, result.MajorItems);
        Assert.Equal(0, result.QualifiedItems);
        Assert.False(result.ShouldStop);
        Assert.Contains("Chapter 7", result.ProfessorItems[0].Title);
        Assert.Equal("compression", result.ProfessorItems[0].Type);
        Assert.Equal("mechanical", result.ProfessorItems[1].Type);
    }

    [Fact]
    public async Task ReviewAsync_SavesToEditLogs()
    {
        _mockFileManager.Setup(x => x.ListChaptersAsync())
            .ReturnsAsync(new List<int> { 1 });
        _mockFileManager.Setup(x => x.ReadFileAsync("chapters/ch_01.md"))
            .ReturnsAsync("Chapter content");
        _mockClient.Setup(x => x.GenerateAsync(
            It.IsAny<GenerationRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(@"CRITIC'S REVIEW
Good work.

PROFESSOR'S REVIEW
1. [MINOR] [COMPRESSION] Tighten chapter 1.

Star rating: 4.5");

        await _service.ReviewAsync();

        var logsDir = Path.Combine(_testDir, "edit_logs");
        Assert.True(Directory.Exists(logsDir));
        var reviewFiles = Directory.GetFiles(logsDir, "*_review.json");
        Assert.Single(reviewFiles);
    }

    [Fact]
    public async Task ReviewAsync_StopsWhenQualified()
    {
        var mockResponse = @"CRITIC'S REVIEW
Each instance works individually fine.

PROFESSOR'S REVIEW
1. [MINOR] [COMPRESSION] A small cut here.
   This is individually fine.

Star rating: 4";

        _mockFileManager.Setup(x => x.ListChaptersAsync())
            .ReturnsAsync(new List<int> { 1 });
        _mockFileManager.Setup(x => x.ReadFileAsync("chapters/ch_01.md"))
            .ReturnsAsync("Chapter content");
        _mockClient.Setup(x => x.GenerateAsync(
            It.IsAny<GenerationRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        var result = await _service.ReviewAsync();

        Assert.True(result.ShouldStop);
        Assert.Contains("qualified", result.StopReason.ToLower());
    }

    [Fact]
    public async Task ReviewAsync_StopsWhenOnlyTwoItems()
    {
        var mockResponse = @"CRITIC'S REVIEW
Good.

PROFESSOR'S REVIEW
1. [MINOR] [COMPRESSION] Cut here.

Star rating: 3";

        _mockFileManager.Setup(x => x.ListChaptersAsync())
            .ReturnsAsync(new List<int> { 1 });
        _mockFileManager.Setup(x => x.ReadFileAsync("chapters/ch_01.md"))
            .ReturnsAsync("Chapter content");
        _mockClient.Setup(x => x.GenerateAsync(
            It.IsAny<GenerationRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        var result = await _service.ReviewAsync();

        Assert.True(result.ShouldStop);
        Assert.Contains("items found", result.StopReason);
    }

    [Fact]
    public async Task ReviewAsync_ExtractsTitleFromOutline()
    {
        var outlinePath = Path.Combine(_testDir, "outline.md");
        File.WriteAllText(outlinePath, "# My Epic Fantasy Novel\n\nChapter outline...");

        _mockFileManager.Setup(x => x.ListChaptersAsync())
            .ReturnsAsync(new List<int> { 1 });
        _mockFileManager.Setup(x => x.ReadFileAsync("chapters/ch_01.md"))
            .ReturnsAsync("Chapter content");
        _mockClient.Setup(x => x.GenerateAsync(
            It.IsAny<GenerationRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(@"CRITIC'S REVIEW
Good.

PROFESSOR'S REVIEW
1. [MINOR] [COMPRESSION] Cut here.

Star rating: 4");

        var result = await _service.ReviewAsync();

        Assert.Equal("My Epic Fantasy Novel", result.Title);
    }

    [Fact]
    public async Task ReviewAsync_ExtractsTitleFromChapter1()
    {
        var ch1Path = Path.Combine(_testDir, "chapters", "ch_01.md");
        File.WriteAllText(ch1Path, "# The Dragon's Legacy\n\nOnce upon a time...");

        _mockFileManager.Setup(x => x.ListChaptersAsync())
            .ReturnsAsync(new List<int> { 1 });
        _mockFileManager.Setup(x => x.ReadFileAsync("chapters/ch_01.md"))
            .ReturnsAsync("Chapter content");
        _mockClient.Setup(x => x.GenerateAsync(
            It.IsAny<GenerationRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(@"CRITIC'S REVIEW
Good.

PROFESSOR'S REVIEW
1. [MINOR] [COMPRESSION] Cut here.

Star rating: 4");

        var result = await _service.ReviewAsync();

        Assert.Equal("The Dragon's Legacy", result.Title);
    }

    [Fact]
    public async Task ReviewAsync_UsesDefaultTitle_WhenNoTitleFound()
    {
        _mockFileManager.Setup(x => x.ListChaptersAsync())
            .ReturnsAsync(new List<int> { 1 });
        _mockFileManager.Setup(x => x.ReadFileAsync("chapters/ch_01.md"))
            .ReturnsAsync("No title here");
        _mockClient.Setup(x => x.GenerateAsync(
            It.IsAny<GenerationRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(@"CRITIC'S REVIEW
Good.

PROFESSOR'S REVIEW
1. [MINOR] [COMPRESSION] Cut here.

Star rating: 4");

        var result = await _service.ReviewAsync();

        Assert.Equal("Untitled Novel", result.Title);
    }

    [Fact]
    public async Task ReviewAsync_SavesHumanReadableCopy()
    {
        var outputPath = Path.Combine(_testDir, "review_output.txt");
        
        _mockFileManager.Setup(x => x.ListChaptersAsync())
            .ReturnsAsync(new List<int> { 1 });
        _mockFileManager.Setup(x => x.ReadFileAsync("chapters/ch_01.md"))
            .ReturnsAsync("Chapter content");
        _mockClient.Setup(x => x.GenerateAsync(
            It.IsAny<GenerationRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(@"CRITIC'S REVIEW
Good.

PROFESSOR'S REVIEW
1. [MINOR] [COMPRESSION] Cut here.

Star rating: 4");

        await _service.ReviewAsync(outputPath);

        Assert.True(File.Exists(outputPath));
        var content = File.ReadAllText(outputPath);
        Assert.Contains("CRITIC'S REVIEW", content);
    }

    [Fact]
    public async Task ReviewAsync_HandlesEmptyChapterList()
    {
        _mockFileManager.Setup(x => x.ListChaptersAsync())
            .ReturnsAsync(new List<int>());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.ReviewAsync());
    }

    [Fact]
    public void Constructor_InjectsDependencies()
    {
        var service = new ReviewService(
            _mockClient.Object, 
            _mockFileManager.Object, 
            _testDir);
        Assert.NotNull(service);
    }

    [Fact]
    public async Task ReviewAsync_ParsesStarRatingWithHalfStar()
    {
        var mockResponse = @"CRITIC'S REVIEW
Excellent work.

PROFESSOR'S REVIEW
1. [MINOR] [COMPRESSION] Small cut.

Star rating: 4.5";

        _mockFileManager.Setup(x => x.ListChaptersAsync())
            .ReturnsAsync(new List<int> { 1 });
        _mockFileManager.Setup(x => x.ReadFileAsync("chapters/ch_01.md"))
            .ReturnsAsync("Chapter content");
        _mockClient.Setup(x => x.GenerateAsync(
            It.IsAny<GenerationRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        var result = await _service.ReviewAsync();

        Assert.Equal(4.5, result.StarRating);
    }

    [Fact]
    public async Task ReviewAsync_ParsesNumericStarRating()
    {
        var mockResponse = @"CRITIC'S REVIEW
Good.

PROFESSOR'S REVIEW
1. [MINOR] [COMPRESSION] Cut here.

Star rating: 4/5";

        _mockFileManager.Setup(x => x.ListChaptersAsync())
            .ReturnsAsync(new List<int> { 1 });
        _mockFileManager.Setup(x => x.ReadFileAsync("chapters/ch_01.md"))
            .ReturnsAsync("Chapter content");
        _mockClient.Setup(x => x.GenerateAsync(
            It.IsAny<GenerationRequest>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        var result = await _service.ReviewAsync();

        Assert.Equal(4.0, result.StarRating);
    }

    [Fact]
    public async Task ParseLatestAsync_ThrowsWhenNoLogs()
    {
        var nonExistentDir = Path.Combine(Path.GetTempPath(), $"autonovel_nologs_{Guid.NewGuid()}");
        var service = new ReviewService(_mockClient.Object, _mockFileManager.Object, nonExistentDir);

        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => service.ParseLatestAsync());
    }
}
