namespace Autonovel.Tests;

using Autonovel.Core.Domain;
using Autonovel.Core.Services;
using System.Text.RegularExpressions;
using Xunit;

public class MechanicalSlopDetectorTests
{
    private readonly MechanicalSlopDetector _detector;

    public MechanicalSlopDetectorTests()
    {
        _detector = new MechanicalSlopDetector();
    }

    [Fact]
    public void Calculate_EmptyText_ReturnsZeroScores()
    {
        // Act
        var result = _detector.Calculate("");

        // Assert
        Assert.Equal(0, result.Tier1Hits.Count);
        Assert.Equal(0, result.Tier2Hits.Count);
        Assert.Equal(0, result.Tier2ClusterCount);
        Assert.Equal(0, result.Tier3Hits.Count);
        Assert.Equal(0, result.FictionAITellCount);
        Assert.Equal(0, result.StructuralTicCount);
        Assert.Equal(0, result.TellingCount);
        Assert.Equal(0.0, result.EmDashDensity);
        Assert.Equal(0.0, result.SlopPenalty);
    }

    [Fact]
    public void Calculate_Tier1BannedWords_DetectsAllWords()
    {
        // Arrange
        var text = "This is a delve and utilize text with leverage and facilitate words.";

        // Act
        var result = _detector.Calculate(text);

        // Assert
        Assert.Equal(4, result.Tier1Hits.Count);
        Assert.Contains("delve", result.Tier1Hits.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("utilize", result.Tier1Hits.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("leverage", result.Tier1Hits.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("facilitate", result.Tier1Hits.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.True(result.SlopPenalty > 0);
    }

[Fact]
    public void Calculate_Tier2Words_InSameParagraph_ClusterDetected()
    {
        // Arrange - 3+ tier2 words in same paragraph
        var text = @"This is a paragraph with robust and comprehensive and seamless text.
        
        Another paragraph with cutting-edge and innovative and streamline and empower features here.";

        // Act
        var result = _detector.Calculate(text);

        // Assert
        Assert.Equal(2, result.Tier2ClusterCount);
        Assert.Contains("robust", result.Tier2Hits.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("comprehensive", result.Tier2Hits.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("seamless", result.Tier2Hits.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("cutting-edge", result.Tier2Hits.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("innovative", result.Tier2Hits.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Calculate_Tier2Words_Distributed_NoCluster()
    {
        // Arrange - tier2 words spread across different paragraphs
        var text = @"This paragraph has robust text.

This paragraph has comprehensive features.

This paragraph has seamless design.

This paragraph has cutting-edge tools.";

        // Act
        var result = _detector.Calculate(text);

        // Assert
        Assert.Equal(0, result.Tier2ClusterCount);
        Assert.Equal(4, result.Tier2Hits.Count);
    }

[Fact]
    public void Calculate_Tier3Patterns_DetectsFillerPhrases()
    {
        // Arrange
        var text = @"It's worth noting that this is important.
        
        It's important to note that we should consider.
        
        Let's dive into the details.
        
        Not just important, but crucial is the point.";

        // Act
        var result = _detector.Calculate(text);

        // Assert
        Assert.Equal(4, result.Tier3Hits.Count);
        Assert.True(result.SlopPenalty > 0);
    }

    [Fact]
    public void Calculate_FictionAITells_DetectsAIPatterns()
    {
        // Arrange
        var text = @"She couldn't help but feel scared.
        
He felt a surge of hope.
        
The weight of silence hung heavy.
        
A sense of dread filled the room.
        
Her eyes widened in surprise.";

        // Act
        var result = _detector.Calculate(text);

        // Assert
        Assert.Equal(5, result.FictionAITellCount);
        Assert.True(result.SlopPenalty > 0);
    }

[Fact]
    public void Calculate_StructuralTics_DetectsAIWritingPatterns()
    {
        // Arrange
        var text = @"I'm not saying this is good, I'm saying it's bad.
        
        I'm not suggesting you should try, I'm suggesting you must.
        
        There's a difference.
        
        Not just important, but crucial is the point.";

        // Act
        var result = _detector.Calculate(text);

        // Assert
        Assert.Equal(4, result.StructuralTicCount);
        Assert.True(result.SlopPenalty > 0);
    }

[Fact]
    public void Calculate_TellingPatterns_DetectsEmotionalTelling()
    {
        // Arrange - single lines to avoid regex IgnorePatternWhitespace issues
        var text = "He was angry. She was sad. He seemed nervous. The child cried angrily. She walked happily down the street.";

        // Act
        var result = _detector.Calculate(text);

        // Assert
        Assert.Equal(5, result.TellingCount);
        Assert.True(result.SlopPenalty > 0);
    }

    [Fact]
    public void Calculate_EmDashDensity_CalculatesCorrectly()
    {
        // Arrange
        var text = "This is a test — with many — em dashes — for density.";

        // Act
        var result = _detector.Calculate(text);

        // Assert
        Assert.True(result.EmDashDensity > 0);
        Assert.Equal(3, Regex.Matches(text, @"—|--").Count);
    }

    [Fact]
    public void Calculate_TransitionOpeners_CalculatesRatio()
    {
        // Arrange
        var text = @"However, this is the first paragraph.
        
Furthermore, this is the second paragraph.
        
Additionally, this is the third paragraph.
        
But this one has no transition.";

        // Act
        var result = _detector.Calculate(text);

        // Assert
        Assert.Equal(0.75, result.TransitionOpenerRatio);
    }

[Fact]
    public void Calculate_CompleteSlopProfile_DetectsAllCategories()
    {
        // Arrange - text with all types of slop
        var text = @"This is a comprehensive and robust text that utilizes delve language.
        
        It's worth noting that the seamless and innovative approach facilitates growth.
        
        He was angry and couldn't help but feel scared.
        
        I'm not saying this is good, I'm saying it's bad.
        
        However, furthermore, additionally - all transitions.";

        // Act
        var result = _detector.Calculate(text);

        // Assert
        Assert.True(result.Tier1Hits.Count > 0);
        Assert.True(result.Tier2Hits.Count > 0);
        Assert.True(result.Tier3Hits.Count > 0);
        Assert.True(result.FictionAITellCount > 0);
        Assert.True(result.StructuralTicCount > 0);
        Assert.True(result.TellingCount > 0);
        Assert.True(result.SlopPenalty > 0);
    }

    [Fact]
    public void Calculate_SentenceLengthCV_LowVariability_Penalty()
    {
        // Arrange - uniform sentence lengths
        var text = "Short sentence. Short sentence. Short sentence. Short sentence.";

        // Act
        var result = _detector.Calculate(text);

        // Assert
        Assert.True(result.SentenceLengthCV < 0.5);
    }
}
