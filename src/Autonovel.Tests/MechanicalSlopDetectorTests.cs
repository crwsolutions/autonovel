using Xunit;
using FluentAssertions;
using Autonovel.Core.Services;

namespace Autonovel.Tests;

public class MechanicalSlopDetectorTests
{
    private readonly MechanicalSlopDetector _detector;

    public MechanicalSlopDetectorTests()
    {
        _detector = new MechanicalSlopDetector();
    }

    [Fact]
    public void Calculate_DetectsTier1BannedWords()
    {
        var text = "We must delve into the depths of this issue. " +
                   "The team will utilize their resources effectively. " +
                   "It is crucial to leverage our advantages. " +
                   "This approach facilitates better understanding. ";

        var result = _detector.Calculate(text);

        result.Tier1Hits.Should().NotBeEmpty();
        result.Tier1Hits.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Calculate_DetectsTier2BannedWords()
    {
        var text = "The character was able to walk through the door. " +
                   "He began to run toward the castle. " +
                   "She started to feel afraid. ";

        var result = _detector.Calculate(text);

        result.Tier2Hits.Should().NotBeEmpty();
    }

    [Fact]
    public void Calculate_DetectsTier3BannedWords()
    {
        var text = "The scene was quite dramatic. It was rather interesting. The character was fairly brave.";

        var result = _detector.Calculate(text);

        result.Tier3Hits.Should().NotBeEmpty();
    }

    [Fact]
    public void Calculate_ScoresCleanTextWithNoHits()
    {
        var text = "The sword gleamed in the moonlight. She drew it slowly, feeling the weight of centuries in her hand.";

        var result = _detector.Calculate(text);

        result.Tier1Hits.Should().BeEmpty();
        result.Tier2Hits.Should().BeEmpty();
        result.Tier3Hits.Should().BeEmpty();
    }

    [Fact]
    public void Calculate_DetectsFictionAITells()
    {
        var text = "Little did she know that this would be the beginning of her journey. " +
                   "The sword was not just a weapon, but a symbol of hope.";

        var result = _detector.Calculate(text);

        result.FictionAITellCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Calculate_DetectsStructuralTics()
    {
        var text = "The sword was old. It was sharp. It was deadly. She was brave. She was skilled. She was ready.";

        var result = _detector.Calculate(text);

        result.StructuralTicCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Calculate_DetectsTellingNotShowing()
    {
        var text = "She was angry. He was sad. They were happy. The king was wise.";

        var result = _detector.Calculate(text);

        result.TellingCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Calculate_CalculatesSlopPenalty()
    {
        var text = "We must delve into the depths. It is crucial to leverage resources. " +
                   "The character was able to win. It was quite dramatic. She was angry.";

        var result = _detector.Calculate(text);

        result.SlopPenalty.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Calculate_ReturnsZeroPenaltyForCleanText()
    {
        var text = "Rain lashed the cobblestones. She pulled her cloak tighter, the wool scratchy against her cheek.";

        var result = _detector.Calculate(text);

        result.SlopPenalty.Should().Be(0);
    }
}
