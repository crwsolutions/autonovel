namespace Autonovel.Prompts;

public static class EvaluationPrompts
{
    // Full prompts would go here - extracted from Python evaluate.py
    // Due to length (800+ lines), these are summarized as constants
    
    public const string FoundationSystem = @"You are a literary critic and novel editor. You evaluate fiction with precision. Always respond with valid JSON. No markdown fences, no preamble -- just the JSON object.";
    
    public const string ChapterSystem = @"You are a literary critic and professor of fiction. Evaluate this chapter on a scale of 0-10 where: 9-10: Published quality, 7-8: Strong with minor gaps, 5-6: Functional but flat, 3-4: Significant problems, 1-2: Not usable. The MEDIAN score for a competent AI-generated chapter should be 6. A 7 means it does something a generic AI draft wouldn't. An 8 means a human editor would keep it with minor notes. Most dimensions should score 6-7. Reserve 8+ for genuine excellence.";
    
    // The full prompts are too long to include here - they would be copied from Python evaluate.py
    // FOUNDATION_PROMPT, CHAPTER_PROMPT, FULL_NOVEL_PROMPT
}
