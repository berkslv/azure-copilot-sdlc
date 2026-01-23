using Copiloter.CLI.Models;
using Copiloter.CLI.Services.Interfaces;
using Copiloter.CLI.Utilities;
using System.Collections.Concurrent;

namespace Copiloter.CLI.Services;

/// <summary>
/// Service to discover and load custom Copilot agent markdown files
/// </summary>
public class AgentDiscoveryService : IAgentDiscoveryService
{
    private readonly ConcurrentDictionary<string, AgentConfig> _cachedAgents = new();

    // Search paths in priority order
    private readonly string[] _searchPaths = new[]
    {
        ".github/agents",
        "agents",
        "docs/agents",
        "."
    };

    /// <summary>
    /// Discover and load an agent by name
    /// </summary>
    /// <param name="agentName">Name of the agent (e.g., "planner", "developer", "reviewer")</param>
    /// <returns>Agent configuration</returns>
    /// <exception cref="FileNotFoundException">If agent file is not found</exception>
    public AgentConfig DiscoverAgent(string agentName, string directory)
    {
        // Check cache first
        if (_cachedAgents.TryGetValue(agentName, out var cached))
        {
            return cached;
        }

        var fileName = $"{agentName}.agent.md";
        var foundPaths = new List<string>();

        // Search in priority order
        foreach (var searchPath in _searchPaths)
        {
            var fullPath = Path.Combine(directory, searchPath, fileName);
            
            if (File.Exists(fullPath))
            {
                foundPaths.Add(fullPath);
            }
        }

        if (foundPaths.Count == 0)
        {
            throw new FileNotFoundException(
                $"Agent '{agentName}' not found. To get the best results, first create a specialized agent for the tasks. Searched in: {string.Join(", ", _searchPaths.Select(p => Path.Combine(directory, p)))}",
                fileName
            );
        }

        // Use first match
        var selectedPath = foundPaths[0];

        // Warn if multiple matches found
        if (foundPaths.Count > 1)
        {
            ConsoleHelper.ShowWarning($"Multiple '{fileName}' files found. Using: {selectedPath}");
        }

        // Load agent content
        var content = File.ReadAllText(selectedPath);
        
        var config = new AgentConfig
        {
            Name = agentName,
            FilePath = selectedPath,
            Content = content
        };

        // Cache for future use
        _cachedAgents[agentName] = config;

        return config;
    }

    /// <summary>
    /// Check if an agent exists without loading it
    /// </summary>
    public bool AgentExists(string agentName, string directory)
    {
        try
        {
            DiscoverAgent(agentName, directory);
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// Get all available agent names in the working directory
    /// </summary>
    public List<string> GetAvailableAgents(string directory)
    {
        var agents = new HashSet<string>();

        foreach (var searchPath in _searchPaths)
        {
            var fullPath = Path.Combine(directory, searchPath);
            
            if (!Directory.Exists(fullPath))
                continue;

            var agentFiles = Directory.GetFiles(fullPath, "*.agent.md");
            
            foreach (var file in agentFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (fileName.EndsWith(".agent"))
                {
                    agents.Add(fileName.Substring(0, fileName.Length - 6)); // Remove ".agent"
                }
            }
        }

        return agents.ToList();
    }
}
