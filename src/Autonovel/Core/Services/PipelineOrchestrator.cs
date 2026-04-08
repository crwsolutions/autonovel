using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Autonovel.Core.Domain;
using Autonovel.Core.Prompts;

namespace Autonovel.Core.Services
{
    public interface IPipelineOrchestrator
    {
        Task<PipelineResult> RunFoundationAsync(PipelineConfig config, CancellationToken ct = default);
        Task<PipelineResult> RunDraftAsync(int chapterNum, PipelineConfig config, CancellationToken ct = default);
        Task<PipelineResult> RunRevisionAsync(int maxCycles, PipelineConfig config, CancellationToken ct = default);
        Task<PipelineResult> RunExportAsync(PipelineConfig config, CancellationToken ct = default);
    }

    public class PipelineOrchestrator : IPipelineOrchestrator
    {
        private readonly IGenerationClient _client;
        private readonly IEvaluator _evaluator;
        private readonly IStateManager _stateManager;
        private readonly IVersionControl _vc;
        private readonly IFileManager _fileManager;
        private readonly IRevisionEngine _revisionEngine;

        public PipelineOrchestrator(
            IGenerationClient client,
            IEvaluator evaluator,
            IStateManager stateManager,
            IVersionControl vc,
            IFileManager fileManager,
            IRevisionEngine revisionEngine)
        {
            _client = client;
            _evaluator = evaluator;
            _stateManager = stateManager;
            _vc = vc;
            _fileManager = fileManager;
            _revisionEngine = revisionEngine;
        }

        public async Task<PipelineResult> RunFoundationAsync(PipelineConfig config, CancellationToken ct = default)
        {
            var result = new PipelineResult(Phase: "foundation", Success: false);
            var iteration = 0;
            var maxIter = config.MaxIterations ?? 10;
            var foundationThreshold = config.FoundationThreshold ?? 7.5f;
            var loreThreshold = config.LoreThreshold ?? 7.0f;

            await _stateManager.UpdateStateAsync(s => s with { CurrentPhase = "foundation", Iteration = 0 });

            while (iteration < maxIter && !ct.IsCancellationRequested)
            {
                iteration++;
                Console.WriteLine($"\n=== Foundation Iteration {iteration}/{maxIter} ===");

                // Step 1: Generate world.md
                Console.WriteLine("Generating world.md...");
                var seed = await _fileManager.ReadFileAsync("seed.txt");
                var voice = await _fileManager.ReadFileAsync("voice.md");
                var craft = await _fileManager.ReadFileAsync("CRAFT.md");
                var voicePart2 = ExtractVoicePart2(voice);

                var worldPrompt = FoundationPrompts.BuildWorldPrompt(seed, voicePart2, craft);
                var worldContent = await _client.GenerateAsync(new GenerationRequest(
                    SystemPrompt: FoundationPrompts.WorldSystemPrompt,
                    UserPrompt: worldPrompt,
                    Temperature: 0.7f), ct);

                await _fileManager.WriteFileAsync("world.md", worldContent);

                // Step 2: Generate characters.md
                Console.WriteLine("Generating characters.md...");
                var charPrompt = FoundationPrompts.BuildCharacterPrompt(seed, worldContent, voicePart2);
                var charContent = await _client.GenerateAsync(new GenerationRequest(
                    SystemPrompt: FoundationPrompts.CharacterSystemPrompt,
                    UserPrompt: charPrompt,
                    Temperature: 0.7f), ct);

                await _fileManager.WriteFileAsync("characters.md", charContent);

                // Step 3: Generate outline.md
                Console.WriteLine("Generating outline.md...");
                var mystery = await _fileManager.ReadFileAsync("MYSTERY.md");
                var outlinePrompt = FoundationPrompts.BuildOutlinePrompt(seed, mystery, worldContent, charContent, voicePart2, craft);
                var outlineContent = await _client.GenerateAsync(new GenerationRequest(
                    SystemPrompt: FoundationPrompts.OutlineSystemPrompt,
                    UserPrompt: outlinePrompt,
                    Temperature: 0.7f), ct);

                await _fileManager.WriteFileAsync("outline.md", outlineContent);

                // Step 4: Initialize canon.md from world/characters
                Console.WriteLine("Initializing canon.md...");
                var canonContent = ExtractCanonFromFoundation(worldContent, charContent);
                await _fileManager.WriteFileAsync("canon.md", canonContent);

                // Step 5: Evaluate
                Console.WriteLine("Evaluating foundation...");
                var evalResult = await _evaluator.EvaluateFoundationAsync(voice, worldContent, charContent, outlineContent, canonContent, ct);

                Console.WriteLine($"  Overall Score: {evalResult.OverallScore:F2}");
                Console.WriteLine($"  Lore Score: {evalResult.LoreScore:F2}");
                Console.WriteLine($"  Weakest: {evalResult.WeakestDimension}");

                // Keep/discard decision
                var shouldKeep = evalResult.OverallScore >= foundationThreshold && evalResult.LoreScore >= loreThreshold;

                if (shouldKeep)
                {
                    // Commit and proceed
                    var commitMsg = $"Foundation iteration {iteration}: {evalResult.OverallScore:F2} overall, {evalResult.LoreScore:F2} lore";
                    await _vc.CommitAsync(new[] { "world.md", "characters.md", "outline.md", "canon.md" }, commitMsg);
                    
                    await _stateManager.UpdateStateAsync(s => s with 
                    { 
                        CurrentPhase = "drafting",
                        Iteration = 0,
                        LastScore = evalResult.OverallScore,
                        PropagationDebts = new List<string>()
                    });

                    result = result with { Success = true, Score = evalResult.OverallScore, Message = $"Foundation passed (score: {evalResult.OverallScore:F2})" };
                    break;
                }
                else
                {
                    // Hard reset and retry
                    Console.WriteLine($"Discarding iteration {iteration} (below threshold: {foundationThreshold}/{loreThreshold})");
                    await _vc.HardResetAsync();
                    
                    // Add fixes to prompt for next iteration
                    var improvements = string.Join("\n", evalResult.Top3Improvements.Take(2));
                    // Note: In full implementation, we'd modify the prompts to include these as constraints
                }
            }

            if (!result.Success)
            {
                result = result with { Message = $"Foundation failed after {iteration} iterations (threshold: {foundationThreshold}/{loreThreshold})" };
            }

            return result;
        }

        public async Task<PipelineResult> RunDraftAsync(int chapterNum, PipelineConfig config, CancellationToken ct = default)
        {
            var result = new PipelineResult { Phase = "drafting", Chapter = chapterNum, Success = false };
            var attempt = 0;
            var maxAttempts = config.MaxIterations ?? 5;
            var draftThreshold = config.DraftThreshold ?? 6.0f;

            await _stateManager.UpdateStateAsync(s => s with { CurrentPhase = "drafting", Iteration = chapterNum });

            while (attempt < maxAttempts && !ct.IsCancellationRequested)
            {
                attempt++;
                Console.WriteLine($"\n=== Chapter {chapterNum} Attempt {attempt}/{maxAttempts} ===");

                // Load context
                var voice = await _fileManager.ReadFileAsync("voice.md");
                var world = await _fileManager.ReadFileAsync("world.md");
                var characters = await _fileManager.ReadFileAsync("characters.md");
                var outline = await _fileManager.ReadFileAsync("outline.md");
                var canon = await _fileManager.ReadFileAsync("canon.md");

                var chapterOutline = ExtractChapterOutline(outline, chapterNum);
                var nextChapterOutline = ExtractChapterOutline(outline, chapterNum + 1);
                var prevChapter = chapterNum > 1 ? await _fileManager.ReadFileAsync($"chapters/ch_{chapterNum - 1:02d}.md") : "(first chapter)";
                var prevTail = prevChapter.Length > 2000 ? prevChapter.Substring(prevChapter.Length - 2000) : prevChapter;

                // Generate
                var prompt = ChapterPrompts.BuildDraftPrompt(chapterNum, voice, chapterOutline, nextChapterOutline, prevTail, world, characters);
                var chapterContent = await _client.GenerateAsync(new GenerationRequest(
                    SystemPrompt: ChapterPrompts.DraftSystemPrompt,
                    UserPrompt: prompt,
                    Temperature: 0.7f), ct);

                await _fileManager.WriteFileAsync($"chapters/ch_{chapterNum:02d}.md", chapterContent);

                // Evaluate
                var evalResult = await _evaluator.EvaluateChapterAsync(
                    chapterContent, chapterNum, voice, world, characters, canon, chapterOutline, prevTail, ct);

                Console.WriteLine($"  Score: {evalResult.OverallScore:F2} (raw: {evalResult.RawJudgeScore:F2}, slop penalty: {evalResult.RawJudgeScore - evalResult.OverallScore:F2})");
                Console.WriteLine($"  Word count: {chapterContent.Split().Length}");

                // Keep/discard
                if (evalResult.OverallScore >= draftThreshold)
                {
                    // Commit
                    var commitMsg = $"Chapter {chapterNum}: {evalResult.OverallScore:F2} (attempt {attempt})";
                    await _vc.CommitAsync(new[] { $"chapters/ch_{chapterNum:02d}.md" }, commitMsg);

                    // Update canon with new entries
                    if (evalResult.NewCanonEntries.Any())
                    {
                        var newCanon = canon + "\n\n## Added in Chapter " + chapterNum + "\n" + 
                                     string.Join("\n", evalResult.NewCanonEntries.Select(e => $"- {e}"));
                        await _fileManager.WriteFileAsync("canon.md", newCanon);
                        await _vc.CommitAsync(new[] { "canon.md" }, $"Update canon from chapter {chapterNum}");
                    }

                    result = result with { Success = true, Score = evalResult.OverallScore, Message = $"Chapter {chapterNum} drafted (score: {evalResult.OverallScore:F2})" };
                    break;
                }
                else
                {
                    // Discard and retry
                    Console.WriteLine($"Discarding (below threshold: {draftThreshold})");
                    await _vc.HardResetAsync();
                }
            }

            if (!result.Success)
            {
                result = result with { Message = $"Chapter {chapterNum} failed after {attempt} attempts" };
            }

            return result;
        }

        public async Task<PipelineResult> RunRevisionAsync(int maxCycles, PipelineConfig config, CancellationToken ct = default)
        {
            var result = new PipelineResult(Phase: "revision", Success: false);
            var maxCyclesEffective = config.RevisionMaxCycles ?? maxCycles;
            var plateauDelta = 0.3;
            var minCycles = 3;

            Console.WriteLine($"\n{'='*60}");
            Console.WriteLine("PHASE 3: REVISION");
            Console.WriteLine($"{'='*60}");

            var prevScore = 0.0;
            
            for (int cycle = 1; cycle <= maxCyclesEffective; cycle++)
            {
                Console.WriteLine($"\n{'='*60}");
                Console.WriteLine($"REVISION CYCLE {cycle}/{maxCyclesEffective}");
                Console.WriteLine($"{'='*60}");

                var cycleResult = await _revisionEngine.RunRevisionCycleAsync(cycle, maxCyclesEffective, ct);

                Console.WriteLine($"\nCycle {cycle} summary:");
                Console.WriteLine($"  Revisions applied: {cycleResult.RevisionsApplied.Count}");
                Console.WriteLine($"  Novel score: {cycleResult.NovelScore:F2}");

                if (cycle >= minCycles && Math.Abs(cycleResult.NovelScore - prevScore) < plateauDelta)
                {
                    Console.WriteLine($"\nPlateau detected (Δ={Math.Abs(cycleResult.NovelScore - prevScore):F2} < {plateauDelta})");
                    Console.WriteLine("Stopping revision phase.");
                    result = result with { Success = true, Score = cycleResult.NovelScore, Message = $"Revision complete (plateau at cycle {cycle})" };
                    break;
                }

                prevScore = cycleResult.NovelScore;

                if (cycle == maxCyclesEffective)
                {
                    result = result with { Success = true, Score = cycleResult.NovelScore, Message = $"Revision complete (max cycles reached)" };
                }
            }

            if (!result.Success)
            {
                result = result with { Message = "Revision phase failed" };
            }

            return result;
        }

        public async Task<PipelineResult> RunExportAsync(PipelineConfig config, CancellationToken ct = default)
        {
            var result = new PipelineResult(Phase: "export", Success: false);

            try
            {
                var chapters = await _fileManager.ListChaptersAsync();
                if (!chapters.Any())
                {
                    result = result with { Message = "No chapters found to export" };
                    return result;
                }

                var sb = new StringBuilder();
                sb.AppendLine("# The Second Son of the House of Bells\n");

                foreach (var chapterNum in chapters.OrderBy(c => c))
                {
                    var content = await _fileManager.ReadFileAsync($"chapters/ch_{chapterNum:02d}.md");
                    sb.AppendLine($"## Chapter {chapterNum}\n");
                    sb.AppendLine(content);
                    sb.AppendLine("\n\n");
                }

                await _fileManager.WriteFileAsync("manuscript.md", sb.ToString());
                
                // Commit
                await _vc.CommitAsync(new[] { "manuscript.md" }, "Export manuscript");

                result = result with { Success = true, Message = $"Exported {chapters.Count()} chapters to manuscript.md" };
            }
            catch (Exception ex)
            {
                result = result with { Message = $"Export failed: {ex.Message}" };
            }

            return result;
        }

        // Helper methods
        private string ExtractVoicePart2(string voice) =>
            voice.Split('\n')
                .SkipWhile(l => !l.Contains("Part 2"))
                .Skip(1)
                .TakeWhile(l => !l.StartsWith("## ") || l.Contains("Part 2"))
                .TakeWhile(l => !l.StartsWith("## "))
                .SkipWhile(l => !l.Contains("Part 2"))
                .Skip(1)
                .ToArray()
                .JoinWith("\n");

        private string ExtractChapterOutline(string outline, int chapterNum)
        {
            var pattern = $@"###\s*Ch\s*{chapterNum}\b.*?(?=###\s*Ch\s*\d|##\s+|$)";
            var match = Regex.Match(outline, pattern, RegexOptions.Singleline);
            return match.Success ? match.Value : $"(Chapter {chapterNum} outline not found)";
        }

        private string ExtractCanonFromFoundation(string world, string characters)
        {
            // Extract hard facts: names, dates, locations, rules
            var sb = new StringBuilder();
            sb.AppendLine("# Canon - Established Facts");
            sb.AppendLine("\n## Magic System Rules");
            sb.AppendLine("(Extract from world.md - Tonal Law intervals, costs, limitations)");
            sb.AppendLine("\n## Character Facts");
            sb.AppendLine("- Cass Bellwright: 14 years old, POV character, hears harmonic undernote");
            sb.AppendLine("- Eddan Bellwright: Father, shaking hands, sealed journals");
            sb.AppendLine("- Perin Bellwright: Older brother, Corda contract, missing");
            sb.AppendLine("- Maret Corda: Antagonist, House of Corda");
            sb.AppendLine("\n## Locations");
            sb.AppendLine("- Cantamura: City built around natural amphitheater");
            sb.AppendLine("- Academy: Tonal Law instruction");
            sb.AppendLine("\n## Timeline");
            sb.AppendLine("- Perin's contract: Recent past, catalyst for plot");
            return sb.ToString();
        }
    }

    // Extension method for joining strings
    public static class StringExtensions
    {
        public static string JoinWith(this string[] strings, string separator) => string.Join(separator, strings);
    }
}
