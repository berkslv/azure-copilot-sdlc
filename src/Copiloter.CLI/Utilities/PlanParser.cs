using System.Text.RegularExpressions;
using Copiloter.CLI.Models;

namespace Copiloter.CLI.Utilities;

/// <summary>
/// Parser for AI-generated plan markdown
/// </summary>
public static class PlanParser
{
    /// <summary>
    /// Parse a markdown plan into structured sections
    /// </summary>
    public static AIPlan Parse(string markdown)
    {
        var plan = new AIPlan
        {
            FullContent = markdown
        };

        // Extract sections using fuzzy header matching (case-insensitive)
        plan.UserStory = ExtractSection(markdown, "User Story");
        plan.TechnicalImplementation = ExtractSection(markdown, "Technical Implementation");
        plan.AcceptanceCriteria = ExtractSection(markdown, "Acceptance Criteria");
        plan.TestPaths = ExtractSection(markdown, "Test Paths");

        return plan;
    }

    /// <summary>
    /// Extract a section from markdown by fuzzy matching the header
    /// </summary>
    private static string? ExtractSection(string markdown, string sectionName)
    {
        // Fuzzy match: case-insensitive, allows variations in wording
        // Matches headers like "## User Story", "# User Story Plan", etc.
        var pattern = $@"#+\s*{Regex.Escape(sectionName)}[^\n]*\n(.*?)(?=\n#+\s|\z)";
        var match = Regex.Match(markdown, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        return null;
    }

    /// <summary>
    /// Validate that a plan has required sections
    /// </summary>
    public static bool HasRequiredSections(AIPlan plan)
    {
        return plan.IsValid;
    }

    /// <summary>
    /// Get missing required sections
    /// </summary>
    public static List<string> GetMissingSections(AIPlan plan)
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(plan.TechnicalImplementation))
            missing.Add("Technical Implementation");

        if (string.IsNullOrWhiteSpace(plan.AcceptanceCriteria))
            missing.Add("Acceptance Criteria");

        return missing;
    }
}
