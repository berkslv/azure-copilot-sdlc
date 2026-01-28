"""Develop command implementation"""

from pathlib import Path
from typing import Optional
import typer
from services import (
    AgentDiscoveryService,
    CopilotAgentService,
    GitService
)
from utilities import (
    console_helper,
    validators
)


def develop(
    work_item_id: int = typer.Argument(..., help="Azure DevOps work item ID"),
    directory: str = typer.Option(".", "-d", "--directory", help="Working directory"),
    with_review: bool = typer.Option(False, "-r", "--with-review", help="Run review after development")
):
    """
    Implement feature based on work item plan.
    
    Workflow:
    1. Validate work item and plan exists
    2. Create feature branch
    3. Execute developer agent
    4. Build and test
    5. Create pull request
    6. Optionally run review
    """
    try:
        # Validate inputs
        work_dir = validators.validate_git_repo(directory)
        item_id = validators.validate_work_item_id(str(work_item_id))
        
        console_helper.show_info(f"Implementing work item #{item_id}...")
        
        # Check for uncommitted changes
        git = GitService(work_dir)
        if git.has_uncommitted_changes():
            console_helper.show_error(
                "You have uncommitted changes. Please commit or stash them first."
            )
            raise typer.Exit(code=1)
        
        # Create feature branch
        branch_name = f"feature/{item_id}"
        if git.branch_exists(branch_name):
            console_helper.show_warning(f"Branch {branch_name} already exists")
            choice = console_helper.prompt_choice(
                "What would you like to do?",
                ["Use existing", "Delete and recreate", "Cancel"]
            )
            
            if choice == "Cancel":
                console_helper.show_info("Cancelled")
                return
            elif choice == "Delete and recreate":
                git.delete_branch(branch_name, force=True)
                git.create_branch(branch_name)
        else:
            git.create_branch(branch_name)
        
        # Discover agent
        discovery = AgentDiscoveryService(work_dir)
        agent = discovery.discover_agent("develop")
        if not agent:
            raise ValueError("Developer agent not found")
        
        # Configure MCP
        mcp_config = McpConfigurationService(work_dir).get_mcp_config()
        
        # Execute agent
        copilot = CopilotAgentService(mcp_config, work_dir)
        
        system_prompt = (
            "You are a senior software developer. Implement the feature based on the "
            "technical implementation plan. Write clean, maintainable code following "
            "project conventions. Include unit tests covering acceptance criteria."
        )
        
        prompt = (
            f"Implement work item #{item_id} based on the plan and technical specifications. "
            f"Write clean code with tests. After implementation, build and run tests to verify."
        )
        
        success, output = copilot.execute_agent(
            agent.path,
            prompt,
            system_prompt,
            timeout=600
        )
        
        if not success:
            raise ValueError(f"Implementation failed: {output}")
        
        console_helper.show_success("Implementation completed")
        
        # Commit changes
        # TODO: Implement build/test cycle before commit
        message = f"feat: #{item_id} implementation"
        if not git.commit(message):
            raise ValueError("Failed to commit changes")
        
        # Push branch
        if not git.push(branch_name):
            raise ValueError("Failed to push branch")
        
        # Create PR
        # TODO: Implement Azure DevOps PR creation
        console_helper.show_success(f"Ready to create PR for {branch_name}")
        console_helper.show_info("Note: PR creation not yet implemented")
        
        # Optional review
        if with_review:
            console_helper.show_info("Starting review stage...")
            # TODO: Call review command
            raise NotImplementedError("Review stage not yet implemented")
        
    except Exception as e:
        console_helper.show_error(str(e))
        raise typer.Exit(code=1)
