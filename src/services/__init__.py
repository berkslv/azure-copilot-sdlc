"""Services module"""

from .agent_discovery import AgentDiscoveryService
from .copilot_agent import CopilotAgentService
from .mcp_configuration import McpConfigurationService
from .git_service import GitService

__all__ = [
    "AgentDiscoveryService",
    "CopilotAgentService",
    "McpConfigurationService",
    "GitService"
]
