using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
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
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<FoundationEvaluationResult> EvaluateFoundationAsync(string voice, string world, string characters, string outline, string canon, CancellationToken ct = default)
    {
        var promptText = EvaluationPrompts.BuildFoundationEvaluationPrompt(voice, world, characters, outline, canon);
        var response = await _client.GenerateAsync(new GenerationRequest(
            SystemPrompt: EvaluationPrompts.JudgeSystemPrompt,
            UserPrompt: promptText,
            Temperature: 0.3f), ct);

        return ParseFoundationResult(response);
    }

    public async Task<ChapterEvaluationResult> EvaluateChapterAsync(string chapterText, int chapterNum, string voice, string world, string characters, string canon, string chapterOutline, string prevChapterTail, CancellationToken ct = default)
    {
        var slopScore = _slopDetector.Calculate(chapterText);

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

        var response = await _client.GenerateAsync(new GenerationRequest(
            SystemPrompt: EvaluationPrompts.JudgeSystemPrompt,
            UserPrompt: prompt,
            Temperature: 0.3f), ct);

        return ParseFullNovelResult(response);
    }

    // Helper: Extract raw JSON from LLM response (remove markdown fences, preamble)
    private string ExtractJson(string text)
    {
        // Remove markdown fences
        text = Regex.Replace(text, @"^\s*```json\s*\n", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\n\s*```$", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"^\s*```\s*\n", "", RegexOptions.IgnoreCase);

        // Find first { and matching }
        var start = text.IndexOf('{');
        if (start == -1) return "{}";

        // Find matching closing brace by tracking depth
        int depth = 0;
        bool inString = false;
        bool escape = false;

        for (int i = start; i < text.Length; i++)
        {
            char c = text[i];
            if (escape)
            {
                escape = false;
                continue;
            }
            if (c == '\\' && inString)
            {
                escape = true;
                continue;
            }
            if (c == '"' && !escape)
            {
                inString = !inString;
                continue;
            }
            if (inString) continue;

            if (c == '{') depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0) return text.Substring(start, i - start + 1);
            }
        }

        // Fallback: return from first { to end
        return text.Substring(start);
    }

    private FoundationEvaluationResult ParseFoundationResult(string response)
    {
        try
        {
            var json = ExtractJson(response);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var dimensionScores = new Dictionary<string, DimensionScore>();
            var dimensionFields = new[] { "magic_system", "world_history", "geography_and_culture", "lore_interconnection",
                "iceberg_depth", "character_depth", "character_distinctiveness", "character_secrets",
                "outline_completeness", "foreshadowing_balance", "internal_consistency", "voice_clarity", "canon_coverage" };

            foreach (var field in dimensionFields)
            {
                if (root.TryGetProperty(field, out var dimElement))
                {
                    var score = dimElement.TryGetProperty("score", out var s) ? s.GetInt32() : 0;
                    var gap = dimElement.TryGetProperty("gap", out var g) ? g.GetString() ?? "" : "";
                    var fix = dimElement.TryGetProperty("fix", out var f) ? f.GetString() ?? "" : "";
                    var note = dimElement.TryGetProperty("note", out var n) ? n.GetString() ?? "" : "";
                    dimensionScores[field] = new DimensionScore(score, gap, fix, note);
                }
            }

            var slopList = new List<string>();
            if (root.TryGetProperty("slop_in_planning_docs", out var slopObj) && slopObj.TryGetProperty("found", out var found))
            {
                foreach (var item in found.EnumerateArray()) slopList.Add(item.GetString() ?? "");
            }

            var contradictions = new List<string>();
            if (root.TryGetProperty("contradictions_found", out var contr))
            {
                foreach (var item in contr.EnumerateArray()) contradictions.Add(item.GetString() ?? "");
            }

            var top3 = new List<string>();
            if (root.TryGetProperty("top_3_improvements", out var imp))
            {
                foreach (var item in imp.EnumerateArray()) top3.Add(item.GetString() ?? "");
            }

            return new FoundationEvaluationResult(
                DimensionScores: dimensionScores,
                SlopInPlanningDocs: slopList,
                ContradictionsFound: contradictions,
                OverallScore: root.TryGetProperty("overall_score", out var os) ? os.GetDouble() : 0.0,
                LoreScore: root.TryGetProperty("lore_score", out var ls) ? ls.GetDouble() : 0.0,
                WeakestDimension: root.TryGetProperty("weakest_dimension", out var wd) ? wd.GetString() ?? "" : "",
                Top3Improvements: top3,
                IdentifiedGaps: dimensionScores.Values.Select(d => d.Gap).Where(g => !string.IsNullOrEmpty(g)).ToList()
            );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to parse foundation result: {ex.Message}");
            return new FoundationEvaluationResult(
                DimensionScores: new Dictionary<string, DimensionScore>(),
                SlopInPlanningDocs: new List<string>(),
                ContradictionsFound: new List<string>(),
                OverallScore: 0.0,
                LoreScore: 0.0,
                WeakestDimension: "parse_error",
                Top3Improvements: new List<string>(),
                IdentifiedGaps: new List<string> { $"JSON parse error: {ex.Message}" }
            );
        }
    }

    private ChapterEvaluationResult ParseChapterResult(string response)
    {
        try
        {
            var json = ExtractJson(response);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var dimensionScores = new Dictionary<string, DimensionScore>();
            var dimensionFields = new[] { "voice_adherence", "beat_coverage", "character_voice", "plants_seeded",
                "prose_quality", "continuity", "canon_compliance", "lore_integration", "engagement" };

            foreach (var field in dimensionFields)
            {
                if (root.TryGetProperty(field, out var dimElement))
                {
                    var score = dimElement.TryGetProperty("score", out var s) ? s.GetInt32() : 0;
                    // Handle different key names for weakest moment/sentence
                    string weakest = "";
                    if (dimElement.TryGetProperty("weakest_moment", out var wm)) weakest = wm.GetString() ?? "";
                    else if (dimElement.TryGetProperty("weakest_sentence", out var ws)) weakest = ws.GetString() ?? "";

                    string fix = "";
                    if (dimElement.TryGetProperty("fix", out var f)) fix = f.GetString() ?? "";
                    else if (dimElement.TryGetProperty("rewrite_suggestion", out var rs)) fix = rs.GetString() ?? "";

                    var note = dimElement.TryGetProperty("note", out var n) ? n.GetString() ?? "" : "";
                    dimensionScores[field] = new DimensionScore(score, weakest, fix, note);
                }
            }

            var threeWeakest = new List<string>();
            if (root.TryGetProperty("three_weakest_sentences", out var tw))
            {
                foreach (var item in tw.EnumerateArray()) threeWeakest.Add(item.GetString() ?? "");
            }

            var threeStrongest = new List<string>();
            if (root.TryGetProperty("three_strongest_sentences", out var ts))
            {
                foreach (var item in ts.EnumerateArray()) threeStrongest.Add(item.GetString() ?? "");
            }

            var aiPatterns = new List<string>();
            if (root.TryGetProperty("ai_patterns_detected", out var ap))
            {
                foreach (var item in ap.EnumerateArray()) aiPatterns.Add(item.GetString() ?? "");
            }

            var top3Revisions = new List<string>();
            if (root.TryGetProperty("top_3_revisions", out var tr))
            {
                foreach (var item in tr.EnumerateArray()) top3Revisions.Add(item.GetString() ?? "");
            }

            var newCanon = new List<string>();
            if (root.TryGetProperty("new_canon_entries", out var nc))
            {
                foreach (var item in nc.EnumerateArray()) newCanon.Add(item.GetString() ?? "");
            }

            var violations = new List<string>();
            if (root.TryGetProperty("canon_compliance", out var cc) && cc.TryGetProperty("violations", out var viol))
            {
                foreach (var item in viol.EnumerateArray()) violations.Add(item.GetString() ?? "");
            }

            return new ChapterEvaluationResult(
                DimensionScores: dimensionScores,
                ThreeWeakestSentences: threeWeakest,
                ThreeStrongestSentences: threeStrongest,
                AIPatternsDetected: aiPatterns,
                OverallScore: root.TryGetProperty("overall_score", out var os) ? os.GetDouble() : 0.0,
                RawJudgeScore: 0.0, // Set by caller after slop penalty
                WeakestDimension: root.TryGetProperty("weakest_dimension", out var wd) ? wd.GetString() ?? "" : "",
                Top3Revisions: top3Revisions,
                NewCanonEntries: newCanon
            );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to parse chapter result: {ex.Message}");
            return new ChapterEvaluationResult(
                DimensionScores: new Dictionary<string, DimensionScore>(),
                ThreeWeakestSentences: new List<string>(),
                ThreeStrongestSentences: new List<string>(),
                AIPatternsDetected: new List<string>(),
                OverallScore: 0.0,
                RawJudgeScore: 0.0,
                WeakestDimension: "parse_error",
                Top3Revisions: new List<string>(),
                NewCanonEntries: new List<string>()
            );
        }
    }

    private FullNovelEvaluationResult ParseFullNovelResult(string response)
    {
        try
        {
            var json = ExtractJson(response);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var dimensionScores = new Dictionary<string, DimensionScore>();
            var dimensionFields = new[] { "arc_completion", "pacing_curve", "theme_coherence", "foreshadowing_resolution",
                "world_consistency", "voice_consistency", "overall_engagement" };

            foreach (var field in dimensionFields)
            {
                if (root.TryGetProperty(field, out var dimElement))
                {
                    var score = dimElement.TryGetProperty("score", out var s) ? s.GetInt32() : 0;
                    var note = dimElement.TryGetProperty("note", out var n) ? n.GetString() ?? "" : "";
                    dimensionScores[field] = new DimensionScore(score, "", "", note);
                }
            }

            return new FullNovelEvaluationResult(
                DimensionScores: dimensionScores,
                NovelScore: root.TryGetProperty("novel_score", out var ns) ? ns.GetDouble() : 0.0,
                WeakestDimension: root.TryGetProperty("weakest_dimension", out var wd) ? wd.GetString() ?? "" : "",
                WeakestChapter: root.TryGetProperty("weakest_chapter", out var wc) ? wc.GetInt32() : 0,
                TopSuggestion: root.TryGetProperty("top_suggestion", out var ts) ? ts.GetString() ?? "" : ""
            );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to parse full novel result: {ex.Message}");
            return new FullNovelEvaluationResult(
                DimensionScores: new Dictionary<string, DimensionScore>(),
                NovelScore: 0.0,
                WeakestDimension: "parse_error",
                WeakestChapter: 0,
                TopSuggestion: $"JSON parse error: {ex.Message}"
            );
        }
    }
}
