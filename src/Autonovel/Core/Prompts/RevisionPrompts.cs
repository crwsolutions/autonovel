using System;
using System.Text;

namespace Autonovel.Core.Prompts
{
    public static class RevisionPrompts
    {
        public const string AdversarialEditorSystem = "You are a ruthless literary editor. You cut fat from prose. You have no sentiment about good-enough sentences -- if a sentence isn't earning its place, it goes. You quote exactly from the text. You never invent or paraphrase. Always respond with valid JSON.";

        public static string BuildAdversarialEditPrompt(string chapterText, int wordCount)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are editing a fantasy novel chapter. Your job: identify exactly what to cut or rewrite to make this chapter tighter, sharper, more alive.");
            sb.AppendLine();
            sb.AppendLine($"THE CHAPTER ({wordCount} words):");
            sb.AppendLine(chapterText);
            sb.AppendLine();
            sb.AppendLine("YOUR TASK:");
            sb.AppendLine("1. Find 10-20 specific passages that should be CUT or REWRITTEN.");
            sb.AppendLine("   For each, quote the EXACT text (minimum 10 words of the quote so it's unambiguous), explain why it's weak, and classify it.");
            sb.AppendLine();
            sb.AppendLine("2. Classify each cut as one of:");
            sb.AppendLine("   - FAT: adds nothing, could be removed with no loss");
            sb.AppendLine("   - REDUNDANT: restates what a previous sentence/scene already showed");
            sb.AppendLine("   - OVER-EXPLAIN: narrator explaining what the scene already demonstrated");
            sb.AppendLine("   - GENERIC: could appear in any novel, not specific to this world/character");
            sb.AppendLine("   - TELL: names an emotion or state instead of showing it");
            sb.AppendLine("   - STRUCTURAL: paragraph/section that disrupts pacing or rhythm");
            sb.AppendLine();
            sb.AppendLine("3. For REWRITE candidates (not cuts), provide a specific revision.");
            sb.AppendLine();
            sb.AppendLine("4. Estimate how many words could be cut total without losing anything the chapter needs.");
            sb.AppendLine();
            sb.AppendLine("Respond with JSON:");
            sb.AppendLine("{");
            sb.AppendLine("  \"cuts\": [");
            sb.AppendLine("    {");
            sb.AppendLine("      \"quote\": \"exact text from the chapter (10+ words)\",");
            sb.AppendLine("      \"type\": \"FAT|REDUNDANT|OVER-EXPLAIN|GENERIC|TELL|STRUCTURAL\",");
            sb.AppendLine("      \"reason\": \"why this should go\",");
            sb.AppendLine("      \"action\": \"CUT or REWRITE\",");
            sb.AppendLine("      \"rewrite\": \"replacement text if action is REWRITE, null if CUT\"");
            sb.AppendLine("    }");
            sb.AppendLine("  ],");
            sb.AppendLine("  \"total_cuttable_words\": N,");
            sb.AppendLine("  \"tightest_passage\": \"quote the best 2-3 sentences in the chapter -- the ones you'd never touch\",");
            sb.AppendLine("  \"loosest_passage\": \"quote the worst 2-3 sentences -- the ones that most need work\",");
            sb.AppendLine("  \"overall_fat_percentage\": N,");
            sb.AppendLine("  \"one_sentence_verdict\": \"what this chapter does well and what drags it down, in one sentence\"");
            sb.AppendLine("}");
            return sb.ToString();
        }

        // Reader Panel personas
        public const string EditorPersona = "You are a senior fiction editor at a major publishing house. You've edited 200+ novels. You care about prose texture, subtext, sentence-level craft, and whether the voice is consistent and earned. You notice when the narrator over-explains, when dialogue sounds written rather than spoken, when a metaphor is borrowed rather than earned. You are not cruel but you are precise. You've seen enough competent prose to know the difference between good and alive. You respond with valid JSON only.";

        public const string GenreReaderPersona = "You are an avid fantasy reader who reads 50+ novels a year. You care about pacing, mystery, worldbuilding payoff, and whether you want to keep turning pages. You get bored by beautiful prose that doesn't GO anywhere. You notice when an investigation stalls, when tension plateaus, when the author is more in love with their world than their story. You compare everything to Sanderson, Le Guin, Jemisin, Rothfuss, Hobb. You are generous with what you love and blunt about what bores you. You respond with valid JSON only.";

        public const string WriterPersona = "You are a published fantasy author with 5 novels and a Hugo nomination. You read as a craftsperson. You notice structure: where the beats fall, whether foreshadowing pays off, whether character arcs complete. You notice when technique shows versus when it disappears into the story. The highest compliment you give is 'I forgot I was reading.' The worst thing you can say is 'I can see the outline.' You care about the gap between what a novel attempts and what it achieves. You respond with valid JSON only.";

        public const string FirstReaderPersona = "You are a thoughtful general reader. Not a writer, not an editor, not a genre expert. You read for the experience. You know what you feel but not always why. You notice when you're moved, when you're bored, when you're confused, when you want to tell someone about what you just read. You don't use craft terminology. You say things like 'I didn't care about this part' and 'I had to put the book down after this scene because I needed a minute.' Your feedback is emotional and honest, not analytical. You respond with valid JSON only.";

        public static string BuildReaderPanelPrompt(string arcSummary)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You have just read a complete fantasy novel in summary form.");
            sb.AppendLine("The summaries include chapter-by-chapter events, opening and closing passages from each chapter, and key dialogue. The full novel is 72,422 words across 24 chapters.");
            sb.AppendLine();
            sb.AppendLine(arcSummary);
            sb.AppendLine();
            sb.AppendLine("Now answer these questions about the NOVEL AS A WHOLE. Be specific. Quote passages when you can. Name chapter numbers.");
            sb.AppendLine();
            sb.AppendLine("Respond with JSON:");
            sb.AppendLine("{");
            sb.AppendLine("  \"momentum_loss\": \"Where does the story lose momentum? Name the specific chapter(s) and what causes the drag. If it never loses momentum, say so and explain why.\",");
            sb.AppendLine("  \"earned_ending\": \"Does the ending feel earned by everything before it? Does the protagonist's choice in the climax land? Does the final image mirror the opening in a way that satisfies? What, if anything, feels unearned?\",");
            sb.AppendLine("  \"cut_candidate\": \"If the novel had to be 10% shorter (~7,000 words), which chapter or section would you cut first? Why? What would be lost?\",");
            sb.AppendLine("  \"missing_scene\": \"Is there a scene the novel NEEDS that it doesn't have? A conversation that should happen, a moment that's earned but never delivered, a character who deserves more page time? Be specific about where it would go.\",");
            sb.AppendLine("  \"thinnest_character\": \"Which character feels thinnest by the end? Who do you want to know more about? Who could be cut without the novel suffering?\",");
            sb.AppendLine("  \"best_scene\": \"What's the single best scene in the novel? Quote the moment that made you feel something. Why does it work?\",");
            sb.AppendLine("  \"worst_scene\": \"What's the single weakest scene? What goes wrong? How would you fix it?\",");
            sb.AppendLine("  \"would_recommend\": \"Would you recommend this novel? To whom? What would you say about it in one sentence?\",");
            sb.AppendLine("  \"haunts_you\": \"Is there a line or moment that stays with you after reading? Quote it.\",");
            sb.AppendLine("  \"next_book\": \"Would you read the author's next book? Why or why not?\"");
            sb.AppendLine("}");
            return sb.ToString();
        }

        public static string BuildRevisionBrief(int chapterNum, string issue, string panelConsensus, string chapterOutline)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Revision Brief: Chapter {chapterNum}");
            sb.AppendLine();
            sb.AppendLine("## Issue");
            sb.AppendLine(issue);
            sb.AppendLine();
            sb.AppendLine("## Panel Consensus");
            sb.AppendLine(panelConsensus);
            sb.AppendLine();
            sb.AppendLine("## Chapter Outline Context");
            sb.AppendLine(chapterOutline);
            sb.AppendLine();
            sb.AppendLine("## Instructions");
            sb.AppendLine($"Rewrite Chapter {chapterNum} to address the issue identified above. Preserve:");
            sb.AppendLine("- Existing voice and character work");
            sb.AppendLine("- Essential beats from the outline");
            sb.AppendLine("- The chapter's function in the arc");
            sb.AppendLine();
            sb.AppendLine("Focus on:");
            sb.AppendLine("- Cutting fat and redundancy");
            sb.AppendLine("- Sharpening prose");
            sb.AppendLine("- Addressing the specific weakness identified by the panel");
            sb.AppendLine();
            sb.AppendLine("Target: ~3,200 words. Do not truncate.");
            return sb.ToString();
        }
    }
}
