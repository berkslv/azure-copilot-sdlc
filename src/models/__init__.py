"""Data models for azure-copilot-sdlc"""

from dataclasses import dataclass
from typing import Optional


@dataclass
class AIPlan:
    """Represents the AI-generated plan for a work item"""
    user_story: str
    technical_implementation: str
    acceptance_criteria: str
    test_paths: str
    raw_content: str
    
    @classmethod
    def parse_from_markdown(cls, content: str) -> "AIPlan":
        """Parse plan from markdown content"""
        # This will be implemented in utilities/plan_parser.py
        pass


@dataclass
class WorkItem:
    """Represents an Azure DevOps work item"""
    id: int
    title: str
    description: str
    work_item_type: str  # Task, Bug, Feature, Epic, etc.
    state: str
    assigned_to: Optional[str] = None
    iteration_path: Optional[str] = None
    comments: list = None

    def __post_init__(self):
        if self.comments is None:
            self.comments = []


@dataclass
class AgentConfig:
    """Configuration for an AI agent"""
    name: str
    path: str
    description: str
    purpose: str  # 'planner', 'developer', 'reviewer'
