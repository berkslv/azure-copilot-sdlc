using Copiloter.CLI.Models;
using Copiloter.CLI.Utilities;

namespace Copiloter.CLI.Services;

/// <summary>
/// Service to discover and load custom Copilot agent markdown files
/// </summary>
public class AgentDiscoveryService
{
    private readonly string _workingDirectory;
    private readonly Dictionary<string, AgentConfig> _cachedAgents = new();

    // Search paths in priority order
    private readonly string[] _searchPaths = new[]
    {
        ".github/agents",
        "agents",
        "docs/agents",
        "."
    };

    public AgentDiscoveryService(string workingDirectory)
    {
        _workingDirectory = workingDirectory;
    }

    /// <summary>
    /// Discover and load an agent by name
    /// </summary>
    /// <param name="agentName">Name of the agent (e.g., "planner", "developer", "reviewer")</param>
    /// <returns>Agent configuration</returns>
    /// <exception cref="FileNotFoundException">If agent file is not found</exception>
    public AgentConfig DiscoverAgent(string agentName)
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
            var fullPath = Path.Combine(_workingDirectory, searchPath, fileName);
            
            if (File.Exists(fullPath))
            {
                foundPaths.Add(fullPath);
            }
        }

        if (foundPaths.Count == 0)
        {
            throw new FileNotFoundException(
                $"Agent '{agentName}' not found. Searched in: {string.Join(", ", _searchPaths.Select(p => Path.Combine(_workingDirectory, p)))}",
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
    public bool AgentExists(string agentName)
    {
        try
        {
            DiscoverAgent(agentName);
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
    public List<string> GetAvailableAgents()
    {
        var agents = new HashSet<string>();

        foreach (var searchPath in _searchPaths)
        {
            var fullPath = Path.Combine(_workingDirectory, searchPath);
            
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
