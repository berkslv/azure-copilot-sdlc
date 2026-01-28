"""Validation utilities"""

import os
from pathlib import Path
from git import Repo, InvalidGitRepositoryError
from . import console_helper
from .config import get_env_variable


def validate_git_repo(directory: str) -> Path:
    """Validate that directory is a git repository"""
    try:
        path = Path(directory).resolve()
        if not path.exists():
            console_helper.show_error(f"Directory does not exist: {directory}")
            raise ValueError(f"Directory not found: {directory}")
        
        Repo(str(path))
        return path
    except InvalidGitRepositoryError:
        console_helper.show_error(
            "Directory is not a git repository. Please run this command from within "
            "a git repository or specify a valid git directory with -d."
        )
        raise
    except Exception as e:
        console_helper.show_error(str(e))
        raise


def validate_work_item_id(work_item_id: str) -> int:
    """Validate work item ID is a positive integer"""
    try:
        item_id = int(work_item_id)
        if item_id <= 0:
            raise ValueError("Work item ID must be positive")
        return item_id
    except ValueError:
        console_helper.show_error(
            f"Invalid work item ID: {work_item_id}. Must be a positive integer."
        )
        raise


def validate_environment_variable(var_name: str, prompt_text: str = None) -> str:
    """Validate environment variable exists in .env file, prompt user if missing
    
    This function uses the .env file in ~/.azure-copilot-sdlc/.env instead of
    OS environment variables. If the variable is not found, it prompts the user
    and saves it to the .env file for future use.
    """
    return get_env_variable(var_name, prompt_text, password=True)
