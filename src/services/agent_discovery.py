"""Agent discovery service to find custom agents"""

from pathlib import Path
from typing import Optional
from models import AgentConfig
from utilities import console_helper


class AgentDiscoveryService:
    """Discover and load custom AI agents"""
    
    SEARCH_PATHS = [
        ".github/agents",
        "agents",
        "docs/agents",
        "."
    ]
    
    AGENT_TYPES = {
        "plan": "planner.agent.md",
        "develop": "developer.agent.md",
        "review": "reviewer.agent.md"
    }
    
    def __init__(self, working_directory: Path):
        self.working_directory = Path(working_directory).resolve()
    
    def discover_agent(self, agent_type: str) -> Optional[AgentConfig]:
        """
        Discover an agent by type (plan, develop, review).
        Returns AgentConfig or None if not found.
        """
        agent_filename = self.AGENT_TYPES.get(agent_type)
        if not agent_filename:
            console_helper.show_error(f"Unknown agent type: {agent_type}")
            return None
        
        for search_path in self.SEARCH_PATHS:
            candidate = self.working_directory / search_path / agent_filename
            if candidate.exists():
                console_helper.show_info(f"Found {agent_type} agent: {candidate}")
                # Extract agent name from filename (e.g., "planner" from "planner.agent.md")
                agent_name = agent_filename.split('.')[0]
                return AgentConfig(
                    name=agent_name,
                    path=str(candidate),
                    description=f"{agent_type.capitalize()} agent",
                    purpose=agent_type
                )
        
        console_helper.show_error(
            f"Could not find {agent_type} agent ({agent_filename}). "
            f"Search paths: {', '.join(self.SEARCH_PATHS)}"
        )
        return None
    
    def discover_all(self) -> dict[str, Optional[AgentConfig]]:
        """Discover all required agents"""
        return {
            agent_type: self.discover_agent(agent_type)
            for agent_type in ["plan", "develop", "review"]
        }
