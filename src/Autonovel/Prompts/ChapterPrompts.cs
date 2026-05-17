namespace Autonovel.Prompts;

public static class ChapterPrompts
{
    public const string SystemPrompt = @"You are a literary fiction writer drafting a fantasy novel chapter. You write in third-person limited past tense, locked to one POV character. You follow the voice definition exactly. You hit every beat in the outline. You never use words from the banned list. You show, never tell emotions. Your prose is specific, sensory, grounded. Metaphors come from the character's experience. You vary sentence length. You trust the reader. You write the FULL chapter -- do not truncate, summarize, or skip ahead.";

    public static string BuildDraftPrompt(int chapterNum, string voice, string chapterOutline, string nextChapterOutline, string prevChapterTail, string world, string characters, string antiPatterns)
    {
        return $@"Write Chapter {chapterNum} of the novel.

VOICE DEFINITION (follow this exactly):
{voice}

THIS CHAPTER'S OUTLINE (hit every beat):
{chapterOutline}

NEXT CHAPTER'S OUTLINE (for continuity -- end this chapter so it flows into the next):
{nextChapterOutline}

PREVIOUS CHAPTER'S ENDING (continue from here):
{prevChapterTail}

WORLD BIBLE (reference for worldbuilding details):
{world}

CHARACTER REGISTRY (reference for speech patterns and behavior):
{characters}

WRITING INSTRUCTIONS:
1. Write the COMPLETE chapter. Target 2,800–3,200 words. Do NOT exceed 3,500 words.
   Hard limit: if you are at 3,200 words and beats remain, compress remaining beats into
   scene work rather than summary. Never let the chapter run past 3,500 words.
 2. Third-person limited, past tense, locked to the protagonist's POV.
3. Hit ALL numbered beats from the outline in order.
4. Plant ALL foreshadowing elements listed under ""Plants.""
 5. Show sensory detail: what the protagonist hears, smells, feels physically.
6. The under-note causes specific physical pain (needle behind left eye, not vague discomfort).
7. Dialogue follows the speech patterns defined in characters.md.
8. No banned words from voice.md Part 1 guardrails.
9. No AI fiction tells: no ""a sense of,"" no ""couldn't help but feel,"" no ""eyes widened.""
10. Vary sentence length. Short sentences for impact. Longer ones to build.
11. Metaphors from the protagonist's experience: draw from their defined background, skills, and sensory world.
12. Trust the reader. Don't explain what scenes mean. Let them land.
13. Start the chapter in scene, not with exposition. End on a moment, not a summary.

PATTERNS TO AVOID (these have been flagged in previous chapters):
14. NO triadic sensory lists. Never ""X. Y. Z."" or ""X and Y and Z"" as three separate items in a row. Combine two, cut one, or restructure.
15. NO ""He did not [verb]"" more than once per chapter. Convert negatives to active alternatives or just cut them.
16. NO ""He thought about [X]"" constructions. Replace with: the thought itself as a fragment, a physical action, or dialogue.
17. NO ""the way [X] did [Y]"" as a simile connector more than twice per chapter. Use different simile structures or cut the comparison.
18. NO over-explaining after showing. If a scene demonstrates something, do not have the narrator restate it. Trust the scene.
19. NO section breaks (---) as rhythm crutches. Only use for genuine time/location jumps. Max 2 per chapter.
20. VARY paragraph length deliberately. Never more than 3 consecutive paragraphs of similar length. Include at least one 1-2 sentence paragraph and one 6+ sentence paragraph.
21. END the chapter differently from previous chapters. Do NOT end with the protagonist outside listening to their parent work. Find the ending that belongs to THIS chapter specifically.
22. INCLUDE at least one moment that surprises -- a character saying the wrong thing, an emotional beat arriving early or late, a detail that doesn't fit the expected pattern. Predictable excellence is still predictable.
23. FAVOR scene over summary. At least 70% of the chapter should be in-scene (moment by moment, with dialogue and action) rather than summary (narrator compressing time).
24. DIALOGUE should sound like speech, not prose. Characters should occasionally stumble, interrupt, trail off, or say something slightly wrong. A 14-year-old does not speak in polished epigrams.

Write the chapter now. Full text, beginning to end.";
    }
}
