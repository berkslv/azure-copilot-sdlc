using Copiloter.CLI.Models;
using Copiloter.CLI.Services;
using Copiloter.CLI.Utilities;

namespace Copiloter.CLI.Commands;

/// <summary>
/// Command to implement features based on AI plans
/// </summary>
public class DevelopCommand
{
    private readonly string _workingDirectory;
    private readonly int _workItemId;
    private readonly bool _withReview;
    private readonly AgentDiscoveryService _agentDiscovery;
    private readonly McpConfigurationService _mcpConfig;
    private readonly CopilotAgentService _copilotAgent;

    public DevelopCommand(string workingDirectory, int workItemId, bool withReview)
    {
        _workingDirectory = workingDirectory;
        _workItemId = workItemId;
        _withReview = withReview;

        _agentDiscovery = new AgentDiscoveryService(workingDirectory);
        _mcpConfig = new McpConfigurationService(workingDirectory);
        _copilotAgent = new CopilotAgentService(workingDirectory, _mcpConfig);
    }

    /// <summary>
    /// Execute the develop command
    /// </summary>
    public async Task<int> ExecuteAsync()
    {
        try
        {
            // Step 1: Discover developer agent
            ConsoleHelper.ShowInfo("Discovering developer agent...");
            AgentConfig developerAgent;
            try
            {
                developerAgent = _agentDiscovery.DiscoverAgent("developer");
                ConsoleHelper.ShowSuccess($"Found developer agent: {developerAgent.FilePath}");
            }
            catch (FileNotFoundException ex)
            {
                ConsoleHelper.ShowError(ex.Message);
                return 1;
            }

            // Step 2: Execute development
            ConsoleHelper.ShowInfo($"Starting development for work item #{_workItemId}...");
            
            var systemPrompt = BuildDeveloperSystemPrompt();

            try
            {
                var result = await ConsoleHelper.WithProgress(
                    "Implementing feature...",
                    async () => await _copilotAgent.ExecuteAgentAsync(
                        developerAgent,
                        systemPrompt,
                        timeout: TimeSpan.FromMinutes(30) // Longer timeout for development
                    )
                );

                ConsoleHelper.ShowSuccess("Development completed!");
                Console.WriteLine(result);
            }
            catch (Exception ex)
            {
                ConsoleHelper.ShowError($"Development failed: {ex.Message}");
                return 1;
            }

            // Step 3: Optional review stage
            if (_withReview)
            {
                ConsoleHelper.ShowInfo("Proceeding to review stage...");
                
                try
                {
                    await ConsoleHelper.ShowCountdown("Starting review stage", 5, CancellationToken.None);
                }
                catch (OperationCanceledException)
                {
                    ConsoleHelper.ShowInfo("Review cancelled by user.");
                    return 0;
                }

                var reviewCommand = new ReviewCommand(_workingDirectory, _workItemId);
                return await reviewCommand.ExecuteAsync();
            }

            return 0;
        }
        finally
        {
            await _copilotAgent.DisposeAsync();
        }
    }

    /// <summary>
    /// Build system prompt for developer agent
    /// </summary>
    private string BuildDeveloperSystemPrompt()
    {
        return $@"You are an expert developer. Your task is to implement work item #{_workItemId} following the AI plan.

Instructions:
1. Use Azure DevOps MCP to retrieve work item #{_workItemId} and its plan (look for '# AI PLAN' comment)
2. Validate that plan exists and has 'Technical Implementation' and 'Acceptance Criteria' sections
3. If no plan found, report error and exit
4. Use git CLI to:
   - Check for uncommitted changes (git status --porcelain). If found, report error and exit.
   - Fetch latest from origin: git fetch origin
   - Checkout main/master: git checkout main (or master)
   - Pull latest: git pull origin main
   - Create feature branch: git checkout -b feature/{_workItemId}
5. Use filesystem MCP to implement the feature according to Technical Implementation
6. Use CLI to build: dotnet build (or appropriate command)
7. Use CLI to test: dotnet test (or appropriate command)
8. If build/test fails, analyze errors and fix (max 3 retry attempts)
9. Generate unit tests based on Acceptance Criteria
10. Once all tests pass:
    - Stage changes: git add .
    - Commit: git commit -m ""feat: #{_workItemId} <work-item-title>""
    - Push: git push origin feature/{_workItemId}
11. Use Azure DevOps MCP to create Pull Request:
    - Title: #{_workItemId} <work-item-title>
    - Description: Include the AI plan content
    - Link PR to work item

Progress Reporting:
- Report each major step as you complete it
- Show build/test results
- Report any errors encountered and how you resolved them

Return a summary of what was accomplished.";
    }
}
