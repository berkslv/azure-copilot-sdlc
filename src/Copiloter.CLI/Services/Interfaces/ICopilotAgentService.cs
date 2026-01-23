using Copiloter.CLI.Models;

namespace Copiloter.CLI.Services.Interfaces;

/// <summary>
/// Interface for executing Copilot agents via GitHub Copilot SDK
/// </summary>
public interface ICopilotAgentService
{
    /// <summary>
    /// Execute an agent with the given prompt
    /// </summary>
    /// <param name="agent">Agent configuration</param>
    /// <param name="systemPrompt">System prompt with task instructions</param>
    /// <param name="timeout">Timeout for agent execution (default 5 minutes)</param>
    /// <returns>Agent response</returns>
    Task<string> ExecuteAgentAsync(
        AgentConfig agent, 
        string systemPrompt,
        string directory,
        TimeSpan? timeout = null);
}
