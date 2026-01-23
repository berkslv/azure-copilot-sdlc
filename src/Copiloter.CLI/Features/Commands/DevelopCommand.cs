using Copiloter.CLI.Models;
using Copiloter.CLI.Services.Interfaces;
using Copiloter.CLI.Utilities;
using MediatR;

namespace Copiloter.CLI.Features.Commands;

/// <summary>
/// MediatR request for executing the Develop command
/// </summary>
public class DevelopCommand : IWorkItemRequest<int>
{
    public int WorkItemId { get; set; }
    public string WorkingDirectory { get; set; } = Directory.GetCurrentDirectory();
    public bool WithReview { get; set; }
}

/// <summary>
/// Handler for executing the Develop command
/// </summary>
public class DevelopCommandHandler(
    IAgentDiscoveryService agentDiscovery,
    ICopilotAgentService copilotAgent,
    IMediator mediator) : IRequestHandler<DevelopCommand, int>
{

    public async Task<int> Handle(DevelopCommand request, CancellationToken cancellationToken)
    {
        // Step 1: Discover developer agent
        ConsoleHelper.ShowInfo("Discovering developer agent...");
        AgentConfig developerAgent;
        try
        {
            developerAgent = agentDiscovery.DiscoverAgent("developer", request.WorkingDirectory);
            ConsoleHelper.ShowSuccess($"Found developer agent: {developerAgent.FilePath}");
        }
        catch (FileNotFoundException ex)
        {
            ConsoleHelper.ShowError(ex.Message);
            return 1;
        }

        // Step 2: Execute development
        ConsoleHelper.ShowInfo($"Starting development for work item #{request.WorkItemId}...");

        var systemPrompt = BuildDeveloperSystemPrompt(request.WorkItemId);

        try
        {
            var result = await ConsoleHelper.WithProgress(
                "Implementing feature...",
                async () => await copilotAgent.ExecuteAgentAsync(
                    developerAgent,
                    systemPrompt,
                    request.WorkingDirectory,
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
        if (request.WithReview)
        {
            ConsoleHelper.ShowInfo("Proceeding to review stage...");

            try
            {
                await ConsoleHelper.ShowCountdown("Starting review stage", 5, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                ConsoleHelper.ShowInfo("Review cancelled by user.");
                return 0;
            }

            return await mediator.Send(new ReviewCommand
            {
                WorkItemId = request.WorkItemId,
                WorkingDirectory = request.WorkingDirectory
            }, cancellationToken);
        }

        return 0;
    }

    /// <summary>
    /// Build system prompt for developer agent
    /// </summary>
    private string BuildDeveloperSystemPrompt(int workItemId)
    {
        return $@"You are an expert developer. Your task is to implement work item #{workItemId} following the AI plan.

Instructions:
1. Use Azure DevOps MCP to retrieve work item #{workItemId} and its plan (look for '# AI PLAN' comment)
2. Validate that plan exists and has 'Technical Implementation' and 'Acceptance Criteria' sections
3. If no plan found, report error and exit
4. Use git CLI to:
   - Check for uncommitted changes (git status --porcelain). If found, report error and exit.
   - Fetch latest from origin: git fetch origin
   - Checkout main/master: git checkout main (or master)
   - Pull latest: git pull origin main
   - Create feature branch: git checkout -b feature/{workItemId}
5. Use filesystem MCP to implement the feature according to Technical Implementation
6. Use CLI to build: dotnet build (or appropriate command)
7. Use CLI to test: dotnet test (or appropriate command)
8. If build/test fails, analyze errors and fix (max 3 retry attempts)
9. Generate unit tests based on Acceptance Criteria
10. Once all tests pass:
    - Stage changes: git add .
    - Commit: git commit -m ""feat: #{workItemId} <work-item-title>""
    - Push: git push origin feature/{workItemId}
11. Use Azure DevOps MCP to create Pull Request:
    - Title: #{workItemId} <work-item-title>
    - Description: Include the AI plan content
    - Link PR to work item

Progress Reporting:
- Report each major step as you complete it
- Show build/test results
- Report any errors encountered and how you resolved them

Return a summary of what was accomplished.";
    }
}
