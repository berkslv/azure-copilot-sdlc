using Copiloter.CLI.Models;
using Copiloter.CLI.Services.Interfaces;
using Copiloter.CLI.Utilities;
using MediatR;

namespace Copiloter.CLI.Features.Commands;

/// <summary>
/// MediatR request for executing the Review command
/// </summary>
public class ReviewCommand : IWorkItemRequest<int>
{
    public int WorkItemId { get; set; }
    public string WorkingDirectory { get; set; } = Directory.GetCurrentDirectory();
}


/// <summary>
/// Handler for executing the Review command
/// </summary>
public class ReviewCommandHandler(
    IAgentDiscoveryService agentDiscovery,
    ICopilotAgentService copilotAgent) : IRequestHandler<ReviewCommand, int>
{

    public async Task<int> Handle(ReviewCommand request, CancellationToken cancellationToken)
    {
        // Step 1: Discover reviewer agent
        ConsoleHelper.ShowInfo("Discovering reviewer agent...");
        AgentConfig reviewerAgent;
        try
        {
            reviewerAgent = agentDiscovery.DiscoverAgent("reviewer", request.WorkingDirectory);
            ConsoleHelper.ShowSuccess($"Found reviewer agent: {reviewerAgent.FilePath}");
        }
        catch (FileNotFoundException ex)
        {
            ConsoleHelper.ShowError(ex.Message);
            return 1;
        }

        // Step 2: Execute review
        ConsoleHelper.ShowInfo($"Starting code review for work item #{request.WorkItemId}...");

        var systemPrompt = BuildReviewerSystemPrompt(request.WorkItemId);

        try
        {
            var result = await ConsoleHelper.WithProgress(
                "Reviewing code...",
                async () => await copilotAgent.ExecuteAgentAsync(
                    reviewerAgent,
                    systemPrompt,
                    request.WorkingDirectory,
                    timeout: TimeSpan.FromMinutes(20) // Longer timeout for review
                )
            );

            ConsoleHelper.ShowSuccess("Code review completed!");
            Console.WriteLine(result);
            return 0;
        }
        catch (Exception ex)
        {
            ConsoleHelper.ShowError($"Review failed: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Build system prompt for reviewer agent
    /// </summary>
    private string BuildReviewerSystemPrompt(int workItemId)
    {
        return $@"You are a senior code reviewer. Your task is to review the implementation for work item #{workItemId}.

Instructions:
1. Use git CLI to verify feature branch exists: git branch --list feature/{workItemId}
2. If branch doesn't exist, report error and exit
3. Use git CLI to check branch has commits: git rev-list --count feature/{workItemId}
4. If 0 commits, report error and exit
5. Use Azure DevOps MCP to retrieve work item #{workItemId} and its plan
6. Use git CLI to get changed files: git diff main...feature/{workItemId} --name-only
7. Use filesystem MCP to review each changed file
8. Run static analysis:
   - dotnet format --verify-no-changes (style)
   - Any security scanners available
9. Review code in this priority order:
   a. Security vulnerabilities (HIGH) - SQL injection, XSS, hardcoded secrets, insecure dependencies
   b. Correctness (HIGH) - Does it meet acceptance criteria? Logic errors?
   c. Test coverage (HIGH) - Are tests comprehensive? Edge cases covered?
   d. Performance issues (MEDIUM) - N+1 queries, inefficient algorithms, memory leaks
   e. Code quality (MEDIUM) - Readability, maintainability, DRY principle
   f. Code style (MEDIUM) - Formatting, naming
   g. Documentation (LOW) - Comments, XML docs, README updates

10. For each issue found, classify as:
    - Critical: Security vulnerabilities, data loss risks (must fix all)
    - Major: Incorrect logic, missing tests (must fix all)
    - Minor: Code style, minor performance (fix if easy, otherwise document)

11. If Critical/Major issues found:
    - Fix them using filesystem MCP
    - Create commit: git commit -m ""review: <category> - <description>""
    - Push: git push origin feature/{workItemId}
    - Repeat review (max 3 iterations)

12. When review complete (no Critical/Major issues):
    - Use Azure DevOps MCP to update PR description with review summary
    - Add PR comment: ""AI Review Complete: X issues found and fixed. Ready for human review.""
    - Add work item comment with review summary and PR link
    - Update work item state to ""In Review"" (if that state exists)

Progress Reporting:
- Report each review category as you complete it
- Show counts: X Critical, Y Major, Z Minor issues
- Show what was fixed in each iteration
- Final summary of review results

Return a detailed review summary.";
    }
}
