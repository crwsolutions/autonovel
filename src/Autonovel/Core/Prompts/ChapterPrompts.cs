using System;
using System.Text;

namespace Autonovel.Core.Prompts
{
    public static class ChapterPrompts
    {
        public const string DraftSystemPrompt = "You are a literary fiction writer drafting a fantasy novel chapter. You write in third-person limited past tense, locked to one POV character. You follow the voice definition exactly. You hit every beat in the outline. You never use words from the banned list. You show, never tell emotions. Your prose is specific, sensory, grounded. Metaphors come from the character's experience. You vary sentence length. You trust the reader. You write the FULL chapter -- do not truncate, summarize, or skip ahead.";

        public static string BuildDraftPrompt(int chapterNum, string voice, string chapterOutline, string nextChapterOutline, string prevChapterTail, string world, string characters)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Write Chapter {chapterNum} of \"The Second Son of the House of Bells.\"");
            sb.AppendLine();
            sb.AppendLine("VOICE DEFINITION (follow this exactly):");
            sb.AppendLine(voice);
            sb.AppendLine();
            sb.AppendLine($"THIS CHAPTER'S OUTLINE (hit every beat):");
            sb.AppendLine(chapterOutline);
            sb.AppendLine();
            sb.AppendLine("NEXT CHAPTER'S OUTLINE (for continuity -- end this chapter so it flows into the next):");
            sb.AppendLine(nextChapterOutline);
            sb.AppendLine();
            sb.AppendLine("PREVIOUS CHAPTER'S ENDING (continue from here):");
            sb.AppendLine(prevChapterTail);
            sb.AppendLine();
            sb.AppendLine("WORLD BIBLE (reference for worldbuilding details):");
            sb.AppendLine(world);
            sb.AppendLine();
            sb.AppendLine("CHARACTER REGISTRY (reference for speech patterns and behavior):");
            sb.AppendLine(characters);
            sb.AppendLine();
            sb.AppendLine("WRITING INSTRUCTIONS:");
            sb.AppendLine($"1. Write the COMPLETE chapter. Target ~3,200 words. Do not truncate or summarize.");
            sb.AppendLine("2. Third-person limited, past tense, locked to Cass's POV.");
            sb.AppendLine("3. Hit ALL numbered beats from the outline in order.");
            sb.AppendLine("4. Plant ALL foreshadowing elements listed under \"Plants.\"");
            sb.AppendLine("5. Show sensory detail: what Cass hears, smells, feels physically.");
            sb.AppendLine("6. The under-note causes specific physical pain (needle behind left eye, not vague discomfort).");
            sb.AppendLine("7. Dialogue follows the speech patterns defined in characters.md.");
            sb.AppendLine("8. No banned words from voice.md Part 1 guardrails.");
            sb.AppendLine("9. No AI fiction tells: no \"a sense of,\" no \"couldn't help but feel,\" no \"eyes widened.\"");
            sb.AppendLine("10. Vary sentence length. Short sentences for impact. Longer ones to build.");
            sb.AppendLine("11. Metaphors from Cass's experience: sound, bronze, craft, the body's response to pitch.");
            sb.AppendLine("12. Trust the reader. Don't explain what scenes mean. Let them land.");
            sb.AppendLine("13. Start the chapter in scene, not with exposition. End on a moment, not a summary.");
            sb.AppendLine();
            sb.AppendLine("PATTERNS TO AVOID (these have been flagged in previous chapters):");
            sb.AppendLine("14. NO triadic sensory lists. Never \"X. Y. Z.\" or \"X and Y and Z\" as three");
            sb.AppendLine("    separate items in a row. Combine two, cut one, or restructure.");
            sb.AppendLine("15. NO \"He did not [verb]\" more than once per chapter. Convert negatives");
            sb.AppendLine("    to active alternatives or just cut them.");
            sb.AppendLine("16. NO \"He thought about [X]\" constructions. Replace with: the thought");
            sb.AppendLine("    itself as a fragment, a physical action, or dialogue.");
            sb.AppendLine("17. NO \"the way [X] did [Y]\" as a simile connector more than twice per");
            sb.AppendLine("    chapter. Use different simile structures or cut the comparison.");
            sb.AppendLine("18. NO over-explaining after showing. If a scene demonstrates something,");
            sb.AppendLine("    do not have the narrator restate it. Trust the scene.");
            sb.AppendLine("19. NO section breaks (---) as rhythm crutches. Only use for genuine");
            sb.AppendLine("    time/location jumps. Max 2 per chapter.");
            sb.AppendLine("20. VARY paragraph length deliberately. Never more than 3 consecutive");
            sb.AppendLine("    paragraphs of similar length. Include at least one 1-2 sentence");
            sb.AppendLine("    paragraph and one 6+ sentence paragraph.");
            sb.AppendLine("21. END the chapter differently from previous chapters. Do NOT end with");
            sb.AppendLine("    Cass outside listening to his father work. Find the ending that");
            sb.AppendLine("    belongs to THIS chapter specifically.");
            sb.AppendLine("22. INCLUDE at least one moment that surprises -- a character saying");
            sb.AppendLine("    the wrong thing, an emotional beat arriving early or late, a detail");
            sb.AppendLine("    that doesn't fit the expected pattern. Predictable excellence is");
            sb.AppendLine("    still predictable.");
            sb.AppendLine("23. FAVOR scene over summary. At least 70% of the chapter should be");
            sb.AppendLine("    in-scene (moment by moment, with dialogue and action) rather than");
            sb.AppendLine("    summary (narrator compressing time).");
            sb.AppendLine("24. DIALOGUE should sound like speech, not prose. Characters should");
            sb.AppendLine("    occasionally stumble, interrupt, trail off, or say something");
            sb.AppendLine("    slightly wrong. A 14-year-old does not speak in polished epigrams.");
            sb.AppendLine();
            sb.AppendLine("Write the chapter now. Full text, beginning to end.");
            return sb.ToString();
        }
    }
}
