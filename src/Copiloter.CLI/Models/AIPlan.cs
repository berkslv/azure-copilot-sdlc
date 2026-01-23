namespace Copiloter.CLI.Models;

/// <summary>
/// Parsed AI-generated plan structure
/// </summary>
public class AIPlan
{
    /// <summary>
    /// Full content of the plan
    /// </summary>
    public required string FullContent { get; set; }

    /// <summary>
    /// User Story section content
    /// </summary>
    public string? UserStory { get; set; }

    /// <summary>
    /// Technical Implementation section content
    /// </summary>
    public string? TechnicalImplementation { get; set; }

    /// <summary>
    /// Acceptance Criteria section content
    /// </summary>
    public string? AcceptanceCriteria { get; set; }

    /// <summary>
    /// Test Paths section content
    /// </summary>
    public string? TestPaths { get; set; }

    /// <summary>
    /// Indicates if the plan has all required sections
    /// </summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(TechnicalImplementation) 
                           && !string.IsNullOrWhiteSpace(AcceptanceCriteria);
}
