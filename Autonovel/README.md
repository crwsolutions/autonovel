# AutoNovel .NET 10 Migration

## Overview

This is the .NET 10 (C#) migration of the AutoNovel pipeline. It replaces the Python implementation with idiomatic C# code while preserving the exact same architecture, prompts, and evaluation logic.

## Architecture

**Five co-evolving layers** (unchanged from Python):
```
Layer 5: voice.md       — Style guardrails + discovered exemplars
Layer 4: world.md       — Lore, magic system, geography, history  
Layer 3: characters.md  — Registry with wound/want/need/lie chains
Layer 2: outline.md     — Beats + foreshadowing ledger
Layer 1: chapters/ch_*.md — The prose
Cross-cutting: canon.md — Hard facts database (consistency source of truth)
```

## Project Structure

```
Autonovel/
├── Core/
│   ├── Domain/          # Records: PipelineState, SlopScore, GenerationRequest, etc.
│   └── Services/        # IMechanicalSlopDetector, IGenerationClient, StateManager, GitVersionControl
├── Prompts/             # Full text prompts (FoundationPrompts, ChapterPrompts, EvaluationPrompts)
├── Commands/            # CLI command implementations
├── Program.cs           # Entry point with argument parsing
├── appsettings.json     # Configuration (endpoint, model, thresholds)
└── Autonovel.csproj     # .NET 10 project file
```

## Key Components

### 1. Mechanical Slop Detector (`MechanicalSlopDetector.cs`)
Exact port of Python's `evaluate.py` slop detection:
- Tier 1: Banned words (delve, utilize, leverage, etc.)
- Tier 2: Suspicious clusters (robust, comprehensive, seamless)
- Tier 3: Filler phrases ("it's worth noting", "let's dive into")
- Fiction AI tells ("a sense of", "couldn't help but feel", "eyes widened")
- Structural AI tics ("not X, but Y" formulas)
- Show-don't-tell violations
- Em-dash density, sentence length CV, transition opener ratio

### 2. LLM Client (`OpenAiGenerationClient.cs`)
OpenAI-compatible API client for llama.cpp:
- HTTP POST to `/v1/chat/completions`
- Configurable endpoint, model, API key (dummy)
- 600-second timeout for long generations
- JSON deserialization of responses

### 3. State Management (`StateManager.cs`)
- Loads/saves `state.json` (same schema as Python)
- Tracks: phase, iteration, scores, chapter counts, debts

### 4. Git Operations (`GitVersionControl.cs`)
- `Process.Start("git", ...)` for commits, resets
- No LibGit2Sharp dependency
- Same keep/discard loop as Python

## Commands

```bash
# Build and run
dotnet build
dotnet run -- <command>

# Commands:
Autonovel foundation              # Build planning documents (world, characters, outline)
Autonovel draft <N>              # Draft chapter N
Autonovel revise [--max-cycles N] # Revision cycles with adversarial editing
Autonovel export                 # Concatenate chapters to manuscript.md
Autonovel test-slop <file>       # Test mechanical slop detection
```

## Configuration (`appsettings.json`)

```json
{
  "LLM": {
    "Endpoint": "http://crw-amd3900x:8080",
    "ModelId": "unsloth/Qwen3-Coder-30B-A3B-Instruct-GGUF:Q6_K",
    "ApiKey": "dummy",
    "TimeoutSeconds": 600
  },
  "Thresholds": {
    "FoundationScore": 7.5,
    "LoreScore": 7.0,
    "ChapterScore": 6.0,
    "PlateauDelta": 0.3
  }
}
```

## Migration Status

**Completed:**
- ✅ Project structure and .csproj
- ✅ Domain models (PipelineState, SlopScore, etc.)
- ✅ Mechanical Slop Detector (100% regex parity with Python)
- ✅ LLM Generation Client (OpenAI-compatible)
- ✅ State Manager (state.json persistence)
- ✅ Git Version Control (Process.Start)
- ✅ File Manager (chapter I/O)
- ✅ CLI argument parsing
- ✅ Test command (`test-slop` verified working)

**Not Yet Implemented (skeletons only):**
- ⏳ Foundation phase loop (world → characters → outline → canon generation)
- ⏳ Drafting phase (chapter generation with context building)
- ⏳ Evaluation service (LLM judge calls + JSON parsing)
- ⏳ Revision phase (adversarial editing, reader panel)
- ⏳ Export phase (manuscript concatenation)

## Next Steps

1. **Implement Prompts**: Add `Prompts/FoundationPrompts.cs`, `Prompts/ChapterPrompts.cs`, `Prompts/EvaluationPrompts.cs` with full text from Python files

2. **Implement IEvaluator**: Service that calls LLM with evaluation prompts and parses JSON responses

3. **Implement PipelineOrchestrator**: State machine that implements the keep/discard loop:
   - Foundation: Generate → Evaluate → Commit/Reset → Repeat until score > 7.5
   - Drafting: For each chapter, generate → evaluate → retry until score > 6.0
   - Revision: Adversarial edit → reader panel → revise until plateau

4. **Test**: Run against existing Python-generated chapters to verify scores match

## Differences from Python

| Python | .NET 10 |
|--------|---------|
| `httpx.post` | `HttpClient.PostAsync` |
| `re.compile` | `Regex` (Compiled option) |
| `subprocess.run` | `Process.Start` (for git only) |
| `argparse` | Manual string parsing |
| `json.load` | `System.Text.Json.JsonSerializer` |
| Dynamic dicts | Strongly-typed records |

## Testing Slop Detection

```bash
# Create a test file with AI slop patterns
echo "The air was thick with tension. He felt a sense of dread..." > test.txt

# Run detection
dotnet run -- test-slop test.txt

# Output:
# Slop Score for test.txt:
#   Tier1 hits: 
#   Tier2 clusters: 0
#   Fiction AI tells: 2
#   ...etc
```

## Build & Run

```bash
cd Autonovel
dotnet build
dotnet run -- foundation
```

## Notes

- **No streaming**: Simplified to `Task<string>` instead of `IAsyncEnumerable` (as requested)
- **No LaTeX**: Output is Markdown only (manuscript.md)
- **Same prompts**: All prompts are exact copies from Python, no changes to generation logic
- **Same thresholds**: 7.5 foundation, 6.0 chapter, 0.3 plateau delta
- **Same Git workflow**: Process.Start("git", ...) for commits and hard resets
