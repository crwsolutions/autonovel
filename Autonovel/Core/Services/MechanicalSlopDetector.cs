using System.Text.RegularExpressions;
using Autonovel.Core.Domain;

namespace Autonovel.Core.Services;

public interface IMechanicalSlopDetector
{
    SlopScore Calculate(string text);
}

public class MechanicalSlopDetector : IMechanicalSlopDetector
{
    private static readonly HashSet<string> Tier1Banned = new(StringComparer.OrdinalIgnoreCase)
    {
        "delve", "utilize", "leverage", "facilitate", "elucidate", "embark",
        "endeavor", "encompass", "multifaceted", "tapestry", "paradigm",
        "synergy", "synergize", "holistic", "catalyze", "catalyst",
        "juxtapose", "myriad", "plethora"
    };

    private static readonly HashSet<string> Tier2Suspicious = new(StringComparer.OrdinalIgnoreCase)
    {
        "robust", "comprehensive", "seamless", "seamlessly", "cutting-edge",
        "innovative", "streamline", "empower", "foster", "enhance", "elevate",
        "optimize", "pivotal", "intricate", "profound", "resonate", "underscore",
        "harness", "cultivate", "bolster", "galvanize", "cornerstone",
        "game-changer", "scalable"
    };

    private static readonly Regex[] Tier3FillerPatterns = [
        new(@"it's? worth noting that", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline),
        new(@"it's? important to note that", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline),
        new(@"^importantly,?\s", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline),
        new(@"^notably,?\s", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline),
        new(@"^interestingly,?\s", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline),
        new(@"let's? dive into", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"let's? explore", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"as we can see", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"^furthermore,?\s", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline),
        new(@"^moreover,?\s", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline),
        new(@"^additionally,?\s", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline),
        new(@"in today's .*(fast-paced|digital|modern)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"at the end of the day", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"it goes without saying", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"when it comes to", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"one might argue that", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"not just .+, but", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    ];

    private static readonly HashSet<string> TransitionOpeners = new(StringComparer.OrdinalIgnoreCase)
    {
        "however", "furthermore", "additionally", "moreover",
        "nevertheless", "consequently", "nonetheless", "similarly"
    };

    private static readonly Regex[] FictionAITells = [
        new(@"a sense of \w+", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"couldn't help but feel", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"the weight of \w+", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"the air was thick with", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"eyes widened", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"a wave of \w+ washed over", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"a pang of \w+", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"heart pounded in (?:his|her|their) chest", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"(?:raven|dark|golden|silver) (?:hair|tresses) (?:spilled|cascaded|tumbled|fell)", 
            RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"piercing (?:blue|green|gray|grey|dark) eyes", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"a knowing (?:smile|grin|look|glance)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"(?:he|she|they) felt a (?:surge|rush|wave|pang|flicker) of", 
            RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"the silence (?:was|hung|stretched|grew) (?:heavy|thick|oppressive|deafening)", 
            RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"let out a breath (?:he|she|they) didn't? (?:know|realize)", 
            RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"something (?:dark|ancient|primal|unnamed) stirred", 
            RegexOptions.Compiled | RegexOptions.IgnoreCase),
    ];

    private static readonly Regex[] StructuralAITics = [
        new(@"(?:I'm|I am) not (?:saying|asking|suggesting) .{3,40}(?:I'm|I am) (?:saying|asking|suggesting)", 
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline),
        new(@"(?:which|that) means either .{3,40} or ", 
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline),
        new(@"[Tt]here's? a (?:difference|distinction)\.", RegexOptions.Compiled),
        new(@"[Tt]hose are (?:different|not the same) things\.", RegexOptions.Compiled),
        new(@"[Nn]ot (?:just|merely|simply) .{3,40}, but ", 
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline),
        new(@"[Nn]ot (?:from|by|because of) .{3,40}, but (?:from|by|because)", 
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline),
    ];

    private static readonly Regex[] TellingPatterns = [
        new(@"\b(?:he|she|they|I|we|[A-Z]\w+) (?:felt|was|seemed|looked|appeared) 
            (?:angry|sad|happy|scared|nervous|excited|jealous|guilty|anxious|lonely|
            desperate|furious|terrified|elated|miserable|hopeful|confused|relieved|
            horrified|disgusted|ashamed|proud|bitter|defeated|triumphant)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace),
        new(@"\b(?:angrily|sadly|happily|nervously|excitedly|desperately|furiously|
            anxiously|guiltily|bitterly|wearily|miserably)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase),
    ];

    public SlopScore Calculate(string text)
    {
        var words = text.ToLower().Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).ToArray();
        var wordCount = words.Length;
        if (wordCount == 0) 
            return new SlopScore(new(), new(), 0, [], 0, 0, 0, 0, 0, 0, 0);

        var tier1Hits = new Dictionary<string, int>();
        foreach (var word in words)
        {
            var clean = word.Trim(".,;:!?\"'()".ToCharArray());
            if (Tier1Banned.Contains(clean) && clean.Length > 2)
            {
                tier1Hits[clean] = tier1Hits.GetValueOrDefault<string, int>(clean, 0) + 1;
            }
        }

        var paragraphs = text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToArray();
        
        int tier2ClusterCount = 0;
        var tier2Hits = new Dictionary<string, int>();
        
        foreach (var para in paragraphs)
        {
            var paraWords = para.ToLower().Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).ToArray();
            var hitsInPara = new HashSet<string>();
            
            foreach (var word in paraWords)
            {
                var clean = word.Trim(".,;:!?\"'()".ToCharArray());
                if (Tier2Suspicious.Contains(clean))
                {
                    hitsInPara.Add(clean);
                    tier2Hits[clean] = tier2Hits.GetValueOrDefault<string, int>(clean, 0) + 1;
                }
            }
            
            if (hitsInPara.Count >= 3)
                tier2ClusterCount++;
        }

        var tier3Hits = new List<(string Pattern, int Count)>();
        foreach (var pattern in Tier3FillerPatterns)
        {
            var matches = pattern.Matches(text);
            if (matches.Count > 0)
                tier3Hits.Add((pattern.ToString(), matches.Count));
        }

        var fictionAITellCount = FictionAITells.Sum(p => p.Matches(text).Count);
        var structuralTicCount = StructuralAITics.Sum(p => p.Matches(text).Count);
        var tellingCount = TellingPatterns.Sum(p => p.Matches(text).Count);

        var emDashCount = Regex.Matches(text, @"—|--").Count;
        var emDashDensity = (emDashCount * 1000.0) / wordCount;

        var sentences = Regex.Split(text, @"(?<=[.!?])\s+").Where(s => s.Trim().Length > 0);
        var wordLengths = sentences.Select(s => s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length).ToArray();
        var mean = wordLengths.Average();
        var variance = wordLengths.Average(w => Math.Pow(w - mean, 2));
        var stdDev = Math.Sqrt(variance);
        var sentenceLengthCV = mean > 0 ? stdDev / mean : 0;

        var transitionStarts = paragraphs.Count(p => 
        {
            var firstWord = p.Split(' ', 2)[0].ToLower().Trim(".,;:!?\"'()".ToCharArray());
            return TransitionOpeners.Contains(firstWord);
        });
        var transitionOpenerRatio = paragraphs.Length > 0 
            ? (double)transitionStarts / paragraphs.Length 
            : 0;

        double penalty = 0;
        penalty += Math.Min(4.0, tier1Hits.Values.Sum() * 1.5);
        penalty += Math.Min(2.0, tier2ClusterCount * 1.0);
        penalty += Math.Min(2.0, tier3Hits.Sum(h => h.Count) * 0.3);
        if (emDashDensity > 15)
            penalty += Math.Min((emDashDensity - 15) * 0.3, 1.0);
        if (sentenceLengthCV < 0.3)
            penalty += 1.0;
        if (transitionOpenerRatio > 0.3)
            penalty += Math.Min(transitionOpenerRatio * 2, 1.0);
        penalty += Math.Min(fictionAITellCount * 0.3, 2.0);
        penalty += Math.Min(tellingCount * 0.2, 1.5);
        penalty += Math.Min(structuralTicCount * 0.5, 2.0);

        penalty = Math.Min(penalty, 10.0);

        return new SlopScore(
            Tier1Hits: tier1Hits,
            Tier2Hits: tier2Hits,
            Tier2ClusterCount: tier2ClusterCount,
            Tier3Hits: tier3Hits,
            FictionAITellCount: fictionAITellCount,
            StructuralTicCount: structuralTicCount,
            TellingCount: tellingCount,
            EmDashDensity: Math.Round(emDashDensity, 2),
            SentenceLengthCV: Math.Round(sentenceLengthCV, 3),
            TransitionOpenerRatio: Math.Round(transitionOpenerRatio, 3),
            SlopPenalty: Math.Round(penalty, 2)
        );
    }
}