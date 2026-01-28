"""Console helper utilities using rich for colorful output"""

from rich.console import Console
from rich.panel import Panel
from rich.prompt import Prompt, Confirm
from typing import Literal

console = Console()


def show_error(message: str):
    """Display error message"""
    console.print(f"[red]Error:[/red] {message}")


def show_warning(message: str):
    """Display warning message"""
    console.print(f"[yellow]Warning:[/yellow] {message}")


def show_success(message: str):
    """Display success message"""
    console.print(f"[green]✓[/green] {message}")


def show_info(message: str):
    """Display info message"""
    console.print(f"[blue]ℹ[/blue] {message}")


def show_panel(title: str, content: str):
    """Display content in a panel"""
    panel = Panel(content, title=title, expand=False, border_style="blue")
    console.print(panel)


def confirm(message: str, default: bool = False) -> bool:
    """Prompt user for confirmation"""
    return Confirm.ask(message, default=default)


def prompt(message: str, default: str = None, password: bool = False) -> str:
    """Prompt user for input"""
    return Prompt.ask(message, default=default, password=password)


def prompt_choice(message: str, choices: list[str]) -> str:
    """Prompt user to choose from list"""
    for i, choice in enumerate(choices, 1):
        console.print(f"  {i}. {choice}")
    
    while True:
        try:
            idx = int(Prompt.ask(message)) - 1
            if 0 <= idx < len(choices):
                return choices[idx]
        except ValueError:
            pass
        
        show_error(f"Please select a number between 1 and {len(choices)}")
