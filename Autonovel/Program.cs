using Autonovel.Core.Domain;
using Autonovel.Core.Services;

Console.WriteLine("Autonovel - AI-assisted novel generation pipeline");
Console.WriteLine("Usage: Autonovel <command> [options]");
Console.WriteLine("Commands:");
Console.WriteLine("  foundation              Build planning documents");
Console.WriteLine("  draft <N>              Draft chapter N");
Console.WriteLine("  revise [--max-cycles N] Run revision cycles");
Console.WriteLine("  export                 Export manuscript");
Console.WriteLine("  test-slop <file>       Test slop detection on file");
Console.WriteLine();

if (args.Length == 0)
{
    return 1;
}

var command = args[0];
var baseDirectory = Directory.GetCurrentDirectory();

// Settings
var settings = new LLMSettings
{
    Endpoint = "http://crw-amd3900x:8080",
    ModelId = "unsloth/Qwen3-Coder-30B-A3B-Instruct-GGUF:Q6_K",
    ApiKey = "dummy",
    TimeoutSeconds = 600
};

// Services
var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds) };
var slopDetector = new MechanicalSlopDetector();
var generationClient = new OpenAiGenerationClient(httpClient, settings.Endpoint, settings.ModelId, settings.ApiKey);
var stateManager = new StateManager(Path.Combine(baseDirectory, "state.json"));
var git = new GitVersionControl(baseDirectory);
var fileManager = new FileManager(baseDirectory, "chapters");
var evaluator = new Evaluator(generationClient, slopDetector);
var orchestrator = new PipelineOrchestrator(generationClient, evaluator, stateManager, git, fileManager);

if (command == "foundation")
    return await RunFoundation();
else if (command == "draft" && args.Length > 1 && int.TryParse(args[1], out int n))
    return await RunDraft(n);
else if (command == "revise")
    return await RunRevise();
else if (command == "export")
    return await RunExport();
else if (command == "test-slop" && args.Length > 1)
    return await TestSlop(args[1]);
else
{
    Console.WriteLine($"Unknown command: {command}");
    return 1;
}

async Task<int> RunFoundation()
    {
        var config = new PipelineConfig { MaxIterations = 10, FoundationThreshold = 7.5f, LoreThreshold = 7.0f };
        var result = await orchestrator.RunFoundationAsync(config);
        Console.WriteLine($"\nResult: {result.Message}");
        return result.Success ? 0 : 1;
    }

    async Task<int> RunDraft(int chapterNum)
    {
        var config = new PipelineConfig { MaxIterations = 5, DraftThreshold = 6.0f };
        var result = await orchestrator.RunDraftAsync(chapterNum, config);
        Console.WriteLine($"\nResult: {result.Message}");
        return result.Success ? 0 : 1;
    }

    async Task<int> RunRevise()
    {
        var config = new PipelineConfig { RevisionMaxCycles = 5 };
        var result = await orchestrator.RunRevisionAsync(5, config);
        Console.WriteLine($"\nResult: {result.Message}");
        return result.Success ? 0 : 1;
    }

    async Task<int> RunExport()
    {
        var config = new PipelineConfig();
        var result = await orchestrator.RunExportAsync(config);
        Console.WriteLine($"\nResult: {result.Message}");
        return result.Success ? 0 : 1;
    }

async Task<int> TestSlop(string file)
{
    if (!File.Exists(file))
    {
        Console.WriteLine($"File not found: {file}");
        return 1;
    }
    
    var text = File.ReadAllText(file);
    var score = slopDetector.Calculate(text);
    
    Console.WriteLine($"Slop Score for {file}:");
    Console.WriteLine($"  Tier1 hits: {string.Join(", ", score.Tier1Hits.Select(k => $"{k.Key}({k.Value})"))}");
    Console.WriteLine($"  Tier2 clusters: {score.Tier2ClusterCount}");
    Console.WriteLine($"  Fiction AI tells: {score.FictionAITellCount}");
    Console.WriteLine($"  Structural tics: {score.StructuralTicCount}");
    Console.WriteLine($"  Em-dash density: {score.EmDashDensity:F1}/1000 words");
    Console.WriteLine($"  Sentence length CV: {score.SentenceLengthCV:F3}");
    Console.WriteLine($"  Total penalty: {score.SlopPenalty:F2}/10");
    return 0;
}

public record LLMSettings(
    string Endpoint = "http://localhost:8080",
    string ModelId = "openai-compatible",
    string ApiKey = "dummy",
    int TimeoutSeconds = 600
);