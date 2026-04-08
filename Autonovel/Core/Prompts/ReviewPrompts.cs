using System;
using System.Text;

namespace Autonovel.Core.Prompts;

public static class ReviewPrompts
{
    public static string BuildOpusReviewPrompt(string title, string manuscript)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Read the below novel, \"{title}\". Review it first as a literary critic (like a newspaper book review) and then as a professor of fiction. In the later review, give specific, actionable suggestions for any defects you find. Be fair but honest. You don't *have* to find defects.");
        sb.AppendLine();
        sb.AppendLine(manuscript);
        return sb.ToString();
    }
}
