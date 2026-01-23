# Azure DevOps Copilot SDLC

AI-powered work item lifecycle automation using GitHub Copilot SDK and Azure DevOps.

## Features

- **Plan**: Generate AI implementation plans for Azure DevOps work items
- **Develop**: Automatically implement features based on AI plans
- **Review**: AI-powered code review before PR merge

## Prerequisites

- .NET 10.0 or later
- Node.js (for MCP servers)
- Git
- GitHub Copilot CLI installed and authenticated
- Azure DevOps PAT with appropriate permissions

## Installation

```bash
cd src/Copiloter.CLI
dotnet build
```

## Configuration

### Credentials Configuration

On first run, the tool will prompt you for the required credentials and save them to a `.azure-copilot` configuration file in your home directory.

Required credentials:
- **Azure DevOps PAT**: Personal Access Token with work items read/write permissions
- **GitHub PAT**: Personal Access Token for GitHub Copilot SDK
- **Azure DevOps Organization**: Your organization name (auto-detected from git remote if possible)

The configuration file is stored at:
- **Linux/macOS**: `~/.azure-copilot`
- **Windows**: `C:\Users\<username>\.azure-copilot`

You can also manually create or edit the configuration file:

```json
{
  "azureDevOpsPat": "your-azure-devops-pat",
  "githubPat": "your-github-pat",
  "azureDevOpsOrg": "your-organization"
}
```

**Security Note**: Keep this file secure and never commit it to version control. Add `.azure-copilot` to your `.gitignore`.

### Legacy Environment Variables (Optional)

The tool still supports environment variables, but the `.azure-copilot` file is the preferred method:

```bash
export AZURE_DEVOPS_PAT="your-azure-devops-pat"
export AZURE_DEVOPS_ORG="your-organization"  # Optional - auto-detected from git remote
export GITHUB_PAT="your-github-pat"          # For GitHub Copilot SDK
```

### Custom Agents

Create custom agent files in one of these locations (in order of precedence):
- `.github/agents/`
- `agents/`
- `docs/agents/`
- `.` (root directory)

Required agent files:
- `planner.agent.md` - For the plan command
- `developer.agent.md` - For the develop command
- `reviewer.agent.md` - For the review command

## Usage

### Plan Command

Generate an AI implementation plan for a work item:

```bash
dotnet run plan <work-item-id> [options]

Options:
  -y, --yes              Skip confirmation prompts
  -d, --directory <dir>  Working directory (default: current directory)
```

Example:
```bash
dotnet run plan 1234
dotnet run plan 1234 -y -d /path/to/project
```

### Develop Command

Implement a feature based on the AI plan:

```bash
dotnet run develop <work-item-id> [options]

Options:
  -r, --with-review      Automatically proceed to review stage after development
  -d, --directory <dir>  Working directory (default: current directory)
```

Example:
```bash
dotnet run develop 1234
dotnet run develop 1234 -r  # With automatic review
```

### Review Command

Perform AI-powered code review:

```bash
dotnet run review <work-item-id> [options]

Options:
  -d, --directory <dir>  Working directory (default: current directory)
```

Example:
```bash
dotnet run review 1234
```

## How It Works

### Plan Workflow

1. Validates git repository and prerequisites
2. Discovers `planner.agent.md` custom agent
3. Executes agent to:
   - Retrieve work item from Azure DevOps via MCP
   - Analyze project structure via filesystem MCP
   - Generate implementation plan with required sections:
     - User Story
     - Technical Implementation
     - Acceptance Criteria
     - Test Paths
4. User reviews and optionally edits the plan
5. Saves plan to work item as comment with `# AI PLAN` tag
6. Updates work item state to "Active"

### Develop Workflow

1. Discovers `developer.agent.md` custom agent
2. Executes agent to:
   - Retrieve work item and plan from Azure DevOps
   - Create feature branch: `feature/<work-item-id>`
   - Implement code based on Technical Implementation
   - Build and test (with automatic retries on failure)
   - Generate unit tests based on Acceptance Criteria
   - Commit and push changes
   - Create PR in Azure DevOps
   - Link PR to work item
3. Optionally proceeds to review stage if `-r` flag is provided

### Review Workflow

1. Discovers `reviewer.agent.md` custom agent
2. Executes agent to:
   - Validate feature branch exists
   - Retrieve work item plan
   - Run static analysis (dotnet format, security scanners)
   - Review code for:
     - Security vulnerabilities (Critical)
     - Correctness (Critical)
     - Test coverage (Critical)
     - Performance issues (Major)
     - Code quality (Major)
     - Documentation (Minor)
   - Fix Critical/Major issues automatically (max 3 iterations)
   - Update PR with review summary
   - Add comments to PR and work item
   - Update work item state to "In Review"

## Architecture

The tool follows a lightweight orchestration pattern:

- **CLI** (Program.cs): Routes commands using System.CommandLine
- **Commands**: Plan, Develop, Review - orchestrate workflows
- **Services**: 
  - `AgentDiscoveryService`: Locates custom agent files
  - `McpConfigurationService`: Configures MCP servers
  - `CopilotAgentService`: Executes agents via GitHub Copilot SDK
- **Utilities**:
  - `ConsoleHelper`: Spectre.Console UI operations
  - `PlanParser`: Validates and parses AI-generated plans
- **Models**: AgentConfig, AIPlan - domain objects

**Agents perform all actual work** - The C# code doesn't interact directly with Azure DevOps or Git. Agents use:
- Azure DevOps MCP for work item operations
- Filesystem MCP for code changes
- CLI tools (git, dotnet) for version control and builds

## Troubleshooting

### "npx is not available"
Install Node.js from https://nodejs.org/

### "Agent 'X' not found"
Create the required agent markdown files in one of the search locations.

### "AZURE_DEVOPS_PAT environment variable not set"
Set your PAT or the tool will prompt you for it (session-only).

### "Directory is not a git repository"
Run the command from within a git repository or use `-d` to specify one.

## License

MIT