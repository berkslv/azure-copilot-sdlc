# Azure DevOps Copilot SDLC

AI-powered work item lifecycle automation using GitHub Copilot CLI and Azure DevOps.

## Overview

This tool automates the plan → develop → review cycle for Azure DevOps work items by driving GitHub Copilot agents programmatically. Each stage reads a custom agent file from your repository to tailor AI behavior to your project conventions.

```
Work Item → Plan → Develop → Review → Pull Request
```

## Prerequisites

| Requirement | Version |
|---|---|
| Python | 3.12+ |
| uv | latest |
| Node.js | for MCP servers |
| Git | any |
| GitHub Copilot CLI | authenticated |

You also need:
- An **Azure DevOps PAT** with work item read/write and code read permissions
- A **GitHub PAT** with repo permissions (for PR creation)

## Installation

```bash
cd src
uv sync
```

## Configuration

Credentials are stored in `~/.azure-copilot-sdlc/.env` (created automatically on first run):

```env
ADO_MCP_AUTH_TOKEN=your_azure_devops_pat
GITHUB_PAT=your_github_pat
AZURE_DEVOPS_PROJECT=your_project_name
```

If any variable is missing, the tool will prompt for it interactively and save it for future runs.

## Agent Files

Each command requires a Markdown agent file in your **target repository**. The tool searches these paths in order:

```
.github/agents/
agents/
docs/agents/
./
```

| Command | File |
|---|---|
| `plan` | `planner.agent.md` |
| `develop` | `developer.agent.md` |
| `review` | `reviewer.agent.md` |

Agent files define how GitHub Copilot should approach each stage (coding standards, tech stack, review criteria, etc.). Create them once per repository.

## Usage

All commands accept a work item ID and an optional `--directory` pointing to your target repository.

### Plan

Generate an implementation plan and save it as a comment on the work item.

```bash
uv run azure-copilot-sdlc plan <work-item-id> -d /path/to/repo
uv run azure-copilot-sdlc plan <work-item-id> -d /path/to/repo -m gpt-4
```

The plan is structured with: User Story, Questions, Technical Implementation, Acceptance Criteria, and Test Paths. It is saved directly to the work item as a `# COPILOT PLAN` comment and the work item state is set to Active.

### Develop

Create a feature branch, implement the work item, run tests, and open a pull request.

```bash
uv run azure-copilot-sdlc develop <work-item-id> -d /path/to/repo
uv run azure-copilot-sdlc develop <work-item-id> -d /path/to/repo -r        # with review
uv run azure-copilot-sdlc develop <work-item-id> -d /path/to/repo -m gpt-4
```

**Options:**
- `-r, --with-review` — run `review` automatically after development
- `-m, --model` — LLM model (default: `gpt-5-mini`)

The feature branch is named `feature/<work-item-id>`. If the branch already exists, you are prompted to reuse or recreate it. Your working tree must be clean before running this command.

### Review

Review code changes on the feature branch and produce a prioritized findings report.

```bash
uv run azure-copilot-sdlc review <work-item-id> -d /path/to/repo
uv run azure-copilot-sdlc review <work-item-id> -d /path/to/repo -m gpt-4
```

The reviewer checks security, correctness, test coverage, performance, code quality, and design patterns. Each finding includes severity (Critical / High / Medium / Low), file location, description, and suggested fix.

**Options:**
- `-m, --model` — LLM model (default: `gpt-5-mini`)

## Building a Standalone Executable

Use PyInstaller to produce a single binary (no Python required on the target machine):

```bash
cd src

# Single-file executable (default)
python build.py

# Directory-based build
python build.py --onedir

# Clean artifacts only
python build.py --clean
```

Output is placed in `src/dist/azure-copilot-sdlc` (Linux/Mac) or `src/dist/azure-copilot-sdlc.exe` (Windows).

## Project Structure

```
src/
├── cli.py              # Entry point and command registration
├── commands/
│   ├── plan.py         # plan command
│   ├── develop.py      # develop command
│   └── review.py       # review command
├── services/
│   ├── agent_discovery.py   # Locates agent .md files in the repo
│   ├── copilot_agent.py     # Executes GitHub Copilot CLI
│   ├── git_service.py       # Branch management
│   └── mcp_configuration.py # MCP server setup
├── utilities/
│   ├── config.py       # .env loading and credential prompting
│   ├── validators.py   # Input validation
│   └── console_helper.py    # Rich console output
└── models/             # Data models (AgentConfig, etc.)
```
