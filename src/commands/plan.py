"""Plan command implementation"""

from pathlib import Path
from typing import Optional
from datetime import datetime
import typer
from services import (
    AgentDiscoveryService,
    CopilotAgentService
)
from utilities import (
    console_helper,
    validators
)
from utilities.config import get_env_variable


def build_combined_plan_prompt(work_item_id: int, project: str) -> str:
    """Build combined prompt to generate and save plan in one execution"""
    timestamp = datetime.utcnow().strftime("%Y-%m-%d %H:%M:%S")
    return f"""You are a technical planning assistant. Your task is to:
1. Retrieve work item #{work_item_id} from Azure DevOps project "{project}"
2. Create a detailed implementation plan
3. Save the plan as a comment to the work item

Required Plan Structure:
1. # COPILOT PLAN (top-level header)
2. ## User Story - What the user wants, the story of the work item
3. ## Technical Implementation - Search project, find correct places for development, create abstract development plan
   - Include file paths and class names. Method signatures are helpful but not required.
   - Mid-level detail: architectural components, key classes/files to modify, new files to create, dependencies to add.
4. ## Acceptance Criteria - Detailed, testable criteria
   - Use testable/measurable criteria. Given-When-Then format is preferred but not required.
5. ## Test Paths - Manual testing steps to verify the requirement
   - Focus on manual testing steps. Automated test suggestions can be mentioned briefly.

Instructions:
1. Use Azure DevOps MCP to retrieve work item #{work_item_id} from project "{project}"
2. Use filesystem MCP to analyze the project structure
3. Create a comprehensive plan following the structure above
4. Keep plan under 2000 tokens (~1000 words)
5. Be specific and actionable also keep it concise
6. After creating the plan, immediately use Azure DevOps MCP to save the plan as a comment to work item #{work_item_id}
   - Check for existing '# COPILOT PLAN' comment and update it if found
   - Create new comment if not found
   - Prefix comment with '# COPILOT PLAN' tag
   - Add timestamp: 'Generated on {timestamp} UTC'
   - Update work item state to 'Active' (or 'In Progress' or 'Committed' if 'Active' is not valid)
   - Do NOT change assigned user, iteration, or other fields just make comment
"""


def plan(
    work_item_id: int = typer.Argument(..., help="Azure DevOps work item ID"),
    directory: str = typer.Option(".", "-d", "--directory", help="Working directory")
):
    """
    Generate Copilot plan for a work item.
    
    Workflow:
    1. Validate git repository and work item ID
    2. Discover planner agent
    3. Generate and save plan in a single execution
    """
    try:
        # Validate inputs
        work_dir = validators.validate_git_repo(directory)
        validators.validate_work_item_id(str(work_item_id))
        
        console_helper.show_info(f"Planning work item #{work_item_id}...")
        
        # Discover agent
        discovery = AgentDiscoveryService(work_dir)
        agent = discovery.discover_agent("plan")
        
        # Get Azure DevOps project name
        project = get_env_variable(
            "AZURE_DEVOPS_PROJECT",
            prompt_text="Enter Azure DevOps project name:",
            password=False
        )
        
        # Generate and save plan in one execution
        copilot = CopilotAgentService(work_dir)
        prompt = build_combined_plan_prompt(work_item_id, project)
        
        success, output = copilot.execute_agent(
            agent=agent,
            prompt=prompt,
            timeout=1000
        )
        
        if not success:
            raise ValueError(f"Plan generation and save failed: {output}")
        
        # Display result
        console_helper.show_panel("Plan Generation Complete", output)
        console_helper.show_success("Plan generated and saved to Azure DevOps")
        
    except Exception as e:
        console_helper.show_error(str(e))
        raise typer.Exit(code=1)
