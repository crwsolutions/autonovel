using System.Linq;
using System.Text.Json;
using Autonovel.Core.Domain;
using Autonovel.Core.Prompts;

namespace Autonovel.Core.Services;

public interface IEvaluator
{
    Task<FoundationEvaluationResult> EvaluateFoundationAsync(string voice, string world, string characters, string outline, string canon, CancellationToken ct = default);
    Task<ChapterEvaluationResult> EvaluateChapterAsync(string chapterText, int chapterNum, string voice, string world, string characters, string canon, string chapterOutline, string prevChapterTail, CancellationToken ct = default);
    Task<FullNovelEvaluationResult> EvaluateFullNovelAsync(string voice, string world, string characters, string outline, Dictionary<int, string> chapterSummaries, CancellationToken ct = default);
}

public class Evaluator : IEvaluator
{
    private readonly IGenerationClient _client;
    private readonly IMechanicalSlopDetector _slopDetector;
    private readonly JsonSerializerOptions _jsonOptions;

    public Evaluator(IGenerationClient client, IMechanicalSlopDetector slopDetector)
    {
        _client = client;
        _slopDetector = slopDetector;
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    public async Task<FoundationEvaluationResult> EvaluateFoundationAsync(string voice, string world, string characters, string outline, string canon, CancellationToken ct = default)
    {
        // Build prompt (simplified - full prompt would be from Python)
        var prompt = $@"""Evaluate these fantasy novel planning documents.

SCORING CALIBRATION:
9-10: Could not improve this with a month of focused editorial work.
7-8: Strong. A skilled author could draft from this document with minimal invention.
5-6: Functional but thin. A writer would need to invent significant material.
3-4: Sketchy. More questions than answers.
1-2: Placeholder or stub.

VOICE DEFINITION:
{voice}

WORLD BIBLE:
{world}

CHARACTER REGISTRY:
{characters}

OUTLINE:
{outline}

CANON:
{canon}

Score these dimensions 0-10 and return JSON:
- magic_system
- world_history  
- geography_and_culture
- lore_interconnection
- iceberg_depth
- character_depth
- character_distinctiveness
- character_secrets
- outline_completeness
- foreshadowing_balance
- internal_consistency
- voice_clarity
- canon_coverage

Respond with JSON including: overall_score, lore_score, weakest_dimension, top_3_improvements.""";

        var promptText = EvaluationPrompts.BuildFoundationEvaluationPrompt(voice, world, characters, outline, canon);
        var response = await _client.GenerateAsync(new GenerationRequest(
            SystemPrompt: EvaluationPrompts.JudgeSystemPrompt,
            UserPrompt: promptText,
            Temperature: 0.3f), ct);

        // Parse JSON (simplified)
        return ParseFoundationResult(response);
    }

    public async Task<ChapterEvaluationResult> EvaluateChapterAsync(string chapterText, int chapterNum, string voice, string world, string characters, string canon, string chapterOutline, string prevChapterTail, CancellationToken ct = default)
    {
        // Mechanical slop detection
        var slopScore = _slopDetector.Calculate(chapterText);

        // LLM evaluation prompt
        var prompt = $@"""Evaluate this fantasy novel chapter.

VOICE DEFINITION:
{voice}

WORLD BIBLE:
{world}

CHARACTER REGISTRY:
{characters}

CANON:
{canon}

CHAPTER OUTLINE:
{chapterOutline}

PREVIOUS CHAPTER:
{prevChapterTail}

THE CHAPTER:
{chapterText}

Score: voice_adherence, beat_coverage, character_voice, plants_seeded, prose_quality, continuity, canon_compliance, lore_integration, engagement (0-10 each).

Respond with JSON including: overall_score, weakest_dimension, top_3_revisions, new_canon_entries.""";

        var promptText = EvaluationPrompts.BuildChapterEvaluationPrompt(voice, world, characters, canon, chapterOutline, prevChapterTail, chapterText);
        var response = await _client.GenerateAsync(new GenerationRequest(
            SystemPrompt: EvaluationPrompts.JudgeSystemPrompt,
            UserPrompt: promptText,
            Temperature: 0.3f), ct);

        var rawScore = ParseChapterResult(response);
        
        // Apply slop penalty
        var adjustedScore = Math.Max(0, rawScore.OverallScore - slopScore.SlopPenalty);
        
        return rawScore with 
        { 
            OverallScore = Math.Round(adjustedScore, 2),
            RawJudgeScore = rawScore.OverallScore
        };
    }

    public async Task<FullNovelEvaluationResult> EvaluateFullNovelAsync(string voice, string world, string characters, string outline, Dictionary<int, string> chapterSummaries, CancellationToken ct = default)
    {
        var summaries = string.Join("\n", chapterSummaries.OrderBy(c => c.Key).Select(c => 
        {
            var summary = c.Value.Length > 500 ? c.Value.Substring(0, 500) + "..." : c.Value;
            return $"Chapter {c.Key}: {summary}";
        }));

        var prompt = $@"""Evaluate this complete fantasy novel.

VOICE: {voice}
WORLD: {world}
CHARACTERS: {characters}
OUTLINE: {outline}

CHAPTER SUMMARIES:
{summaries}

Score: arc_completion, pacing_curve, theme_coherence, foreshadowing_resolution, world_consistency, voice_consistency, overall_engagement (0-10 each).

Respond with JSON including: novel_score, weakest_dimension, weakest_chapter, top_suggestion.""";

        // TODO: Build full novel evaluation prompt
        var response = await _client.GenerateAsync(new GenerationRequest(
            SystemPrompt: EvaluationPrompts.JudgeSystemPrompt,
            UserPrompt: prompt,
            Temperature: 0.3f), ct);

        return ParseFullNovelResult(response);
    }

    // Simplified JSON parsing - in production would use proper deserialization
    private FoundationEvaluationResult ParseFoundationResult(string json)
    {
        // Placeholder - would parse actual JSON response
        return new FoundationEvaluationResult(
            DimensionScores: new Dictionary<string, DimensionScore>(),
            SlopInPlanningDocs: new List<string>(),
            ContradictionsFound: new List<string>(),
            OverallScore: 0.0,
            LoreScore: 0.0,
            WeakestDimension: "",
            Top3Improvements: new List<string>(),
            IdentifiedGaps: new List<string>()
        );
    }

    private ChapterEvaluationResult ParseChapterResult(string json)
    {
        return new ChapterEvaluationResult(
            DimensionScores: new Dictionary<string, DimensionScore>(),
            ThreeWeakestSentences: new List<string>(),
            ThreeStrongestSentences: new List<string>(),
            AIPatternsDetected: new List<string>(),
            OverallScore: 0.0,
            RawJudgeScore: 0.0,
            WeakestDimension: "",
            Top3Revisions: new List<string>(),
            NewCanonEntries: new List<string>()
        );
    }

    private FullNovelEvaluationResult ParseFullNovelResult(string json)
    {
        return new FullNovelEvaluationResult(
            DimensionScores: new Dictionary<string, DimensionScore>(),
            NovelScore: 0.0,
            WeakestDimension: "",
            WeakestChapter: 0,
            TopSuggestion: ""
        );
    }
}