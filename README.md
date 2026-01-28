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
```

Generates an implementation plan and saves it as a comment to the work item.

### Develop a Feature

```bash
uv run azure-copilot-sdlc develop 123 -d /path/to/repo
```

Creates a feature branch, implements the code, and creates a pull request.

### Review Code

```bash
uv run azure-copilot-sdlc review 123 -d /path/to/repo
```

Performs AI code review with automated fixes for critical issues.