using GitHub.Copilot.SDK;

namespace Copiloter.CLI.Services.Interfaces;

/// <summary>
/// Interface for configuring Model Context Protocol (MCP) servers
/// </summary>
public interface IMcpConfigurationService
{
    /// <summary>
    /// Get environment variables configuration for MCP
    /// Prompts for missing PATs and sets them in current session
    /// </summary>
    Dictionary<string, string> GetEnvironmentVariables(string directory);

    /// <summary>
    /// Create filesystem MCP configuration
    /// </summary>
    McpLocalServerConfig CreateFilesystemMcpConfig(string directory);

    /// <summary>
    /// Create Azure DevOps MCP configuration
    /// </summary>
    McpLocalServerConfig CreateAzureDevOpsMcpConfig(string organization, string pat);

    /// <summary>
    /// Validate that npx is available
    /// </summary>
    bool ValidateNpxAvailable();

    /// <summary>
    /// Extract Azure DevOps project from git remote URL
    /// </summary>
    string? ExtractProjectFromGitRemote(string directory);
}
