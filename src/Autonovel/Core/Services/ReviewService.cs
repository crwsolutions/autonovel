using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Autonovel.Core.Domain;
using Autonovel.Core.Prompts;

namespace Autonovel.Core.Services;

public interface IReviewService
{
    Task<OpusReviewResult> ReviewAsync(string? outputPath = null, CancellationToken ct = default);
    Task<OpusReviewResult> ParseLatestAsync(CancellationToken ct = default);
}

public class ReviewService : IReviewService
{
    private readonly IGenerationClient _client;
    private readonly IFileManager _fileManager;
    private readonly string _baseDirectory;
    private readonly JsonSerializerOptions _jsonOptions;

    public ReviewService(
        IGenerationClient client,
        IFileManager fileManager,
        string baseDirectory)
    {
        _client = client;
        _fileManager = fileManager;
        _baseDirectory = baseDirectory;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    public async Task<OpusReviewResult> ReviewAsync(string? outputPath = null, CancellationToken ct = default)
    {
        // Get title
        var title = await GetTitleAsync();
        
        // Build manuscript
        var chapters = await _fileManager.ListChaptersAsync();
        var manuscript = await BuildManuscriptAsync(chapters);
        var wordCount = manuscript.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
        
        Console.Error.WriteLine($"Manuscript: {chapters.Count} chapters, {wordCount:N0} words");
        Console.Error.WriteLine($"Sending to model ({manuscript.Length:N0} chars)...");

        // Build prompt and call model
        var prompt = ReviewPrompts.BuildOpusReviewPrompt(title, manuscript);
        var response = await _client.GenerateAsync(new GenerationRequest(
            SystemPrompt: "", // No system prompt for this one, like Python
            UserPrompt: prompt,
            Temperature: 0.7f), ct);

        // Parse review
        var parsed = ParseReview(response);
        
        // Determine stop condition
        var (shouldStop, stopReason) = ShouldStop(parsed);
        
        // Build result
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var result = new OpusReviewResult(
            StarRating: parsed.Stars,
            CriticSummary: parsed.CriticSummary,
            ProfessorItems: parsed.Items,
            TotalItems: parsed.Items.Count,
            MajorItems: parsed.Items.Count(i => i.Severity == "major"),
            QualifiedItems: parsed.Items.Count(i => i.Qualified),
            ShouldStop: shouldStop,
            StopReason: stopReason,
            RawText: response,
            Timestamp: timestamp,
            Title: title,
            WordCount: wordCount
        );

        // Save to edit_logs
        var logsDir = Path.Combine(_baseDirectory, "edit_logs");
        Directory.CreateDirectory(logsDir);
        var logPath = Path.Combine(logsDir, $"{timestamp}_review.json");
        await File.WriteAllTextAsync(logPath, JsonSerializer.Serialize(result, _jsonOptions));
        Console.Error.WriteLine($"\nReview saved to {logPath}");

        // Save human-readable copy if requested
        if (!string.IsNullOrEmpty(outputPath))
        {
            await File.WriteAllTextAsync(outputPath, response);
            Console.Error.WriteLine($"Human-readable copy: {outputPath}");
        }

        // Print summary
        PrintSummary(result);

        return result;
    }

    public async Task<OpusReviewResult> ParseLatestAsync(CancellationToken ct = default)
    {
        var logsDir = Path.Combine(_baseDirectory, "edit_logs");
        if (!Directory.Exists(logsDir))
        {
            throw new DirectoryNotFoundException($"No edit_logs directory found at {logsDir}");
        }

        var reviews = Directory.EnumerateFiles(logsDir, "*_review.json", SearchOption.TopDirectoryOnly)
            .OrderByDescending(p => p)
            .ToList();

        if (!reviews.Any())
        {
            throw new InvalidOperationException("No reviews found. Run: dotnet run -- review first");
        }

        var latestPath = reviews.First();
        var json = await File.ReadAllTextAsync(latestPath);
        var result = JsonSerializer.Deserialize<OpusReviewResult>(json, _jsonOptions);

        if (result == null)
        {
            throw new InvalidOperationException("Failed to deserialize review");
        }

        // Print formatted output
        Console.WriteLine($"Latest review: {result.Timestamp}");
        Console.WriteLine($"Stars: {result.StarRating}");
        Console.WriteLine($"\nACTIONABLE ITEMS ({result.TotalItems}):\n");

        foreach (var item in result.ProfessorItems)
        {
            var qual = item.Qualified ? " [QUALIFIED]" : "";
            Console.WriteLine($"  {item.Number}. [{item.Severity.ToUpper()}] [{item.Type}]{qual}");
            Console.WriteLine($"     {item.Title}");
            if (!string.IsNullOrEmpty(item.Suggestion))
            {
                var sugg = item.Suggestion.Length > 120 ? item.Suggestion[..120] + "..." : item.Suggestion;
                Console.WriteLine($"     Suggestion: {sugg}");
            }
        }

        Console.WriteLine($"\n{'='*50}");
        Console.WriteLine($"Stop revising? {(result.ShouldStop ? "YES — " : "NO — ")} {result.StopReason}");
        Console.WriteLine($"{'='*50}");

        return result;
    }

    private async Task<string> GetTitleAsync()
    {
        // Try outline.md first
        var outlinePath = Path.Combine(_baseDirectory, "outline.md");
        if (File.Exists(outlinePath))
        {
            var firstLine = await File.ReadAllLinesAsync(outlinePath);
            if (firstLine.Length > 0)
            {
                var title = firstLine[0].Replace("#", "").Trim();
                if (!string.IsNullOrEmpty(title))
                    return title;
            }
        }

        // Try ch_01.md
        var ch1Path = Path.Combine(_baseDirectory, "chapters", "ch_01.md");
        if (File.Exists(ch1Path))
        {
            var firstLine = await File.ReadAllLinesAsync(ch1Path);
            if (firstLine.Length > 0)
            {
                return firstLine[0].Replace("#", "").Trim();
            }
        }

        return "Untitled Novel";
    }

    private async Task<string> BuildManuscriptAsync(List<int> chapters)
    {
        if (!chapters.Any())
        {
            throw new InvalidOperationException("No chapters found.");
        }

        var parts = new List<string>();
        foreach (var chNum in chapters.OrderBy(c => c))
        {
            var content = await _fileManager.ReadFileAsync($"chapters/ch_{chNum:02d}.md");
            parts.Add(content);
        }

        return string.Join("\n\n---\n\n", parts);
    }

    private (double? Stars, string CriticSummary, List<ReviewItem> Items) ParseReview(string reviewText)
    {
        // Split into critic and professor sections
        var sectionSplit = Regex.Split(reviewText, 
            @"(?:Professor|PROFESSOR|professor).*?(?:Review|Assessment|Analysis|Craft)", 
            RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
        
        var criticText = sectionSplit.Length > 0 ? sectionSplit[0] : reviewText;
        var professorText = sectionSplit.Length > 1 ? sectionSplit[1] : "";

        // Extract star rating
        double? stars = null;
        var starMatch = Regex.Match(criticText, @"★+½?|\d+\.?\d*\s*/?\s*(?:out of\s*)?(?:five|5)", 
            RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
        if (starMatch.Success)
        {
            var starStr = starMatch.Value;
            stars = starStr.Count(c => c == '★') + (starStr.Contains('½') ? 0.5 : 0);
            
            // If numeric (e.g., "4/5"), parse it
            if (stars == 0 && Regex.IsMatch(starStr, @"\d"))
            {
                var numericMatch = Regex.Match(starStr, @"(\d+\.?\d*)");
                if (numericMatch.Success && double.TryParse(numericMatch.Value, out var num))
                {
                    stars = num;
                }
            }
        }

        // Extract professor's numbered items
        var items = new List<ReviewItem>();
        var profSections = Regex.Split(professorText, 
            @"\n(?=\d+\.\s+[A-Z])", 
            RegexOptions.None, TimeSpan.FromMilliseconds(100));

        foreach (var section in profSections)
        {
            if (string.IsNullOrWhiteSpace(section))
                continue;

            // Extract number and title
            var titleMatch = Regex.Match(section, @"(\d+)\.\s+(.+?)(?:\n|$)");
            if (!titleMatch.Success)
                continue;

            var num = int.Parse(titleMatch.Groups[1].Value);
            var title = titleMatch.Groups[2].Value.Trim();

            var textLower = section.ToLowerInvariant();

            // Classify severity
            string severity;
            if (ContainsAny(textLower, new[] { "major", "significant", "primary", "most important" }))
                severity = "major";
            else if (ContainsAny(textLower, new[] { "minor", "small", "slight", "cosmetic" }))
                severity = "minor";
            else
                severity = "moderate";

            // Classify type
            string type;
            if (ContainsAny(textLower, new[] { "cut", "compress", "trim", "reduce", "consolidate" }))
                type = "compression";
            else if (ContainsAny(textLower, new[] { "add", "expand", "introduce", "give", "more" }))
                type = "addition";
            else if (ContainsAny(textLower, new[] { "repetit", "recurring", "frequency", "tic", "gesture" }))
                type = "mechanical";
            else if (ContainsAny(textLower, new[] { "restructur", "rearrang", "move", "reorganiz" }))
                type = "structural";
            else
                type = "revision";

            // Check if qualified/hedged
            var qualified = ContainsAny(textLower, new[] { 
                "individually fine", "largely successful", "each instance works",
                "minor relative to", "small complaint", "costs of ambition",
                "not a flaw", "deliberate choice", "thematically coherent"
            });

            // Extract suggestion
            var suggestion = "";
            var suggMatch = Regex.Match(section, 
                @"(?:Specific\s+)?[Ss]uggestion[s]?:?\s*\n?(.*?)(?=\n\d+\.|\n\n[A-Z]|\Z)",
                RegexOptions.Singleline, TimeSpan.FromMilliseconds(100));
            if (suggMatch.Success)
            {
                suggestion = suggMatch.Groups[1].Value.Trim();
                if (suggestion.Length > 500)
                    suggestion = suggestion[..500];
            }

            var fullText = section.Trim();
            if (fullText.Length > 1000)
                fullText = fullText[..1000];

            items.Add(new ReviewItem(
                Number: num,
                Title: title,
                Severity: severity,
                Type: type,
                Qualified: qualified,
                Suggestion: suggestion,
                FullText: fullText
            ));
        }

        var criticSummary = criticText.Trim();
        if (criticSummary.Length > 500)
            criticSummary = criticSummary[..500];

        return (stars, criticSummary, items);
    }

    private (bool ShouldStop, string Reason) ShouldStop((double? Stars, string CriticSummary, List<ReviewItem> Items) parsed)
    {
        var stars = parsed.Stars ?? 0;
        var total = parsed.Items.Count;
        var major = parsed.Items.Count(i => i.Severity == "major");
        var qualified = parsed.Items.Count(i => i.Qualified);

        if (stars >= 4.5 && major == 0)
            return (true, "★★★★½ with no major items");
        
        if (stars >= 4 && total > 0 && (double)qualified / total > 0.5)
            return (true, $"Star rating {stars:F1} with {qualified}/{total} items qualified");
        
        if (total <= 2)
            return (true, $"Only {total} items found");

        return (false, $"{major} major items, {total - qualified} unqualified");
    }

    private bool ContainsAny(string text, string[] keywords)
    {
        return keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    private void PrintSummary(OpusReviewResult result)
    {
        Console.WriteLine($"\n{'='*50}");
        Console.WriteLine("REVIEW SUMMARY");
        Console.WriteLine($"  Stars: {result.StarRating}");
        Console.WriteLine($"  Items: {result.TotalItems} ({result.MajorItems} major)");
        Console.WriteLine($"  Qualified: {result.QualifiedItems}/{result.TotalItems}");
        Console.WriteLine($"  Stop revising? {(result.ShouldStop ? "YES — " : "NO — ")} {result.StopReason}");
        Console.WriteLine($"{'='*50}");
    }
}
