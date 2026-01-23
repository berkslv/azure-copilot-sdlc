using Copiloter.CLI.Models;

namespace Copiloter.CLI.Services.Interfaces;

/// <summary>
/// Interface for discovering and loading custom Copilot agents
/// </summary>
public interface IAgentDiscoveryService
{
    /// <summary>
    /// Discover and load an agent by name
    /// </summary>
    /// <param name="agentName">Name of the agent (e.g., "planner", "developer", "reviewer")</param>
    /// <returns>Agent configuration</returns>
    /// <exception cref="FileNotFoundException">If agent file is not found</exception>
    AgentConfig DiscoverAgent(string agentName, string directory);
}
