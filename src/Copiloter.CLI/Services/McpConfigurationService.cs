using Copiloter.CLI.Services.Interfaces;
using Copiloter.CLI.Utilities;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Copiloter.CLI.Services;

/// <summary>
/// Service to configure Model Context Protocol (MCP) servers
/// </summary>
public class McpConfigurationService : IMcpConfigurationService
{
    private readonly ConcurrentDictionary<string, string> _cachedEnvironmentVariables;

    public McpConfigurationService()
    {
        _cachedEnvironmentVariables = new ConcurrentDictionary<string, string>();
    }

    /// <summary>
    /// Get environment variables configuration for MCP
    /// Prompts for missing PATs and sets them in current session
    /// Uses cached values if already retrieved
    /// </summary>
    public Dictionary<string, string> GetEnvironmentVariables(string directory)
    {
        // Check if we have cached variables already
        if (_cachedEnvironmentVariables.Count > 0)
        {
            return new Dictionary<string, string>(_cachedEnvironmentVariables);
        }

        var envVars = new Dictionary<string, string>();

        // Get or prompt for Azure DevOps PAT
        var azureDevOpsPat = Environment.GetEnvironmentVariable("AZURE_DEVOPS_PAT", EnvironmentVariableTarget.User);
        if (string.IsNullOrWhiteSpace(azureDevOpsPat))
        {
            azureDevOpsPat = ConsoleHelper.PromptSecret("Azure DevOps PAT not found. Please enter your PAT:");
            Environment.SetEnvironmentVariable("AZURE_DEVOPS_PAT", azureDevOpsPat, EnvironmentVariableTarget.User);
            ConsoleHelper.ShowSuccess("PAT stored for user.");
            ConsoleHelper.ShowInfo("To persist across sessions, add to your shell profile (~/.zshrc or ~/.bashrc): export AZURE_DEVOPS_PAT=\"your-pat\"");
        }
        envVars["AZURE_DEVOPS_PAT"] = azureDevOpsPat;
        _cachedEnvironmentVariables.TryAdd("AZURE_DEVOPS_PAT", azureDevOpsPat);

        // Get or prompt for GitHub PAT
        var githubPat = Environment.GetEnvironmentVariable("GITHUB_PAT", EnvironmentVariableTarget.User);
        if (string.IsNullOrWhiteSpace(githubPat))
        {
            githubPat = ConsoleHelper.PromptSecret("GitHub PAT not found. Please enter your PAT:");
            Environment.SetEnvironmentVariable("GITHUB_PAT", githubPat, EnvironmentVariableTarget.User);
            ConsoleHelper.ShowSuccess("PAT stored for user.");
            ConsoleHelper.ShowInfo("To persist across sessions, add to your shell profile (~/.zshrc or ~/.bashrc): export GITHUB_PAT=\"your-pat\"");
        }
        envVars["GITHUB_PAT"] = githubPat;
        _cachedEnvironmentVariables.TryAdd("GITHUB_PAT", githubPat);

        // Get or auto-detect Azure DevOps organization
        var org = Environment.GetEnvironmentVariable("AZURE_DEVOPS_ORG");
        if (string.IsNullOrWhiteSpace(org))
        {
            org = ExtractOrgFromGitRemote(directory);
            if (!string.IsNullOrWhiteSpace(org))
            {
                ConsoleHelper.ShowInfo($"Auto-detected Azure DevOps organization: {org}");
                Environment.SetEnvironmentVariable("AZURE_DEVOPS_ORG", org);
            }
        }
        
        if (!string.IsNullOrWhiteSpace(org))
        {
            envVars["AZURE_DEVOPS_ORG"] = org;
            _cachedEnvironmentVariables.TryAdd("AZURE_DEVOPS_ORG", org);
        }

        return envVars;
    }

    /// <summary>
    /// Create filesystem MCP configuration
    /// </summary>
    public object CreateFilesystemMcpConfig(string directory)
    {
        return new
        {
            command = "npx",
            args = new[]
            {
                "-y",
                "@modelcontextprotocol/server-filesystem",
                directory
            }
        };
    }

    /// <summary>
    /// Create Azure DevOps MCP configuration
    /// </summary>
    public object CreateAzureDevOpsMcpConfig(string organization, string pat)
    {
        return new
        {
            command = "npx",
            args = new[]
            {
                "-y",
                "@azure-devops/mcp",
                organization,
                "--authentication",
                "envvar"
            },
            env = new Dictionary<string, string>
            {
                { "ADO_MCP_AUTH_TOKEN", pat }
            }
        };
    }

    /// <summary>
    /// Validate that npx is available
    /// </summary>
    public bool ValidateNpxAvailable()
    {
        try
        {
            // On Windows, we need to use shell execute to find npx (Node.js command)
            // On Unix-like systems, the PATH will find it
            var isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows);
            
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = isWindows ? "cmd.exe" : "npx",
                    Arguments = isWindows ? "/c npx --version" : "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit();

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Extract Azure DevOps organization from git remote URL
    /// </summary>
    private string? ExtractOrgFromGitRemote(string directory)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "remote get-url origin",
                    WorkingDirectory = directory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
                return null;

            // Match patterns like:
            // https://dev.azure.com/{org}/{project}/_git/{repo}
            // git@ssh.dev.azure.com:v3/{org}/{project}/{repo}
            var patterns = new[]
            {
                @"dev\.azure\.com/([^/]+)",
                @"ssh\.dev\.azure\.com:v3/([^/]+)"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(output, pattern);
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extract Azure DevOps project from git remote URL
    /// </summary>
    public string? ExtractProjectFromGitRemote(string directory)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "remote get-url origin",
                    WorkingDirectory = directory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
                return null;

            // Match patterns like:
            // https://dev.azure.com/{org}/{project}/_git/{repo}
            // git@ssh.dev.azure.com:v3/{org}/{project}/{repo}
            var patterns = new[]
            {
                @"dev\.azure\.com/[^/]+/([^/]+)",
                @"ssh\.dev\.azure\.com:v3/[^/]+/([^/]+)"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(output, pattern);
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
