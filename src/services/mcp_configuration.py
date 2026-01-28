"""MCP configuration service"""

import json
import os
import subprocess
import shutil
from pathlib import Path
from typing import Optional
from utilities import console_helper, validators
from utilities.config import get_env_variable


class McpConfigurationService:
    """Configure Model Context Protocol servers"""
    
    def __init__(self, working_directory: Path):
        self.working_directory = Path(working_directory).resolve()
    
    def _check_npx_available(self) -> bool:
        """Check if npx is available in PATH"""
        try:
            # Use shutil.which() to find npx in PATH (works cross-platform)
            npx_path = shutil.which("npx")
            if npx_path:
                return True
            
            # Fallback: try running npx --version
            result = subprocess.run(
                "npx --version",
                shell=True,
                capture_output=True,
                timeout=5
            )
            return result.returncode == 0
        except Exception:
            return False
    
    def _extract_org_from_git(self) -> Optional[str]:
        """Extract Azure DevOps organization from git remote"""
        try:
            result = subprocess.run(
                ["git", "config", "remote.origin.url"],
                cwd=self.working_directory,
                capture_output=True,
                text=True,
                check=True
            )
            url = result.stdout.strip()
            
            # Parse URL like: https://dev.azure.com/ORG/PROJECT/_git/REPO
            # or: git@ssh.dev.azure.com:v3/ORG/PROJECT/REPO
            if "dev.azure.com" in url:
                parts = url.split("/")
                if len(parts) >= 4:
                    return parts[3]  # ORG is at index 3
            
            return None
        except subprocess.CalledProcessError:
            return None
    
    def get_mcp_config(self) -> str:
        """Get MCP configuration JSON for copilot CLI"""
        if not self._check_npx_available():
            console_helper.show_error(
                "npx is not available. Please install Node.js to use MCP servers."
            )
            raise RuntimeError("npx not available")
        
        # Get Azure DevOps PAT
        pat = validators.validate_environment_variable(
            "ADO_MCP_AUTH_TOKEN",
            "Azure DevOps PAT not found. Please enter your PAT:"
        )
        
        # Get organization - check .env file first
        try:
            org = get_env_variable(
                "AZURE_DEVOPS_ORG",
                prompt_text=None,  # Will use default prompt if needed
                password=False
            )
        except Exception:
            # If get_env_variable fails, try extracting from git
            org = self._extract_org_from_git()
            if not org:
                org = console_helper.prompt("Enter Azure DevOps organization name:", password=False)
                # Save it to .env for future use
                from utilities.config import get_env_path
                from dotenv import set_key
                env_path = get_env_path()
                if env_path.exists():
                    set_key(env_path, "AZURE_DEVOPS_ORG", org)
        
        # Build MCP config
        config = {
            "mcpServers": {
                "filesystem": {
                    "type":"stdio",
                    "tools": ["*"],
                    "command": "npx",
                    "args": [
                        "-y",
                        "@modelcontextprotocol/server-filesystem",
                        str(self.working_directory)
                    ]
                },
                "azure-devops": {
                    "type":"stdio",
                    "tools": ["*"],
                    "command": "npx",
                    "args": [
                        "-y",
                        "@azure-devops/mcp",
                        org,
                        "--authentication",
                        "envvar"
                    ],
                    "env": {
                        "ADO_MCP_AUTH_TOKEN": pat
                    }
                }
            }
        }
        
        return json.dumps(config)
