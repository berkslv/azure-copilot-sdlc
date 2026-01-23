using GitHub.Copilot.SDK;
using Copiloter.CLI.Models;

namespace Copiloter.CLI.Services;

/// <summary>
/// Service to execute Copilot agents via GitHub Copilot SDK
/// </summary>
public class CopilotAgentService
{
    private readonly string _workingDirectory;
    private readonly McpConfigurationService _mcpService;
    private CopilotClient? _client;

    public CopilotAgentService(string workingDirectory, McpConfigurationService mcpService)
    {
        _workingDirectory = workingDirectory;
        _mcpService = mcpService;
    }

    /// <summary>
    /// Initialize the Copilot client
    /// </summary>
    private async Task<CopilotClient> GetOrCreateClientAsync()
    {
        if (_client == null)
        {
            _client = new CopilotClient(new CopilotClientOptions
            {
                Cwd = _workingDirectory,
                LogLevel = "info"
            });

            await _client.StartAsync();
        }

        return _client;
    }

    /// <summary>
    /// Execute an agent with the given prompt
    /// </summary>
    /// <param name="agent">Agent configuration</param>
    /// <param name="systemPrompt">System prompt with task instructions</param>
    /// <param name="timeout">Timeout for agent execution (default 5 minutes)</param>
    /// <returns>Agent response</returns>
    public async Task<string> ExecuteAgentAsync(
        AgentConfig agent, 
        string systemPrompt, 
        TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromMinutes(5);

        // Get environment variables (prompts for PATs if needed)
        var envVars = _mcpService.GetEnvironmentVariables();

        // Create MCP configurations
        var filesystemMcp = _mcpService.CreateFilesystemMcpConfig();
        var azureDevOpsMcp = _mcpService.CreateAzureDevOpsMcpConfig(
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
        var fullPrompt = BuildFullPrompt(systemPrompt, agent.Content, envVars);

        // Create cancellation token with timeout
        using var cts = new CancellationTokenSource(timeout.Value);

        try
        {
            var client = await GetOrCreateClientAsync();

            // Create session with MCP servers
            var session = await client.CreateSessionAsync(new SessionConfig
            {
                McpServers = mcpServers,
                Streaming = false
            }, cts.Token);

            // Collect assistant messages
            var responseBuilder = new System.Text.StringBuilder();
            session.On(evt =>
            {
                if (evt is AssistantMessageEvent assistantMsg && assistantMsg.Data?.Content != null)
                {
                    responseBuilder.AppendLine(assistantMsg.Data.Content);
                }
            });

            // Send message and wait for completion
            await session.SendAndWaitAsync(new MessageOptions
            {
                Prompt = fullPrompt
            }, timeout, cts.Token);

            // Cleanup session
            await session.DisposeAsync();

            return responseBuilder.ToString();
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"Agent execution timed out after {timeout.Value.TotalMinutes} minutes");
        }
    }

    /// <summary>
    /// Build full system prompt combining base instructions, agent content, and context
    /// </summary>
    private string BuildFullPrompt(string baseInstructions, string agentContent, Dictionary<string, string> envVars)
    {
        var prompt = $@"{baseInstructions}

Context:
- Working Directory: {_workingDirectory}
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

{agentContent}

IMPORTANT: Execute all tasks autonomously. Use the MCP servers and CLI tools to complete the task. Report progress and results clearly.
";

        return prompt;
    }

    /// <summary>
    /// Execute agent with retry logic
    /// </summary>
    public async Task<string> ExecuteAgentWithRetryAsync(
        AgentConfig agent,
        string systemPrompt,
        int maxRetries = 3,
        TimeSpan? timeout = null)
    {
        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await ExecuteAgentAsync(agent, systemPrompt, timeout);
            }
            catch (Exception ex)
            {
                lastException = ex;
                
                if (attempt < maxRetries)
                {
                    // Exponential backoff
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    await Task.Delay(delay);
                }
            }
        }

        throw new Exception($"Agent execution failed after {maxRetries} attempts", lastException);
    }

    /// <summary>
    /// Dispose the client
    /// </summary>
    public async Task DisposeAsync()
    {
        if (_client != null)
        {
            await _client.StopAsync();
            await _client.DisposeAsync();
            _client = null;
        }
    }
}
