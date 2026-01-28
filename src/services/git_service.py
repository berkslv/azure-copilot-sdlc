"""Git service for repository operations"""

import subprocess
from pathlib import Path
from typing import Optional
from utilities import console_helper


class GitService:
    """Git operations"""
    
    def __init__(self, working_directory: Path):
        self.working_directory = Path(working_directory).resolve()
    
    def _run_git(self, *args, **kwargs) -> tuple[bool, str]:
        """Run git command and return (success, output)"""
        try:
            result = subprocess.run(
                ["git"] + list(args),
                cwd=str(self.working_directory),
                capture_output=True,
                text=True,
                check=False,
                **kwargs
            )
            return result.returncode == 0, result.stdout.strip()
        except FileNotFoundError:
            return False, "Git not found"
    
    def get_status(self) -> str:
        """Get git status"""
        success, output = self._run_git("status", "--porcelain")
        return output if success else ""
    
    def has_uncommitted_changes(self) -> bool:
        """Check if repository has uncommitted changes"""
        status = self.get_status()
        return bool(status)
    
    def create_branch(self, branch_name: str, from_branch: str = "main") -> bool:
        """Create and checkout new branch"""
        # Fetch latest
        success, _ = self._run_git("fetch", "origin")
        if not success:
            console_helper.show_warning("Could not fetch from origin")
        
        # Sync with main
        console_helper.show_info(f"Syncing with origin/{from_branch}...")
        success, _ = self._run_git("checkout", from_branch)
        if not success:
            console_helper.show_warning(f"Could not checkout {from_branch}")
        
        success, _ = self._run_git("reset", "--hard", f"origin/{from_branch}")
        if not success:
            console_helper.show_error("Could not sync with remote")
            return False
        
        # Create branch
        success, _ = self._run_git("checkout", "-b", branch_name)
        if not success:
            console_helper.show_error(f"Could not create branch {branch_name}")
            return False
        
        console_helper.show_success(f"Created branch {branch_name}")
        return True
    
    def branch_exists(self, branch_name: str) -> bool:
        """Check if branch exists locally or remotely"""
        success, output = self._run_git("branch", "-a")
        if not success:
            return False
        
        for line in output.split("\n"):
            # Strip remotes/origin prefix and whitespace
            line = line.strip().replace("remotes/origin/", "")
            if line == branch_name:
                return True
        return False
    
    def switch_branch(self, branch_name: str) -> bool:
        """Switch to existing branch"""
        success, _ = self._run_git("checkout", branch_name)
        if not success:
            console_helper.show_error(f"Could not switch to branch {branch_name}")
        return success
    
    def delete_branch(self, branch_name: str, force: bool = False) -> bool:
        """Delete branch"""
        flag = "-D" if force else "-d"
        success, _ = self._run_git("branch", flag, branch_name)
        return success
    
    def commit(self, message: str, skip_hooks: bool = False) -> bool:
        """Commit staged changes"""
        args = ["commit", "-m", message]
        if skip_hooks:
            args.append("--no-verify")
        
        success, output = self._run_git(*args)
        if not success:
            console_helper.show_error(f"Commit failed: {output}")
        else:
            console_helper.show_success(f"Committed: {message}")
        
        return success
    
    def push(self, branch_name: str, force: bool = False) -> bool:
        """Push branch to origin"""
        args = ["push", "origin", branch_name]
        if force:
            args.append("--force")
        
        success, output = self._run_git(*args)
        if not success:
            console_helper.show_error(f"Push failed: {output}")
        else:
            console_helper.show_success(f"Pushed branch {branch_name}")
        
        return success
    
    def get_default_branch(self) -> str:
        """Get default branch name (main/master)"""
        # Try to get from remote
        success, output = self._run_git("symbolic-ref", "refs/remotes/origin/HEAD")
        if success:
            # Output is like "refs/remotes/origin/main"
            return output.split("/")[-1]
        
        # Fallback to common names
        for branch in ["main", "master"]:
            if self.branch_exists(branch):
                return branch
        
        return "main"
