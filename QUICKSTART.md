# Quick Start Guide

This guide will help you get started with the Azure DevOps Copilot SDLC tool quickly.

## 1. Prerequisites Check

Before you start, ensure you have:

- ✅ .NET 10.0 SDK installed
- ✅ Node.js and npx installed (for MCP servers)
- ✅ Git configured
- ✅ Access to an Azure DevOps project
- ✅ GitHub Copilot subscription

## 2. First-Time Setup

### Configure Credentials

The tool will automatically prompt you for credentials on first run. Just run any command and follow the prompts:

```bash
cd src/Copiloter.CLI
dotnet run plan 1234
```

You'll be prompted for:
- **Azure DevOps PAT**: Personal Access Token (requires: Work Items, Code, Pull Requests permissions)
- **GitHub PAT**: Personal Access Token (requires: Copilot access)
- **Azure DevOps Organization**: Your org name (auto-detected from git remote if possible)

The credentials are saved to `~/.azure-copilot` (Linux/macOS) or `C:\Users\<username>\.azure-copilot` (Windows).

### Manual Configuration (Optional)

You can also manually create the configuration file:

**Linux/macOS**: `~/.azure-copilot`
**Windows**: `C:\Users\<username>\.azure-copilot`

```json
{
  "azureDevOpsPat": "your-azure-devops-pat-here",
  "githubPat": "your-github-pat-here",
  "azureDevOpsOrg": "your-organization-name"
}
```

### Get Your Personal Access Tokens

#### Azure DevOps PAT
1. Go to https://dev.azure.com/{your-org}
2. Click User Settings → Personal Access Tokens
3. Create new token with these scopes:
   - **Work Items**: Read, Write
   - **Code**: Read, Write
   - **Pull Requests**: Read, Write, Manage

#### GitHub PAT
1. Go to https://github.com/settings/tokens
2. Create new classic token with `copilot` scope
3. Or use fine-grained token with Copilot access

## 3. Install MCP Servers

```bash
# Filesystem MCP (included with Copilot CLI)
# No installation needed

# Azure DevOps MCP
npm install -g @azure-devops/mcp
```

## 4. Customize Agents (Optional)

The tool includes default agent files in the `agents/` directory. You can customize them:

- [planner.agent.md](agents/planner.agent.md) - Planning instructions
- [developer.agent.md](agents/developer.agent.md) - Development guidelines
- [reviewer.agent.md](agents/reviewer.agent.md) - Review criteria

**Customization ideas:**
- Add project-specific coding standards
- Configure security scanning tools
- Set performance requirements
- Define documentation requirements

## 5. Build the Tool

```bash
cd src/Copiloter.CLI
dotnet build
```

## 6. Run Your First Command

### Create a Plan

```bash
# Navigate to your project repository
cd /path/to/your/repo

# Run the plan command with a work item ID
dotnet run --project /path/to/Copiloter.CLI plan 1234
```

**What happens:**
1. Tool fetches work item #1234 from Azure DevOps
2. AI analyzes the requirements
3. Generates a detailed implementation plan
4. Shows you the plan for review
5. Saves plan back to work item description

### Implement the Plan

```bash
# From your project repository
dotnet run --project /path/to/Copiloter.CLI develop 1234
```

**What happens:**
1. Reads the plan from work item #1234
2. Creates a feature branch
3. Implements the code
4. Writes tests
5. Commits and pushes changes
6. Creates a pull request

### Review the Code

```bash
# Review the implementation
dotnet run --project /path/to/Copiloter.CLI review 1234
```

**What happens:**
1. Checks out the feature branch
2. Reviews all changes
3. Runs static analysis
4. Fixes critical issues automatically
5. Updates PR with review comments

### Or Do It All at Once

```bash
# Plan + Develop + Review in one command
dotnet run --project /path/to/Copiloter.CLI plan 1234 -y
dotnet run --project /path/to/Copiloter.CLI develop 1234 -r
```

## 7. Common Issues

### "npx not found"
```bash
# Install Node.js (includes npx)
brew install node  # macOS
```

### "Could not find agent file"
```bash
# Agents must be in one of these locations:
mkdir -p .github/agents
# OR
mkdir -p agents
# OR  
mkdir -p docs/agents
```

### "Invalid personal access token"
```bash
# Check your token hasn't expired
# Verify scopes include Work Items, Code, Pull Requests
# Re-export environment variable
export AZURE_DEVOPS_PAT="new-token"
```

### "Failed to execute agent"
```bash
# Verify GitHub Copilot subscription is active
# Check GITHUB_PAT has copilot scope
# Try re-authenticating: gh auth login
```

## 8. Tips for Success

### Start Small
Test with a simple work item first to verify everything works.

### Review AI Plans
Always review the generated plan before proceeding to development. Use `-y` flag only after you're comfortable with the results.

### Customize Agents
The default agents are generic. Customize them with your:
- Coding standards
- Architecture patterns
- Security requirements
- Testing strategies

### Use Auto-Review
The `-r` flag on `develop` command automatically runs review after implementation, saving you a command.

### Monitor Agent Execution
Agents can take several minutes, especially for complex work items. The tool shows progress indicators.

## 9. Next Steps

- Customize agent files for your project
- Set up CI/CD to run on work item state changes
- Create custom agents for specialized tasks
- Integrate with team workflows

## Need Help?

See the [main README](README.md) for detailed documentation, architecture explanation, and troubleshooting guide.
