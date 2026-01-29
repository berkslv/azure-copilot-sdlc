"""Develop command implementation"""

from datetime import datetime
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
from utilities.config import get_env_variable

def build_develop_prompt(work_item_id: int, project: str, branch_name: str) -> str:
    """Build comprehensive prompt for development execution"""
    timestamp = datetime.utcnow().strftime("%Y-%m-%d %H:%M:%S")
    return f"""You are a senior software developer. Your task is to implement the feature for work item #{work_item_id}.

Instructions:
1. Analyze the COPILOT PLAN comment on work item #{work_item_id} in project "{project}" (retrieve from Azure DevOps)
2. Follow the Technical Implementation section to guide your development
3. Write clean, maintainable code following project conventions
4. Create unit tests covering all acceptance criteria
5. Ensure code builds successfully
6. Run tests and verify all pass
7. Commit changes to branch '{branch_name}' with message: "feat: #{work_item_id} implementation"

Requirements:
- Follow the technical implementation plan precisely
- Write tests as you implement features
- Include error handling and validation
- Ensure code is well-documented with comments where needed
- Verify all acceptance criteria are met
- Keep commits atomic and meaningful

After implementation:
1. Run full test suite
2. Verify build succeeds
3. Ensure all acceptance criteria are met
4. Stage and commit changes (commit message: "feat: #{work_item_id} implementation")
6. Push changes to origin
5. Create PR in Azure devops for review using mcp

Be thorough and ensure high quality implementation.
Generated on {timestamp} UTC
"""


def develop(
    work_item_id: int = typer.Argument(..., help="Azure DevOps work item ID"),
    directory: str = typer.Option(".", "-d", "--directory", help="Working directory"),
    with_review: bool = typer.Option(False, "-r", "--with-review", help="Run review after development"),
    model: str = typer.Option(None, "-m", "--model", help="LLM model to use (e.g., gpt-5-mini, gpt-4)")
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

        # Get Azure DevOps project name
        project = get_env_variable(
            "AZURE_DEVOPS_PROJECT",
            prompt_text="Enter Azure DevOps project name:",
            password=False
        )
        
        # Execute agent with comprehensive prompt
        copilot = CopilotAgentService(work_dir, model=model)
        prompt = build_develop_prompt(item_id, project, branch_name)
        
        success, output = copilot.execute_agent(
            agent=agent,
            prompt=prompt,
            timeout=600
        )
        
        if not success:
            raise ValueError(f"Implementation failed: {output}")
        
        console_helper.show_success("Implementation completed")
        
    except Exception as e:
        console_helper.show_error(str(e))
        raise typer.Exit(code=1)
