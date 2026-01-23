namespace Copiloter.CLI.Models;

/// <summary>
/// Configuration for a custom Copilot agent
/// </summary>
public class AgentConfig
{
    /// <summary>
    /// Name of the agent (e.g., "planner", "developer", "reviewer")
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Full path to the agent markdown file
    /// </summary>
    public required string FilePath { get; set; }

    /// <summary>
    /// Content of the agent markdown file
    /// </summary>
    public required string Content { get; set; }

    /// <summary>
    /// Timestamp when the agent was loaded
    /// </summary>
    public DateTime LoadedAt { get; set; } = DateTime.UtcNow;
}
