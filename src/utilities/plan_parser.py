"""Plan parser to extract sections from markdown content"""

import re
from typing import Optional
from . import console_helper


class PlanParser:
    """Parse AI-generated plans from markdown"""
    
    # Required sections (case-insensitive)
    REQUIRED_SECTIONS = [
        "Technical Implementation",
        "Acceptance Criteria"
    ]
    
    # Optional but expected sections
    OPTIONAL_SECTIONS = [
        "User Story",
        "Test Paths"
    ]
    
    @staticmethod
    def extract_section(content: str, section_name: str) -> Optional[str]:
        """Extract a section from markdown content using fuzzy matching"""
        # Look for markdown headers (## or ###) with fuzzy matching
        pattern = rf"^#+\s*.*{re.escape(section_name)}.*$"
        match = re.search(pattern, content, re.IGNORECASE | re.MULTILINE)
        
        if not match:
            return None
        
        header_pos = match.end()
        
        # Find next header or end of content
        next_header = re.search(r"\n^#+\s", content[header_pos:], re.MULTILINE)
        
        if next_header:
            section_content = content[header_pos:header_pos + next_header.start()]
        else:
            section_content = content[header_pos:]
        
        return section_content.strip()
    
    @classmethod
    def parse(cls, content: str) -> dict:
        """
        Parse plan content and extract sections.
        Returns dict with sections and any missing required sections.
        """
        result = {
            "raw_content": content,
            "sections": {},
            "missing_required": [],
            "found_required": []
        }
        
        # Check for required sections
        for section in cls.REQUIRED_SECTIONS:
            extracted = cls.extract_section(content, section)
            if extracted:
                result["sections"][section] = extracted
                result["found_required"].append(section)
            else:
                result["missing_required"].append(section)
        
        # Extract optional sections
        for section in cls.OPTIONAL_SECTIONS:
            extracted = cls.extract_section(content, section)
            if extracted:
                result["sections"][section] = extracted
        
        return result
    
    @classmethod
    def validate(cls, content: str) -> tuple[bool, list[str]]:
        """
        Validate that plan contains all required sections.
        Returns (is_valid, missing_sections)
        """
        parsed = cls.parse(content)
        return (
            len(parsed["missing_required"]) == 0,
            parsed["missing_required"]
        )
