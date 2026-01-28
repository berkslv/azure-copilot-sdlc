"""CLI entry point for azure-copilot-sdlc"""

import typer
from rich.console import Console
from rich.text import Text

from utilities.config import load_env_from_home
from commands.plan import plan
from commands.develop import develop
from commands.review import review

# Load .env from home directory
load_env_from_home()

# Create console for output
console = Console()


def show_banner():
    """Display colorful startup banner"""
    banner_text = Text(
        "▶ azure-copilot-sdlc",
        style="bold cyan"
    )
    console.print(banner_text)
    console.print(Text("Azure DevOps Work Item Lifecycle Automation", style="dim"), end="\n\n")


# Create main app
app = typer.Typer()

# Add commands directly (no subgroups)
app.command(help="Enrich work items with AI-generated implementation plans")(plan)
app.command(help="Implement features based on plans")(develop)
app.command(help="Review code changes before PR merge")(review)


@app.callback(invoke_without_command=True)
def main(ctx: typer.Context):
    """
    Azure DevOps work item lifecycle automation using GitHub Copilot.
    
    This tool helps you automate the planning, development, and review stages
    of Azure DevOps work items using AI agents powered by GitHub Copilot.
    """
    # Show banner only when no command is specified
    if ctx.invoked_subcommand is None:
        show_banner()


def cli():
    """Entry point for the CLI"""
    app()


if __name__ == "__main__":
    cli()
