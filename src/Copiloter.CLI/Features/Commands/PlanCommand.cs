using Copiloter.CLI.Models;
using Copiloter.CLI.Services.Interfaces;
using Copiloter.CLI.Utilities;
using MediatR;
using System.Diagnostics;

namespace Copiloter.CLI.Features.Commands;

/// <summary>
/// MediatR request for executing the Plan command
/// </summary>
public class PlanCommand : IWorkItemRequest<int>
{
    public int WorkItemId { get; set; }
    public string WorkingDirectory { get; set; } = Directory.GetCurrentDirectory();
    public bool SkipConfirmation { get; set; }
}


/// <summary>
/// Handler for executing the Plan command
/// </summary>
public class PlanCommandHandler(
    IAgentDiscoveryService agentDiscovery,
    ICopilotAgentService copilotAgent): IRequestHandler<PlanCommand, int>
{

    public async Task<int> Handle(PlanCommand request, CancellationToken cancellationToken)
    {
        // Step 2: Discover planner agent
        ConsoleHelper.ShowInfo("Discovering planner agent...");
        AgentConfig plannerAgent;
        try
        {
            plannerAgent = agentDiscovery.DiscoverAgent("planner", request.WorkingDirectory);
            ConsoleHelper.ShowSuccess($"Found planner agent: {plannerAgent.FilePath}");
        }
        catch (FileNotFoundException ex)
        {
            ConsoleHelper.ShowError(ex.Message);
            return 1;
        }

        // Step 3: Generate plan
        ConsoleHelper.ShowInfo($"Generating plan for work item #{request.WorkItemId}...");

        var systemPrompt = BuildPlannerSystemPrompt(request.WorkItemId);
        string planContent;

        try
        {
            planContent = await ConsoleHelper.WithProgress(
                "Generating AI plan...",
                async () => await copilotAgent.ExecuteAgentAsync(
                    plannerAgent,
                    systemPrompt,
                    request.WorkingDirectory,
                    TimeSpan.FromMinutes(5)
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

            if (!request.SkipConfirmation && !ConsoleHelper.PromptConfirm("Accept plan anyway?", false))
            {
                ConsoleHelper.ShowInfo("Plan rejected.");
                return 1;
            }
        }

        // Step 5: User verification (unless -y flag)
        if (!request.SkipConfirmation)
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
                    planContent = await EditPlanAsync(plan.FullContent, request.WorkItemId);
                    plan = PlanParser.Parse(planContent);
                    break;

                case 'A':
                default:
                    // Accept - continue
                    break;
            }
        }

        // Step 6: Save plan to work item
        ConsoleHelper.ShowInfo($"Saving plan to work item #{request.WorkItemId}...");

        var savePrompt = BuildSavePlanPrompt(request.WorkItemId, plan.FullContent);

        try
        {
            await ConsoleHelper.WithProgress(
                "Saving plan to Azure DevOps...",
                async () => await copilotAgent.ExecuteAgentAsync(
                    plannerAgent,
                    savePrompt,
                    request.WorkingDirectory,
                    TimeSpan.FromMinutes(5)
                )
            );

            ConsoleHelper.ShowSuccess($"Plan saved to work item #{request.WorkItemId}");
            return 0;
        }
        catch (Exception ex)
        {
            ConsoleHelper.ShowError($"Failed to save plan: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Build system prompt for planner agent
    /// </summary>
    private string BuildPlannerSystemPrompt(int workItemId)
    {
        return $@"You are a technical planning assistant. Your task is to retrieve work item #{workItemId} from Azure DevOps and create a detailed implementation plan.

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
1. Use Azure DevOps MCP to retrieve work item #{workItemId}
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
    private string BuildSavePlanPrompt(int workItemId, string planContent)
    {
        return $@"Save the following AI-generated plan to Azure DevOps work item #{workItemId}.

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
    private async Task<string> EditPlanAsync(string originalContent, int workItemId)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"plan-{workItemId}.md");

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
