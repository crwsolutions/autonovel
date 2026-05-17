using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Autonovel.Core.Domain;
using Autonovel.Core.Prompts;

namespace Autonovel.Core.Services
{
    public interface IRevisionEngine
    {
        Task<RevisionResult> RunRevisionCycleAsync(int cycle, int maxCycles, CancellationToken ct = default);
    }

    public class RevisionEngine : IRevisionEngine
    {
        private readonly IGenerationClient _client;
        private readonly IEvaluator _evaluator;
        private readonly IFileManager _fileManager;
        private readonly IVersionControl _vc;
        private readonly JsonSerializerOptions _jsonOptions;

        public RevisionEngine(
            IGenerationClient client,
            IEvaluator evaluator,
            IFileManager fileManager,
            IVersionControl vc)
        {
            _client = client;
            _evaluator = evaluator;
            _fileManager = fileManager;
            _vc = vc;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public async Task<RevisionResult> RunRevisionCycleAsync(int cycle, int maxCycles, CancellationToken ct = default)
        {
            var result = new RevisionResult(Cycle: cycle);
            var revisionsApplied = new List<ChapterRevision>();

            // Step 1: Run adversarial edit on all chapters to identify cuts
            Console.WriteLine($"\nPhase {cycle}: Adversarial Edit Pass");
            var editResults = await RunAdversarialEditAsync(ct);

            // Step 2: Run reader panel on full novel
            Console.WriteLine($"\nPhase {cycle}: Reader Panel Evaluation");
            var readerPanelResult = await RunReaderPanelAsync(ct);

            // Step 3: Find consensus issues
            var consensusIssues = FindConsensusIssues(editResults, readerPanelResult);

            // Step 4: Apply targeted revisions
            foreach (var issue in consensusIssues.Take(5)) // Limit to 5 major issues per cycle
            {
                var revisionSuccess = await ApplyRevisionAsync(issue, cycle, ct);
                if (revisionSuccess)
                {
                    revisionsApplied.Add(new ChapterRevision(
                        Chapter: issue.Chapter,
                        Issue: issue.Issue,
                        PreScore: 0.0, // Would need to track pre-score
                        PostScore: 0.0,
                        Success: true
                    ));
                }
            }

            // Step 5: Evaluate full novel
            var novelScore = await EvaluateFullNovelAsync(ct);

            return result with
            {
                RevisionsApplied = revisionsApplied,
                NovelScore = novelScore
            };
        }

        private async Task<Dictionary<int, AdversarialEditResult>> RunAdversarialEditAsync(CancellationToken ct)
        {
            var results = new Dictionary<int, AdversarialEditResult>();
            var chapters = await _fileManager.ListChaptersAsync();

            foreach (var chNum in chapters)
            {
                try
                {
                    var chapterText = await _fileManager.ReadFileAsync($"chapters/ch_{chNum:02d}.md");
                    var wordCount = chapterText.Split(' ', '\n', '\r').Count(w => !string.IsNullOrWhiteSpace(w));

                    var prompt = EvaluationPrompts.BuildAdversarialEditPrompt(chapterText, wordCount);
                    var response = await _client.GenerateAsync(new GenerationRequest(
                        SystemPrompt: EvaluationPrompts.AdversarialEditSystemPrompt,
                        UserPrompt: prompt,
                        Temperature: 0.3f), ct);

                    var editResult = ParseAdversarialEditResult(response);
                    results[chNum] = editResult;

                    // Save edit log
                    var logPath = $"edit_logs/ch{chNum:02d}_cycle_cuts.json";
                    var json = JsonSerializer.Serialize(editResult, _jsonOptions);
                    await _fileManager.WriteFileAsync(logPath, json);

                    Console.WriteLine($"  Ch {chNum}: {editResult.Cuts.Count} cuts identified ({editResult.OverallFatPercentage:F1}% fat)");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"  Error editing chapter {chNum}: {ex.Message}");
                }
            }

            return results;
        }

        private async Task<ReaderPanelResult> RunReaderPanelAsync(CancellationToken ct)
        {
            // Build arc summary from chapters
            var chapters = await _fileManager.ListChaptersAsync();
            var summaries = new StringBuilder();

            foreach (var chNum in chapters.OrderBy(c => c).Take(24))
            {
                var chapterText = await _fileManager.ReadFileAsync($"chapters/ch_{chNum:02d}.md");
                var summary = BuildChapterSummary(chapterText);
                summaries.AppendLine($"Chapter {chNum}: {summary}");
            }

            var arcSummary = summaries.ToString();
            var readerPrompt = EvaluationPrompts.BuildReaderPanelPrompt(arcSummary);
            var readers = EvaluationPrompts.GetReaders();
            var readerResponses = new Dictionary<string, ReaderResponse>();

            foreach (var reader in readers)
            {
                try
                {
                    var response = await _client.GenerateAsync(new GenerationRequest(
                        SystemPrompt: reader.Value.System,
                        UserPrompt: readerPrompt,
                        Temperature: 0.5f), ct);

                    var readerResult = ParseReaderResponse(response);
                    readerResponses[reader.Key] = readerResult;

                    Console.WriteLine($"  {reader.Value.Name}: {readerResult.WouldRecommend[..50]}...");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"  Error with reader {reader.Key}: {ex.Message}");
                }
            }

            // Find disagreements
            var disagreements = FindDisagreements(readerResponses);

            // Save results
            var panelResult = new ReaderPanelResult(
                ReaderResponses: readerResponses,
                Disagreements: disagreements
            );

            var json = JsonSerializer.Serialize(panelResult, _jsonOptions);
            await _fileManager.WriteFileAsync("edit_logs/reader_panel_cycle.json", json);

            return panelResult;
        }

        private List<ConsensusItem> FindConsensusIssues(
            Dictionary<int, AdversarialEditResult> editResults,
            ReaderPanelResult readerPanel)
        {
            var issues = new List<ConsensusItem>();

            // Find chapters flagged by multiple readers
            var chapterFlags = new Dictionary<int, List<string>>();

            foreach (var reader in readerPanel.ReaderResponses)
            {
                // Extract chapter numbers from momentum_loss and worst_scene
                var chapters = ExtractChapterNumbers(reader.Value.MomentumLoss);
                foreach (var ch in chapters)
                {
                    if (!chapterFlags.ContainsKey(ch)) chapterFlags[ch] = new List<string>();
                    chapterFlags[ch].Add(reader.Key);
                }

                chapters = ExtractChapterNumbers(reader.Value.WorstScene);
                foreach (var ch in chapters)
                {
                    if (!chapterFlags.ContainsKey(ch)) chapterFlags[ch] = new List<string>();
                    chapterFlags[ch].Add(reader.Key);
                }
            }

            // Consensus = flagged by 2+ readers
            foreach (var flag in chapterFlags.Where(f => f.Value.Count >= 2))
            {
                issues.Add(new ConsensusItem(
                    Chapter: flag.Key,
                    Issue: "Momentum loss or weak scene",
                    FlaggedBy: flag.Value
                ));
            }

            // Add chapters with high fat percentage from adversarial edit
            foreach (var edit in editResults.Where(e => e.Value.OverallFatPercentage > 15.0))
            {
                issues.Add(new ConsensusItem(
                    Chapter: edit.Key,
                    Issue: $"High fat content ({edit.Value.OverallFatPercentage:F1}%)",
                    FlaggedBy: new List<string> { "adversarial_editor" }
                ));
            }

            return issues.OrderByDescending(i => i.FlaggedBy.Count).ToList();
        }

        private async Task<bool> ApplyRevisionAsync(ConsensusItem issue, int cycle, CancellationToken ct)
        {
            try
            {
                Console.WriteLine($"\nRevising Chapter {issue.Chapter}: {issue.Issue}");

                var chapterPath = $"chapters/ch_{issue.Chapter:02d}.md";
                var currentText = await _fileManager.ReadFileAsync(chapterPath);

                // Get adversarial edit results for this chapter
                var editPrompt = $@"""You are revising Chapter {issue.Chapter} of a fantasy novel.

CURRENT CHAPTER:
{currentText}

ISSUE TO FIX: {issue.Issue}
FLAGGED BY: {string.Join(", ", issue.FlaggedBy)}

YOUR TASK:
1. Tighten the prose - remove fat, redundancy, and over-explanation
2. Fix the specific issue identified
3. Maintain voice and continuity
4. Keep word count between 2500-3500 words

Rewrite the chapter now. Output only the revised chapter text, no commentary.""";

                var revision = await _client.GenerateAsync(new GenerationRequest(
                    SystemPrompt: EvaluationPrompts.AdversarialEditSystemPrompt,
                    UserPrompt: editPrompt,
                    Temperature: 0.7f), ct);

                await _fileManager.WriteFileAsync(chapterPath, revision);
                var commitMsg = $"Revision cycle {cycle}: Fix {issue.Issue} in Ch {issue.Chapter}";
                var confirmed = await _vc.ConfirmCommitAsync(commitMsg);
                if (!confirmed)
                {
                    Console.Error.WriteLine($"  User cancelled commit for chapter {issue.Chapter}");
                    return false;
                }
                await _vc.CommitAsync(new[] { $"workspace/{chapterPath}" }, commitMsg);

                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  Revision failed: {ex.Message}");
                return false;
            }
        }

        private async Task<double> EvaluateFullNovelAsync(CancellationToken ct)
        {
            // Simplified: just evaluate a sample chapter for now
            // Full implementation would aggregate all chapters
            try
            {
                var voice = await _fileManager.ReadFileAsync("voice.md");
                var world = await _fileManager.ReadFileAsync("world.md");
                var characters = await _fileManager.ReadFileAsync("characters.md");
                var canon = await _fileManager.ReadFileAsync("canon.md");
                var outline = await _fileManager.ReadFileAsync("outline.md");

                // Evaluate chapter 1 as proxy
                var ch1 = await _fileManager.ReadFileAsync("chapters/ch_01.md");
                var ch1Outline = ExtractChapterOutline(outline, 1);

                var eval = await _evaluator.EvaluateChapterAsync(
                    ch1, 1, voice, world, characters, canon, ch1Outline, "", ct);

                return eval.OverallScore;
            }
            catch
            {
                return 0.0;
            }
        }

        // Helper methods
        private AdversarialEditResult ParseAdversarialEditResult(string response)
        {
            try
            {
                var json = ExtractJson(response);
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var cuts = new List<EditCut>();
                if (root.TryGetProperty("cuts", out var cutsProp))
                {
                    foreach (var cut in cutsProp.EnumerateArray())
                    {
                        cuts.Add(new EditCut(
                            Quote: cut.TryGetProperty("quote", out var q) ? q.GetString() ?? "" : "",
                            Type: cut.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "",
                            Reason: cut.TryGetProperty("reason", out var r) ? r.GetString() ?? "" : "",
                            Action: cut.TryGetProperty("action", out var a) ? a.GetString() ?? "" : "",
                            Rewrite: cut.TryGetProperty("rewrite", out var rw) ? rw.GetString() : null
                        ));
                    }
                }

                return new AdversarialEditResult(
                    Cuts: cuts,
                    TotalCuttableWords: root.TryGetProperty("total_cuttable_words", out var tcw) ? tcw.GetInt32() : 0,
                    TightestPassage: root.TryGetProperty("tightest_passage", out var tp) ? tp.GetString() ?? "" : "",
                    LoosestPassage: root.TryGetProperty("loosest_passage", out var lp) ? lp.GetString() ?? "" : "",
                    OverallFatPercentage: root.TryGetProperty("overall_fat_percentage", out var f) ? f.GetDouble() : 0.0,
                    OneSentenceVerdict: root.TryGetProperty("one_sentence_verdict", out var v) ? v.GetString() ?? "" : ""
                );
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Parse error: {ex.Message}");
                return new AdversarialEditResult();
            }
        }

        private ReaderResponse ParseReaderResponse(string response)
        {
            try
            {
                var json = ExtractJson(response);
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                return new ReaderResponse(
                    MomentumLoss: root.TryGetProperty("momentum_loss", out var ml) ? ml.GetString() ?? "" : "",
                    EarnedEnding: root.TryGetProperty("earned_ending", out var ee) ? ee.GetString() ?? "" : "",
                    CutCandidate: root.TryGetProperty("cut_candidate", out var cc) ? cc.GetString() ?? "" : "",
                    MissingScene: root.TryGetProperty("missing_scene", out var ms) ? ms.GetString() ?? "" : "",
                    ThinnestCharacter: root.TryGetProperty("thinnest_character", out var tc) ? tc.GetString() ?? "" : "",
                    BestScene: root.TryGetProperty("best_scene", out var bs) ? bs.GetString() ?? "" : "",
                    WorstScene: root.TryGetProperty("worst_scene", out var ws) ? ws.GetString() ?? "" : "",
                    WouldRecommend: root.TryGetProperty("would_recommend", out var wr) ? wr.GetString() ?? "" : "",
                    HauntsYou: root.TryGetProperty("haunts_you", out var hy) ? hy.GetString() ?? "" : "",
                    NextBook: root.TryGetProperty("next_book", out var nb) ? nb.GetString() ?? "" : ""
                );
            }
            catch
            {
                return new ReaderResponse();
            }
        }

        private List<ReaderDisagreement> FindDisagreements(Dictionary<string, ReaderResponse> responses)
        {
            var disagreements = new List<ReaderDisagreement>();
            var questions = new[] { "momentum_loss", "cut_candidate", "thinnest_character", "worst_scene" };

            // Simplified disagreement detection
            // In full implementation, would extract chapter mentions and find conflicts

            return disagreements;
        }

        private string ExtractJson(string text)
        {
            // Remove markdown fences
            text = Regex.Replace(text, @"^\s*```json\s*\n", "", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\n\s*```$", "", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"^\s*```\s*\n", "", RegexOptions.IgnoreCase);

            // Find first { and matching }
            var start = text.IndexOf('{');
            if (start == -1) return "{}";

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

            return text.Substring(start);
        }

        private string BuildChapterSummary(string chapterText)
        {
            // Take first and last 300 words as proxy for summary
            var words = chapterText.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length <= 600) return chapterText;

            var first = string.Join(" ", words.Take(300));
            var last = string.Join(" ", words.TakeLast(300));
            return $"{first}...{last}";
        }

        private List<int> ExtractChapterNumbers(string text)
        {
            var matches = Regex.Matches(text, @"Ch(?:apter)?\s*(\d+)", RegexOptions.IgnoreCase);
            return matches.Select(m => int.Parse(m.Groups[1].Value)).ToList();
        }

        private string ExtractChapterOutline(string outline, int chapterNum)
        {
            var pattern = $@"###\s*Ch\s*{chapterNum}\b.*?(?=###\s*Ch\s*\d|##\s+|$)";
            var match = Regex.Match(outline, pattern, RegexOptions.Singleline);
            return match.Success ? match.Value : $"(Chapter {chapterNum} outline not found)";
        }
    }
}
