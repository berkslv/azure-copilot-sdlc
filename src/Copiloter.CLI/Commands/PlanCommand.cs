using Copiloter.CLI.Models;
using Copiloter.CLI.Services;
using Copiloter.CLI.Utilities;
using System.Diagnostics;

namespace Copiloter.CLI.Commands;

/// <summary>
/// Command to generate AI plans for Azure DevOps work items
/// </summary>
public class PlanCommand
{
    private readonly string _workingDirectory;
    private readonly int _workItemId;
    private readonly bool _skipConfirmation;
    private readonly AgentDiscoveryService _agentDiscovery;
    private readonly McpConfigurationService _mcpConfig;
    private readonly CopilotAgentService _copilotAgent;

    public PlanCommand(string workingDirectory, int workItemId, bool skipConfirmation)
    {
        _workingDirectory = workingDirectory;
        _workItemId = workItemId;
        _skipConfirmation = skipConfirmation;

        _agentDiscovery = new AgentDiscoveryService(workingDirectory);
        _mcpConfig = new McpConfigurationService(workingDirectory);
        _copilotAgent = new CopilotAgentService(workingDirectory, _mcpConfig);
    }

    /// <summary>
    /// Execute the plan command
    /// </summary>
    public async Task<int> ExecuteAsync()
    {
        try
        {
            // Step 1: Validate prerequisites
            if (!ValidateGitRepository())
                return 1;

            if (!_mcpConfig.ValidateNpxAvailable())
            {
                ConsoleHelper.ShowError("npx is not available. Please install Node.js.");
                return 1;
            }

            // Step 2: Discover planner agent
            ConsoleHelper.ShowInfo("Discovering planner agent...");
            AgentConfig plannerAgent;
            try
            {
                plannerAgent = _agentDiscovery.DiscoverAgent("planner");
                ConsoleHelper.ShowSuccess($"Found planner agent: {plannerAgent.FilePath}");
            }
            catch (FileNotFoundException ex)
            {
                ConsoleHelper.ShowError(ex.Message);
                return 1;
            }

            // Step 3: Generate plan
            ConsoleHelper.ShowInfo($"Generating plan for work item #{_workItemId}...");
            
            var systemPrompt = BuildPlannerSystemPrompt();
            string planContent;

            try
            {
                planContent = await ConsoleHelper.WithProgress(
                    "Generating AI plan...",
                    async () => await _copilotAgent.ExecuteAgentWithRetryAsync(
                        plannerAgent,
                        systemPrompt,
                        maxRetries: 3
                    )
                );
            }
            catch (Exception ex)
            {
                ConsoleHelper.ShowError($"Failed to generate plan: {ex.Message}");
                return 1;
            }

            // Step 4: Validate plan structure
            var plan = PlanParser.Parse(planContent);
            
            if (!plan.IsValid)
            {
                var missing = PlanParser.GetMissingSections(plan);
                ConsoleHelper.ShowWarning($"Plan is missing required sections: {string.Join(", ", missing)}");
                
                if (!_skipConfirmation && !ConsoleHelper.PromptConfirm("Accept plan anyway?", false))
                {
                    ConsoleHelper.ShowInfo("Plan rejected.");
                    return 1;
                }
            }

            // Step 5: User verification (unless -y flag)
            if (!_skipConfirmation)
            {
                ConsoleHelper.ShowPanel("Generated Plan", plan.FullContent);
                
                var choice = ConsoleHelper.PromptChoice(
                    "What would you like to do?",
                    "(A)ccept",
                    "(R)eject",
                    "(E)dit"
                );

                switch (choice[1])  // Get second character (A, R, or E)
                {
                    case 'R':
                        var regenChoice = ConsoleHelper.PromptChoice(
                            "Would you like to:",
                            "(R)egenerate",
                            "(C)ancel"
                        );
                        
                        if (regenChoice[1] == 'C')
                        {
                            ConsoleHelper.ShowInfo("Operation cancelled.");
                            return 0;
                        }
                        
                        // TODO: Implement regeneration logic (max 3 attempts)
                        ConsoleHelper.ShowWarning("Regeneration not yet implemented.");
                        return 1;

                    case 'E':
                        planContent = await EditPlanAsync(plan.FullContent);
                        plan = PlanParser.Parse(planContent);
                        break;

                    case 'A':
                    default:
                        // Accept - continue
                        break;
                }
            }

            // Step 6: Save plan to work item
            ConsoleHelper.ShowInfo($"Saving plan to work item #{_workItemId}...");
            
            var savePrompt = BuildSavePlanPrompt(plan.FullContent);
            
            try
            {
                await ConsoleHelper.WithProgress(
                    "Saving plan to Azure DevOps...",
                    async () => await _copilotAgent.ExecuteAgentAsync(
                        plannerAgent,
                        savePrompt
                    )
                );

                ConsoleHelper.ShowSuccess($"Plan saved to work item #{_workItemId}");
                return 0;
            }
            catch (Exception ex)
            {
                ConsoleHelper.ShowError($"Failed to save plan: {ex.Message}");
                return 1;
            }
        }
        finally
        {
            await _copilotAgent.DisposeAsync();
        }
    }

    /// <summary>
    /// Validate that working directory is a git repository
    /// </summary>
    private bool ValidateGitRepository()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "rev-parse --git-dir",
                    WorkingDirectory = _workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                ConsoleHelper.ShowError("Directory is not a git repository. Please run this command from within a git repository or specify a valid git directory with -d.");
                return false;
            }

            return true;
        }
        catch
        {
            ConsoleHelper.ShowError("Failed to execute git command. Is git installed?");
            return false;
        }
    }

    /// <summary>
    /// Build system prompt for planner agent
    /// </summary>
    private string BuildPlannerSystemPrompt()
    {
        return $@"You are a technical planning assistant. Your task is to retrieve work item #{_workItemId} from Azure DevOps and create a detailed implementation plan.

Required Plan Structure:
1. # AI Plan (top-level header)
2. ## User Story - What the user wants, the story of the work item
3. ## Technical Implementation - Search project, find correct places for development, create abstract development plan
   - Include file paths and class names. Method signatures are helpful but not required.
   - Mid-level detail: architectural components, key classes/files to modify, new files to create, dependencies to add.
4. ## Acceptance Criteria - Detailed, testable criteria
   - Use testable/measurable criteria. Given-When-Then format is preferred but not required.
5. ## Test Paths - Manual testing steps to verify the requirement
   - Focus on manual testing steps. Automated test suggestions can be mentioned briefly.

Instructions:
1. Use Azure DevOps MCP to retrieve work item #{_workItemId}
2. Check work item state - if Completed/Closed/Removed, note it in your response
3. Use filesystem MCP to analyze the project structure
4. Create a comprehensive plan following the structure above
5. Keep plan under 4000 tokens (~3000 words)
6. Be specific and actionable

Return ONLY the plan content in markdown format.";
    }

    /// <summary>
    /// Build prompt to save plan to work item
    /// </summary>
    private string BuildSavePlanPrompt(string planContent)
    {
        return $@"Save the following AI-generated plan to Azure DevOps work item #{_workItemId}.

Instructions:
1. Use Azure DevOps MCP to check for existing '# AI PLAN' comment
2. If exists, update it. If not, create new comment
3. Prefix comment with '# AI PLAN' tag
4. Add timestamp: 'Generated on {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC'
5. Update work item state to 'Active' (or 'In Progress' or 'Committed' if 'Active' is not valid)
6. Do NOT change assigned user, iteration, or other fields

Plan Content:
{planContent}

Return a confirmation message when complete.";
    }

    /// <summary>
    /// Open plan in editor for user modifications
    /// </summary>
    private async Task<string> EditPlanAsync(string originalContent)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"plan-{_workItemId}.md");
        
        try
        {
            // Write plan to temp file
            await File.WriteAllTextAsync(tempFile, originalContent);

            // Get editor from environment or use default
            var editor = Environment.GetEnvironmentVariable("EDITOR") ?? "nano";

            // Open editor
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = editor,
                    Arguments = tempFile,
                    UseShellExecute = false
                }
            };

            process.Start();
            await process.WaitForExitAsync();

            // Read modified content
            return await File.ReadAllTextAsync(tempFile);
        }
        finally
        {
            // Cleanup temp file
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
