"""Review command implementation"""

from pathlib import Path
import typer
from services import (
    AgentDiscoveryService,
    CopilotAgentService,
    McpConfigurationService,
    GitService
)
from utilities import (
    console_helper,
    validators
)


def review(
    work_item_id: int = typer.Argument(..., help="Azure DevOps work item ID"),
    directory: str = typer.Option(".", "-d", "--directory", help="Working directory")
):
    """
    Review code changes for a work item.
    
    Workflow:
    1. Find feature branch
    2. Retrieve work item and plan
    3. Execute reviewer agent
    4. Apply fixes for issues found
    5. Update PR and work item
    """
    try:
        # Validate inputs
        work_dir = validators.validate_git_repo(directory)
        item_id = validators.validate_work_item_id(str(work_item_id))
        
        console_helper.show_info(f"Reviewing work item #{item_id}...")
        
        # Find branch
        git = GitService(work_dir)
        branch_name = f"feature/{item_id}"
        
        if not git.branch_exists(branch_name):
            console_helper.show_error(f"Branch {branch_name} not found")
            raise typer.Exit(code=1)
        
        # Switch to branch
        if not git.switch_branch(branch_name):
            raise ValueError(f"Could not switch to branch {branch_name}")
        
        # Discover agent
        discovery = AgentDiscoveryService(work_dir)
        agent = discovery.discover_agent("review")
        if not agent:
            raise ValueError("Reviewer agent not found")
        
        # Configure MCP
        mcp_config = McpConfigurationService(work_dir).get_mcp_config()
        
        # Execute agent
        copilot = CopilotAgentService(mcp_config, work_dir)
        
        system_prompt = (
            "You are a senior code reviewer. Review the implementation against: "
            "1. Security vulnerabilities\n"
            "2. Correctness and logic\n"
            "3. Test coverage\n"
            "4. Performance\n"
            "5. Code quality and maintainability\n"
            "Provide specific, actionable feedback."
        )
        
        prompt = (
            f"Review the implementation of work item #{item_id} on branch {branch_name}. "
            f"Check for security issues, correctness, test coverage, and code quality. "
            f"Provide a detailed review summary."
        )
        
        success, output = copilot.execute_agent(
            agent.path,
            prompt,
            system_prompt,
            timeout=300
        )
        
        if not success:
            raise ValueError(f"Review failed: {output}")
        
        console_helper.show_panel("Review Results", output[:500] + "..." if len(output) > 500 else output)
        
        # TODO: Parse review results and apply fixes
        console_helper.show_info("Note: Automatic issue fixing not yet implemented")
        console_helper.show_success("Code review completed")
        
    except Exception as e:
        console_helper.show_error(str(e))
        raise typer.Exit(code=1)
