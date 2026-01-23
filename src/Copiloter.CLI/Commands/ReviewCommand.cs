using Copiloter.CLI.Models;
using Copiloter.CLI.Services;
using Copiloter.CLI.Utilities;

namespace Copiloter.CLI.Commands;

/// <summary>
/// Command to review code before PR merge
/// </summary>
public class ReviewCommand
{
    private readonly string _workingDirectory;
    private readonly int _workItemId;
    private readonly AgentDiscoveryService _agentDiscovery;
    private readonly McpConfigurationService _mcpConfig;
    private readonly CopilotAgentService _copilotAgent;

    public ReviewCommand(string workingDirectory, int workItemId)
    {
        _workingDirectory = workingDirectory;
        _workItemId = workItemId;

        _agentDiscovery = new AgentDiscoveryService(workingDirectory);
        _mcpConfig = new McpConfigurationService(workingDirectory);
        _copilotAgent = new CopilotAgentService(workingDirectory, _mcpConfig);
    }

    /// <summary>
    /// Execute the review command
    /// </summary>
    public async Task<int> ExecuteAsync()
    {
        try
        {
            // Step 1: Discover reviewer agent
            ConsoleHelper.ShowInfo("Discovering reviewer agent...");
            AgentConfig reviewerAgent;
            try
            {
                reviewerAgent = _agentDiscovery.DiscoverAgent("reviewer");
                ConsoleHelper.ShowSuccess($"Found reviewer agent: {reviewerAgent.FilePath}");
            }
            catch (FileNotFoundException ex)
            {
                ConsoleHelper.ShowError(ex.Message);
                return 1;
            }

            // Step 2: Execute review
            ConsoleHelper.ShowInfo($"Starting code review for work item #{_workItemId}...");
            
            var systemPrompt = BuildReviewerSystemPrompt();

            try
            {
                var result = await ConsoleHelper.WithProgress(
                    "Reviewing code...",
                    async () => await _copilotAgent.ExecuteAgentAsync(
                        reviewerAgent,
                        systemPrompt,
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
        finally
        {
            await _copilotAgent.DisposeAsync();
        }
    }

    /// <summary>
    /// Build system prompt for reviewer agent
    /// </summary>
    private string BuildReviewerSystemPrompt()
    {
        return $@"You are a senior code reviewer. Your task is to review the implementation for work item #{_workItemId}.

Instructions:
1. Use git CLI to verify feature branch exists: git branch --list feature/{_workItemId}
2. If branch doesn't exist, report error and exit
3. Use git CLI to check branch has commits: git rev-list --count feature/{_workItemId}
4. If 0 commits, report error and exit
5. Use Azure DevOps MCP to retrieve work item #{_workItemId} and its plan
6. Use git CLI to get changed files: git diff main...feature/{_workItemId} --name-only
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
    - Push: git push origin feature/{_workItemId}
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
