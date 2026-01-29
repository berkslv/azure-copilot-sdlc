"""Copilot agent service to execute agents"""

import subprocess
import os
import shlex
import time
from pathlib import Path
from typing import Optional
from utilities.console_helper import console
from utilities import console_helper
from models import AgentConfig


class CopilotAgentService:
    """Execute AI agents via Copilot CLI"""
    
    def __init__(self, working_directory: Path, model: Optional[str] = None):
        """
        Initialize service.
        
        Args:
            working_directory: Working directory for agent execution
            model: Optional model parameter (e.g., 'gpt-5-mini', 'gpt-4', etc.)
        """
        self.working_directory = Path(working_directory).resolve()
        self.model = model or "gpt-5-mini"
    
    def _check_copilot_available(self) -> bool:
        """Check if copilot CLI is available"""
        try:
            subprocess.run(
                ["copilot", "--version"],
                capture_output=True,
                check=True,
                timeout=5
            )
            return True
        except (subprocess.CalledProcessError, FileNotFoundError, subprocess.TimeoutExpired):
            return False
    
    def execute_agent(
        self,
        agent: AgentConfig,
        prompt: str,
        timeout: int = 300,
        model: Optional[str] = None
    ) -> tuple[bool, str]:
        """
        Execute an agent with given prompt.
        
        Workflow:
        1. Get MCP configuration
        2. Read agent file
        3. Execute copilot CLI with MCP servers
        
        Args:
            agent: AgentConfig object with agent information
            prompt: User prompt/request
            timeout: Timeout in seconds (default: 5 minutes)
            model: Optional model override (e.g., 'gpt-5-mini', 'gpt-4', etc.)
        
        Returns:
            Tuple of (success, output)
        """
        if not self._check_copilot_available():
            console_helper.show_error(
                "Copilot CLI is not available. Please install it first."
            )
            return False, ""
        
        try:
            # Get MCP configuration
            from .mcp_configuration import McpConfigurationService
            mcp_service = McpConfigurationService(self.working_directory)
            mcp_config = mcp_service.get_mcp_config()
            
            # Execute copilot with streaming output (UTF-8 decoding)
            model_to_use = model or self.model
            cmd = [
                "copilot",
                "--additional-mcp-config", mcp_config,
                "--yolo",
                "--model", model_to_use,
                "--prompt", prompt,
            ]

            if agent and agent.name:
                cmd.extend(["--agent", agent.name])

            # Debug: Print command as runnable one-liner
            cmd_str = ' '.join(shlex.quote(str(arg)) for arg in cmd)
            console_helper.show_info(f"Running: {cmd_str}")

            env = os.environ.copy()
            env.setdefault("PYTHONIOENCODING", "utf-8")

            output_lines: list[str] = []
            error_lines: list[str] = []

            start = time.time()
            with console.status("[bold cyan]Executing agent...", spinner="dots") as status:
                proc = subprocess.Popen(
                    cmd,
                    stdout=subprocess.PIPE,
                    stderr=subprocess.PIPE,
                    text=True,
                    encoding="utf-8",
                    errors="replace",
                    cwd=str(self.working_directory),
                    env=env
                )

                try:
                    while True:
                        # Read a chunk of stdout
                        line = proc.stdout.readline() if proc.stdout else ""
                        if line:
                            output_lines.append(line)
                            status.update(f"[bold green]Partial Output:\n{''.join(output_lines)[-800:]}")

                        # Check for timeout
                        if timeout and time.time() - start > timeout:
                            proc.kill()
                            raise subprocess.TimeoutExpired(cmd, timeout)

                        # Exit loop when process ends and buffers drained
                        if proc.poll() is not None and not line:
                            break

                    # Drain remaining stdout/stderr
                    if proc.stdout:
                        output_lines.extend(proc.stdout.read().splitlines(keepends=True))
                    if proc.stderr:
                        error_lines.extend(proc.stderr.read().splitlines(keepends=True))
                finally:
                    if proc.stdout:
                        proc.stdout.close()
                    if proc.stderr:
                        proc.stderr.close()

            stdout_text = "".join(output_lines)
            stderr_text = "".join(error_lines)

            if proc.returncode == 0:
                console_helper.show_success(f"Agent completed: {stdout_text}")
                return True, stdout_text
            else:
                console_helper.show_error(f"Agent failed: {stderr_text}")
                return False, stderr_text
        
        except subprocess.TimeoutExpired:
            console_helper.show_error(
                f"Agent execution timed out after {timeout} seconds"
            )
            return False, ""
        except Exception as e:
            console_helper.show_error(f"Agent execution failed: {str(e)}")
            return False, str(e)
