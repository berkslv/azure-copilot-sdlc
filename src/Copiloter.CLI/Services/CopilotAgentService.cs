using Copiloter.CLI.Models;
using Copiloter.CLI.Services.Interfaces;
using Copiloter.CLI.Utilities;
using GitHub.Copilot.SDK;
using System.IO;

namespace Copiloter.CLI.Services;

/// <summary>
/// Service to execute Copilot agents via GitHub Copilot SDK
/// </summary>
public class CopilotAgentService(IMcpConfigurationService mcpService) : ICopilotAgentService
{
    /// <summary>
    /// Execute an agent with the given prompt
    /// </summary>
    /// <param name="agent">Agent configuration</param>
    /// <param name="systemPrompt">System prompt with task instructions</param>
    /// <param name="directory">Working directory</param>
    /// <param name="timeout">Timeout for agent execution (default 5 minutes)</param>
    /// <returns>Agent response</returns>
    public async Task<string> ExecuteAgentAsync(
        AgentConfig agent, 
        string systemPrompt,
        string directory,
        TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromMinutes(5);

        // Get environment variables (prompts for PATs if needed)
        var envVars = mcpService.GetEnvironmentVariables(directory);

        // Create MCP configurations
        var filesystemMcp = mcpService.CreateFilesystemMcpConfig(directory);
        var azureDevOpsMcp = mcpService.CreateAzureDevOpsMcpConfig(
            envVars["AZURE_DEVOPS_ORG"],
            envVars["AZURE_DEVOPS_PAT"]
        );

        // Build MCP servers dictionary
        var mcpServers = new Dictionary<string, object>
        {
            { "filesystem", filesystemMcp },
            { "azure-devops", azureDevOpsMcp }
        };

        // Build full system prompt combining base instructions and agent content
        var fullPrompt = BuildFullPrompt(systemPrompt, envVars, directory);

        // Create cancellation token with timeout
        using var cts = new CancellationTokenSource(timeout.Value);

        try
        {
            ConsoleHelper.ShowInfo("Connecting to Copilot server at localhost:4321...");
            await using var client = new CopilotClient(new CopilotClientOptions
            {
                CliUrl = "localhost:4321",
                UseStdio = false
            });

            // Create session with MCP servers and custom agent
            ConsoleHelper.ShowInfo("Initializing session with MCP servers...");
            await using var session = await client.CreateSessionAsync(new SessionConfig
            {
                McpServers = mcpServers,
                Streaming = false,
                CustomAgents = new List<CustomAgentConfig>
                {
                    new CustomAgentConfig
                    {
                        Name = agent.Name,
                        Prompt = agent.Content,
                    }
                }
            }, cts.Token);

            // Collect assistant messages
            var responseBuilder = new System.Text.StringBuilder();
            session.On(evt =>
            {
                if (evt is AssistantMessageEvent assistantMsg && assistantMsg.Data?.Content != null)
                {
                    responseBuilder.AppendLine(assistantMsg.Data.Content);
                    Console.WriteLine(assistantMsg.Data.Content);
                }
            });

            ConsoleHelper.ShowInfo("Sending request to agent...");

            // Send message and wait for completion
            await session.SendAndWaitAsync(new MessageOptions
            {
                Prompt = fullPrompt,
            });

            var response = responseBuilder.ToString();
            if (string.IsNullOrWhiteSpace(response))
            {
                ConsoleHelper.ShowWarning("Agent returned empty response");
                response = "No response received from agent";
            }

            return response;
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"Agent execution timed out after {timeout.Value.TotalMinutes} minutes");
        }
        catch (Exception ex)
        {
            ConsoleHelper.ShowError($"Failed to generate content: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Build full system prompt combining base instructions, agent content, and context
    /// </summary>
    private string BuildFullPrompt(string baseInstructions, Dictionary<string, string> envVars, string directory)
    {
        var prompt = $@"{baseInstructions}

Context:
- Working Directory: {directory}
- Organization: {envVars.GetValueOrDefault("AZURE_DEVOPS_ORG", "Not set")}

Available via MCP:
- Azure DevOps MCP: Query work items, PRs, comments, create/update work items
  - Use tools from @azure-devops/mcp server
- Filesystem MCP: Read/write project files in working directory
  - Use tools from @modelcontextprotocol/server-filesystem

Available via CLI (you can execute these):
- git: All git commands (branch, commit, push, etc.)
- dotnet: build, test, etc. (for .NET projects)
- Any other CLI tools needed for the project

IMPORTANT: Execute all tasks autonomously. Use the MCP servers and CLI tools to complete the task. Report progress and results clearly.
";

        return prompt;
    }
}
