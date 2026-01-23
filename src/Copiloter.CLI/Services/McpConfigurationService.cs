using Copiloter.CLI.Models;
using Copiloter.CLI.Services.Interfaces;
using Copiloter.CLI.Utilities;
using GitHub.Copilot.SDK;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Copiloter.CLI.Services;

/// <summary>
/// Service to configure Model Context Protocol (MCP) servers
/// </summary>
public class McpConfigurationService : IMcpConfigurationService
{
    private readonly ConcurrentDictionary<string, string> _cachedEnvironmentVariables;
    private const string ConfigFileName = ".azure-copilot";
    private CopilotConfig? _loadedConfig;

    public McpConfigurationService()
    {
        _cachedEnvironmentVariables = new ConcurrentDictionary<string, string>();
    }

    /// <summary>
    /// Get the config file path (platform-agnostic)
    /// Checks in this order: current directory, user home directory
    /// </summary>
    private string GetConfigFilePath()
    {
        // First check current directory
        var currentDirConfig = Path.Combine(Directory.GetCurrentDirectory(), ConfigFileName);
        if (File.Exists(currentDirConfig))
        {
            return currentDirConfig;
        }

        // Then check user home directory
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(homeDir, ConfigFileName);
    }

    /// <summary>
    /// Load configuration from .azure-copilot file
    /// </summary>
    private CopilotConfig? LoadConfig()
    {
        if (_loadedConfig != null)
        {
            return _loadedConfig;
        }

        var configPath = GetConfigFilePath();
        
        if (!File.Exists(configPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(configPath);
            _loadedConfig = JsonSerializer.Deserialize<CopilotConfig>(json);
            return _loadedConfig;
        }
        catch (Exception ex)
        {
            ConsoleHelper.ShowError($"Failed to load config from {configPath}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Save configuration to .azure-copilot file in user home directory
    /// </summary>
    private void SaveConfig(CopilotConfig config)
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var configPath = Path.Combine(homeDir, ConfigFileName);

        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            var json = JsonSerializer.Serialize(config, options);
            File.WriteAllText(configPath, json);
            _loadedConfig = config;
            
            ConsoleHelper.ShowSuccess($"Configuration saved to {configPath}");
        }
        catch (Exception ex)
        {
            ConsoleHelper.ShowError($"Failed to save config to {configPath}: {ex.Message}");
        }
    }

    /// <summary>
    /// Get environment variables configuration for MCP
    /// Prompts for missing PATs and saves them to .azure-copilot file
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
        var config = LoadConfig();
        var configNeedsSaving = false;

        // Initialize config if it doesn't exist
        if (config == null)
        {
            config = new CopilotConfig();
            configNeedsSaving = true;
            ConsoleHelper.ShowInfo("No configuration file found. Let's set up your credentials.");
        }

        // Get or prompt for Azure DevOps PAT
        var azureDevOpsPat = config.AzureDevOpsPat;
        if (string.IsNullOrWhiteSpace(azureDevOpsPat))
        {
            azureDevOpsPat = ConsoleHelper.PromptSecret("Azure DevOps PAT not found. Please enter your PAT:");
            config.AzureDevOpsPat = azureDevOpsPat;
            configNeedsSaving = true;
        }
        envVars["AZURE_DEVOPS_PAT"] = azureDevOpsPat;
        _cachedEnvironmentVariables.TryAdd("AZURE_DEVOPS_PAT", azureDevOpsPat);

        // Get or prompt for GitHub PAT
        var githubPat = config.GitHubPat;
        if (string.IsNullOrWhiteSpace(githubPat))
        {
            githubPat = ConsoleHelper.PromptSecret("GitHub PAT not found. Please enter your PAT:");
            config.GitHubPat = githubPat;
            configNeedsSaving = true;
        }
        envVars["GITHUB_PAT"] = githubPat;
        _cachedEnvironmentVariables.TryAdd("GITHUB_PAT", githubPat);

        // Get or auto-detect Azure DevOps organization
        var org = config.AzureDevOpsOrg;
        if (string.IsNullOrWhiteSpace(org))
        {
            org = ExtractOrgFromGitRemote(directory);
            if (!string.IsNullOrWhiteSpace(org))
            {
                ConsoleHelper.ShowInfo($"Auto-detected Azure DevOps organization: {org}");
                config.AzureDevOpsOrg = org;
                configNeedsSaving = true;
            }
            else
            {
                org = ConsoleHelper.PromptSecret("Azure DevOps Organization not found. Please enter your Organization:");
                config.AzureDevOpsOrg = org;
                configNeedsSaving = true;
            }
        }
        envVars["AZURE_DEVOPS_ORG"] = org;
        _cachedEnvironmentVariables.TryAdd("AZURE_DEVOPS_ORG", org);

        // Save config if any values were added or updated
        if (configNeedsSaving)
        {
            SaveConfig(config);
        }

        return envVars;
    }

    /// <summary>
    /// Create filesystem MCP configuration
    /// </summary>
    public McpLocalServerConfig CreateFilesystemMcpConfig(string directory)
    {
        return new McpLocalServerConfig
        {
            Command = "npx",
            Args = new List<string>
            {
                "-y",
                "@modelcontextprotocol/server-filesystem",
                directory
            },
            Tools = ["*"]
        };
    }

    /// <summary>
    /// Create Azure DevOps MCP configuration
    /// </summary>
    public McpLocalServerConfig CreateAzureDevOpsMcpConfig(string organization, string pat)
    {
        return new McpLocalServerConfig
        {
            Command = "npx",
            Args = new List<string>
            {
                "-y",
                "@azure-devops/mcp",
                organization,
                "-d", "core", "work", "work-items",
                "--authentication",
                "envvar"
            },
            Env = new Dictionary<string, string>
            {
                { "ADO_MCP_AUTH_TOKEN", pat }
            },
            Tools = ["*"]
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
