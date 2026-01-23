using System.Text.Json.Serialization;

namespace Copiloter.CLI.Models;

/// <summary>
/// Configuration model for .azure-copilot file
/// </summary>
public class CopilotConfig
{
    [JsonPropertyName("azureDevOpsPat")]
    public string AzureDevOpsPat { get; set; } = string.Empty;

    [JsonPropertyName("githubPat")]
    public string GitHubPat { get; set; } = string.Empty;

    [JsonPropertyName("azureDevOpsOrg")]
    public string AzureDevOpsOrg { get; set; } = string.Empty;
}
