"""Configuration loading from home directory"""

import os
import sys
from pathlib import Path
from dotenv import load_dotenv, set_key, dotenv_values
from . import console_helper


def load_env_from_home():
    """Load .env file from home directory based on OS and load all variables into process environment"""
    home = Path.home()
    
    if sys.platform == "win32":
        # Windows: C:\Users\{username}\.azure-copilot-sdlc\.env
        env_path = home / ".azure-copilot-sdlc" / ".env"
        config_dir = home / ".azure-copilot-sdlc"
    else:
        # Unix/Linux/Mac: ~/.azure-copilot-sdlc/.env
        env_path = home / ".azure-copilot-sdlc" / ".env"
        config_dir = home / ".azure-copilot-sdlc"
    
    # Create config directory if it doesn't exist
    config_dir.mkdir(parents=True, exist_ok=True)
    
    print(f"Loading config from: {env_path}")
    
    # Load .env file if it exists and set all variables in process environment
    if env_path.exists():
        # load_dotenv automatically loads variables into os.environ
        load_dotenv(env_path, override=True)
        
        # Explicitly verify all variables are loaded into process environment
        env_values = dotenv_values(env_path)
        for key, value in env_values.items():
            if value:  # Only set non-empty values
                os.environ[key] = value
    
    return config_dir, env_path


def get_env_variable(var_name: str, prompt_text: str = None, password: bool = True) -> str:
    """Get environment variable from .env file, prompt if missing, and save to file
    
    Args:
        var_name: Name of the environment variable
        prompt_text: Custom prompt text (optional)
        password: Whether to hide input when prompting
    
    Returns:
        The environment variable value
    """
    env_path = get_env_path()
    
    # Load current values from .env file
    env_values = dotenv_values(env_path) if env_path.exists() else {}
    
    # Check if variable exists in .env file
    value = env_values.get(var_name)
    
    if value:
        # Set in current process environment if not already there
        if var_name not in os.environ:
            os.environ[var_name] = value
        return value
    
    # Variable not found, prompt user
    if not prompt_text:
        prompt_text = f"{var_name} not found. Please enter your value:"
    
    value = console_helper.prompt(prompt_text, password=password)
    
    # Ensure .env file exists
    if not env_path.exists():
        env_path.touch()
    
    # Save to .env file
    set_key(env_path, var_name, value)
    
    # Set in current process environment
    os.environ[var_name] = value
    
    console_helper.show_success(f"{var_name} saved to {env_path}")
    
    return value


def get_config_dir() -> Path:
    """Get the config directory path"""
    home = Path.home()
    return home / ".azure-copilot-sdlc"


def get_env_path() -> Path:
    """Get the .env file path"""
    return get_config_dir() / ".env"
