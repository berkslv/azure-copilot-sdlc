# Azure DevOps Copilot SDLC

AI-powered work item lifecycle automation using GitHub Copilot CLI and Azure DevOps.

## Features

- **Plan**: Generate AI implementation plans for work items
- **Develop**: Automatically implement features, create branches, and submit PRs
- **Review**: AI-powered code review with automated fixes

## Prerequisites

- Python 3.12+
- uv package manager
- Node.js (for MCP servers)
- Git
- GitHub Copilot CLI
- Azure DevOps PAT

## Installation

```bash
cd src
uv sync
```

## Quick Start

### 1. Configure Credentials

Create `~/.azure-copilot-sdlc/.env` (or `%USERPROFILE%\.azure-copilot-sdlc\.env` on Windows):

```env
ADO_MCP_AUTH_TOKEN=your_azure_devops_pat
GITHUB_PAT=your_github_pat
```

Or let the tool prompt you on first run.

### 2. Create Agent Files

In your target repository, create these files in `.github/agents/` (or `agents/` or `docs/agents/`):
- `planner.agent.md` - Plan generation instructions
- `developer.agent.md` - Code implementation guidelines  
- `reviewer.agent.md` - Code review criteria

## Usage

### Plan a Work Item

```bash
uv run azure-copilot-sdlc plan 123 -d /path/to/repo
uv run azure-copilot-sdlc plan 123 -d /path/to/repo -m gpt-4
```

Generates an implementation plan and saves it as a comment to the work item.

**Options:**
- `-m, --model`: LLM model to use (e.g., `gpt-5-mini`, `gpt-4`, `gpt-4-turbo`). Defaults to `gpt-5-mini`.

### Develop a Feature

```bash
uv run azure-copilot-sdlc develop 123 -d /path/to/repo
uv run azure-copilot-sdlc develop 123 -d /path/to/repo -m gpt-4
uv run azure-copilot-sdlc develop 123 -d /path/to/repo -r -m gpt-4
```

Creates a feature branch, implements the code, and creates a pull request.

**Options:**
- `-r, --with-review`: Run code review after development
- `-m, --model`: LLM model to use (e.g., `gpt-5-mini`, `gpt-4`, `gpt-4-turbo`). Defaults to `gpt-5-mini`.

### Review Code

```bash
uv run azure-copilot-sdlc review 123 -d /path/to/repo
uv run azure-copilot-sdlc review 123 -d /path/to/repo -m gpt-4
```

Performs AI code review with automated fixes for critical issues.

**Options:**
- `-m, --model`: LLM model to use (e.g., `gpt-5-mini`, `gpt-4`, `gpt-4-turbo`). Defaults to `gpt-5-mini`.