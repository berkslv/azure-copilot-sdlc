# Project Overview

.NET CLI tool for Azure DevOps work item lifecycle automation using GitHub Copilot SDK and Spectre.Console.

## Core Capabilities
- **Plan**: Enrich Azure DevOps work items with AI-generated implementation plans
- **Develop**: Automatically implement features based on plans
- **Review**: AI-powered code review before PR merge

## Key Technologies
- Spectre.Console (CLI interface) - https://spectreconsole.net/console
- GitHub Copilot SDK (AI agents) - https://github.com/github/copilot-sdk/blob/main/dotnet/README.md
- Model Context Protocol (MCP) for Azure DevOps and filesystem access

# General Rules & Configuration

## Working Directory
- Run CLI in the current directory by default
- Accept `-d | --directory` parameter to specify working directory
- Validate that the directory is a valid git repository on startup. Show error: `[red]Error:[/red] Directory is not a git repository. Please run this command from within a git repository or specify a valid git directory with -d.`

## Custom Agent Discovery
- Search for GitHub Copilot custom agents in the provided directory
- Expected agent names:
  - `planner.agent.md` - for plan stage
  - `developer.agent.md` - for develop stage (NOTE: typo in original: "develoepr")
  - `reviewer.agent.md` - for review stage
- Search in these locations in order: `./.github/agents/`, `./agents/`, `./docs/agents/`, `./`. Use the first match found.
- If multiple agents with the same name are found, use the first one found in the search order above. Log a warning showing which agent was selected.
- **REQUIREMENT**: Tool must throw an error and exit gracefully if required custom agents are not found

## MCP Server Configuration

### Filesystem MCP
- use filesystem mcp to work files like in the example
  ### Filesystem MCP
```json
  "filesystem": {
    "command": "npx",
    "args": [
      "-y",
      "@modelcontextprotocol/server-filesystem",
      "/Users/username/Desktop",
      "/path/to/other/allowed/dir"
    ]
  }
```
- Dynamically configure filesystem paths to include the working directory (specified by -d or current directory). This ensures the agent can access the project files.
- Grant read-write permissions to enable the agent to create, modify, and delete files during development. Read-only would prevent implementation.

### Azure DevOps MCP
- **Environment Variable**: `AZURE_DEVOPS_PAT` - Personal Access Token for Azure DevOps
- **Environment Variable**: `AZURE_DEVOPS_ORG` - Organization name (can be auto-detected from git remote)
- **Environment Variable**: `GITHUB_PAT` - Personal Access Token for GitHub (used by GitHub Copilot SDK)
- If `AZURE_DEVOPS_PAT` not found in environment:
  - Prompt user: `Azure DevOps PAT not found. Please enter your PAT:` (input hidden with Spectre.Console)
  - Set environment variable for current session: `Environment.SetEnvironmentVariable("AZURE_DEVOPS_PAT", pat)`
  - Inform user: `[green]PAT stored for this session.[/green] To persist across sessions, add to your shell profile (~/.zshrc or ~/.bashrc): export AZURE_DEVOPS_PAT="your-pat"`
- If `GITHUB_PAT` not found in environment:
  - Prompt user: `GitHub PAT not found. Please enter your PAT:` (input hidden)
  - Set environment variable for current session
  - Inform user about persisting in shell profile
- Extract organization and project names from git remote URL if `AZURE_DEVOPS_ORG` not set
- Agent will use these via MCP to interact with Azure DevOps

```json
  {
    "mcpServers": {
      "azure-devops": {
        "command": "npx",
        "args": [
          "-y",
          "@azure-devops/mcp",
          "YOUR_ORG_NAME", 
          "--authentication", 
          "envvar"
        ],
        "env": {
          "ADO_MCP_AUTH_TOKEN": "YOUR_PAT_TOKEN_HERE"
        }
      }
    }
  }
```

# Plan Stage Workflow

**Command**: `azure-copilot plan <work-item-id> [-y|--yes] [-d|--directory <path>]`

## Process Flow

### 1. Input Validation & Retrieval
- Accept Azure DevOps work item ID as parameter
- validate that the work item ID is a positive integer. Show error if invalid format.
- Retrieve work item and contents using Azure DevOps MCP
- If work item doesn't exist, show error: `[red]Error:[/red] Work item #<id> not found.` Support all work item types (Bug, Task, User Story, Feature, Epic) - the tool should be agnostic to type.
- Check work item state. If state is "Completed", "Closed", or "Removed", warn user: `[yellow]Warning:[/yellow] Work item is already <state>. Continue anyway? (y/n)` and prompt for confirmation. if -y flag is provided, skip confirmation and proceed.

### 2. AI Plan Generation
- Use `planner.agent.md` custom agent to enrich work item with project context
- Use a base system prompt: "You are a technical planning assistant. Analyze the work item and project context to create a detailed implementation plan with User Story, Technical Implementation, Acceptance Criteria, and Test Paths sections. Be specific and actionable." Allow agent file to override or extend this.
- Provide project structure (tree), README files, and relevant source files based on the work item description keywords. Use semantic search to find related code. Limit to top 20 most relevant files to avoid context overflow.
- Limit plan to 4000 tokens (~3000 words). If exceeded, ask agent to be more concise and regenerate. Large plans are hard to review and implement.

#### Required Plan Structure
The agent must generate a plan with the following headers (in order):

1. **AI Plan** (top-level header)
2. **User Story** - What the user wants, the story of the work item
3. **Technical Implementation** - Search project, find correct places for development, create abstract development plan
   - include file paths and class names. Method signatures are helpful but not required. Be specific enough to guide implementation without being prescriptive.
   - Mid-level detail: architectural components, key classes/files to modify, new files to create, dependencies to add. Not pseudocode, but clear enough for a developer to follow.
4. **Acceptance Criteria** - Detailed acceptance criteria
   - Use testable/measurable criteria. Given-When-Then format is preferred but not required. Each criterion should be verifiable.
5. **Test Paths** - Manual testing steps to verify the requirement
   - Focus on manual testing steps here. Automated test suggestions can be mentioned briefly, but detailed test implementation belongs in the develop stage.

### 3. User Verification
- Display the generated plan in a Spectre.Console panel for user verification
- **Skip verification if**: `-y | --yes` flag is provided
- provide options: `(A)ccept`, `(R)eject`, `(E)dit`. For Edit, open plan in default editor (using $EDITOR env var or `nano`/`vim`), let user modify, then continue with modified version.
- If user rejects, prompt: `Would you like to (R)egenerate or (C)ancel?`. Regenerate reruns the agent (max 3 attempts), Cancel exits gracefully.

### 4. Save & Update
- Create comment with enriched plan on the Azure DevOps work item
- prefix comment with `# AI PLAN` tag for identification. Add timestamp and tool version in the tag.
- Check for existing plan comment (by searching for `# AI PLAN` tag). If found, update it instead of creating a new one. Keep history by appending "Updated on [date]" to the comment.
- Update Work Item state to "Active"
- validate state transitions using Azure DevOps API. If transition to "Active" is not valid, try "In Progress" or "Committed". If all fail, log a warning but don't fail the operation.
- No, only update the state. Preserve assigned user, iteration, and other fields. Changing assignment could interfere with team workflows.

## Error Handling
- Error handling strategies:
  - **Azure DevOps API unreachable**: Retry 3 times with exponential backoff (1s, 2s, 4s). If all fail, show error: `[red]Error:[/red] Cannot reach Azure DevOps API. Check your internet connection and try again.`
  - **Agent fails to generate plan**: Log the error, show user the error message, and ask if they want to retry or exit. Max 3 retries.
  - **Plan generation times out**: Set timeout to 5 minutes. If exceeded, show: `[red]Error:[/red] Plan generation timed out. Try with a smaller scope or check agent configuration.`
  - **Token lacks permissions**: Show error: `[red]Error:[/red] Your Azure DevOps PAT lacks permissions to update work item #<id>. Required: work items (read/write).` and exit.

# Develop Stage Workflow

**Command**: `azure-copilot develop <work-item-id> [-r|--with-review] [-d|--directory <path>]`

## Process Flow

### 1. Validation & Context Loading
- Read provided work item and all comments
- **Validate Plan Exists**: Check for specific headers (`Technical Implementation`, `Acceptance Criteria`)
- **Error Handling**: If headers missing, halt and display: `[red]Error:[/red] No plan found. Please run 'azure-copilot plan <id>' first.`
- Check for `Technical Implementation` and `Acceptance Criteria` as minimum required. `User Story` and `Test Paths` are helpful but not mandatory for development.
- Use case-insensitive fuzzy matching. Match if header contains the key words (e.g., "Technical Implementation" matches "## Technical Implementation Plan"). This handles variations in AI-generated headers.

### 2. Branch Management
- Create feature branch: `feature/<work-item-id>`
- If branch already exists, prompt: `[yellow]Warning:[/yellow] Branch feature/<id> already exists. (U)se existing, (D)elete and recreate, or (C)ancel?` Let user choose. Default to "Use existing" if -y flag is provided.
- Always start from the latest main/master branch. Fetch origin and reset/checkout main before creating feature branch. Show: `Syncing with origin/main...`
- Check for uncommitted changes with `git status --porcelain`. If found, show error: `[red]Error:[/red] You have uncommitted changes. Please commit or stash them first.` and exit. This prevents accidental loss of work.

### 3. Implementation
- Use `developer.agent.md` custom agent
- Agent reviews the plan and finds correct places to work
- Proceed autonomously without confirmation for each file unless `--interactive` flag is provided. Show a summary of planned changes before starting implementation.
- Use the file paths mentioned in Technical Implementation as scope. If not specified, limit to files discovered by semantic search based on work item description. Never modify files in node_modules, bin, obj, or other build directories.
- Perform development based on Technical Implementation plan
- Create one final commit after all development and tests pass. Intermediate commits complicate history. Agent should work atomically - either complete the feature or rollback.

### 4. Build & Test Cycle
- Run build after development
- Use `dotnet build` for .NET projects. Auto-detect by presence of .sln or .csproj files. Support other build systems (npm, gradle, etc.) also.
- Run tests after development
- Use `dotnet test` for .NET projects. Support configuration file to specify custom test commands for multi-language projects.
- **If failed**: Try again
  - Allow 3 retry attempts for build/test failures.
  - Agent should analyze error messages and fix specific issues incrementally. Only regenerate entire implementation if errors are fundamental (e.g., wrong architecture approach). Each retry should build on previous work.

### 5. Unit Test Generation
- Write unit tests based on `Acceptance Criteria`
- Use existing unit testing suite and examples
- Auto-detect the testing framework by scanning existing test projects for xUnit, NUnit, or MSTest references. Use the same framework found. If multiple found, use the most common one. If none found, default to xUnit (most popular in modern .NET).
- Auto-discover test project by convention: look for projects ending in .Tests, .UnitTests, .Test. If multiple found, prefer the one closest to the project being modified. If none found, create one following project conventions.
- Target 80% code coverage for new code. Don't enforce coverage for entire codebase. Use `dotnet test --collect:"XPlat Code Coverage"` to measure.
- Focus on unit tests. Generate integration tests only if the work item explicitly mentions integration scenarios or API endpoints. Integration tests are slower and more complex.

### 6. Commit & Push
- Once tests pass, stage all changes
- Commit with message: `#<id> implementation for item`
- include work item title: `#<id>: <work-item-title>` (truncate to 72 chars to follow git best practices).
- use conventional commit format: `feat: #<id> <work-item-title>` for features, `fix: #<id> <work-item-title>` for bugs. Detect type from work item type field in Azure DevOps.
- If commit fails (e.g., pre-commit hooks, GPG signing issues), show the error and ask: `[red]Commit failed.[/red] (R)etry, (S)kip hooks (--no-verify), or (A)bort?`. Never skip hooks by default.

### 7. Pull Request Creation
- Create PR in Azure DevOps to default branch (main/master) using Azure DevOps MCP
- Auto-detect default branch using `git remote show origin | grep 'HEAD branch'` or `git symbolic-ref refs/remotes/origin/HEAD`
- PR title: `#<id> <work-item-title>`
- PR description: Content from `# AI Plan` header with work item link
- Link PR to work item in Azure DevOps (associate work item with PR)
- Push feature branch to origin before creating PR

### 8. Optional Review Stage
- If `-r | --with-review` flag provided, proceed to review stage automatically
- Show a 5-second countdown: `Starting review stage in 5 seconds... (Press Ctrl+C to cancel)` This gives users a chance to abort if they want to manually review first.

## Error Handling
- Error handling strategies:
  - **Developer agent produces invalid code**: Build/test failures will catch this. After max retries, prompt user for manual intervention.
  - **Git operations fail**: Show specific error (permissions, network, conflicts) and suggest remediation. For network: retry. For permissions: check SSH keys/credentials. Exit gracefully.
  - **PR already exists**: Show warning with PR URL: `[yellow]Warning:[/yellow] PR already exists: <url>. Do you want to (U)pdate it or (C)ancel?` Update PR by pushing to existing branch and updating PR description via Azure DevOps MCP.
  - **Tests never pass after retries**: Ask user: `(C)ommit anyway and create PR with failing tests (for manual review), or (A)bort?` Document test failures in PR description.
  - **Repository is in dirty state**: Already handled in step 2 - prevent operation from starting. Always check git status before making changes.

# Review Stage Workflow

**Command**: `azure-copilot review <work-item-id> [-d|--directory <path>]`

## Purpose
Acts as the gatekeeper before PR merge. Simulates a senior developer reviewing the code to ensure quality, best practices, and alignment with requirements.

## Process Flow

### 1. Branch Discovery
- Search for the feature branch: `feature/<work-item-id>`
- If branch doesn't exist locally or remotely, show error: `[red]Error:[/red] Branch feature/<id> not found. Please run 'azure-copilot develop <id>' first.` and exit.
- Use exact match `feature/<work-item-id>` only. If multiple matches found somehow (shouldn't happen with exact match), use the one with most recent commit.
- Yes, verify branch has commits: `git rev-list --count feature/<id>`. If 0 commits, show error: `[red]Error:[/red] Branch feature/<id> is empty. Nothing to review.`

### 2. Context Gathering
- Retrieve Work Item and all associated comments
- Look for the "Enriched Plan" comment from the Plan stage
- If plan comment is missing, show warning but proceed with generic review using work item description and title. Log: `[yellow]Warning:[/yellow] No AI plan found. Review will be based on work item description only.`
- Load requirements and technical implementation from the plan

### 3. Code Review Using AI Agent
- Use `reviewer.agent.md` custom agent
- Agent performs code review against:
  - Requirements from the plan
  - Technical Implementation details
  - Best practices and project standards
  - Code quality, maintainability, and performance
- Review all aspects in this priority order:
  1. **Security vulnerabilities** (HIGH) - SQL injection, XSS, hardcoded secrets, insecure dependencies
  2. **Correctness** (HIGH) - Does it meet acceptance criteria? Any logic errors?
  3. **Test coverage** (HIGH) - Are tests comprehensive? Do they cover edge cases?
  4. **Performance issues** (MEDIUM) - N+1 queries, inefficient algorithms, memory leaks
  5. **Code quality** (MEDIUM) - Readability, maintainability, DRY principle
  7. **Code style** (MEDIUM) - Formatting, naming (should be handled by linters)
  6. **Documentation** (LOW) - Comments, XML docs, README updates
- **ANSWER**: Yes, run static analysis first: dotnet format (style), Security Code Scan (security), SonarLint if available. AI review should focus on logic and architecture that tools can't catch.

### 4. Issue Resolution
- When finding weaknesses or possible improvements, create new commits to the feature branch
- Create one commit for all issue resolutions per review iteration
- Use format: `review: <category> - <brief description>` (e.g., `review: security - fix SQL injection in user query`). Keep commit messages descriptive.
- Allow max 3 review iterations. If issues remain after 3 rounds, escalate to human: `[yellow]Unable to resolve all issues automatically. Manual review required.[/yellow]`
- Classify issues by severity:
  - **Critical**: Security vulnerabilities, data loss risks - must fix all
  - **Major**: Incorrect logic, missing tests - must fix all
  - **Minor**: Code style, minor performance - fix if easy, otherwise document in PR
  Code is "good enough" when no Critical/Major issues remain. Minor issues can be accepted.
- Ensure all changes align with best practices and project standards

### 5. Final Actions
- After review is complete:
  - **DO NOT auto-merge** - leave that to human reviewers or branch policies
  - **Update PR description** - add review summary section at the bottom listing what was fixed (via Azure DevOps MCP)
  - **Add PR comment** - summarize review results: "AI Review Complete: X issues found and fixed. Ready for human review." (via Azure DevOps MCP)
  - **Add work item comment** - post review summary with link to PR
- Update Azure DevOps work item state to "In Review" if that state exists in the workflow. Don't mark "Completed" - that's for after merge.

## Error Handling
- Error handling strategies:
  - **Critical issues can't be auto-fixed**: Document them clearly in PR comment and work item. Mark PR as "needs work" with specific issues listed. Show: `[red]Critical issues require manual attention:[/red]` followed by issue list. Don't proceed to completion.
  - **Review iterations exceed maximum**: Add comment to PR: "AI review completed with remaining issues. See comments for details." and update work item with remaining issues. Let humans take over.
  - **Feature branch conflicts with main/master**: Attempt auto-merge using `git merge origin/main`. If conflicts, show: `[red]Merge conflicts detected.[/red] Please resolve conflicts manually and run review again.` List conflicting files. Don't proceed.
  - **Reviewer agent crashes/times out**: Retry once. If fails again, log error, add comment to PR: "AI review failed to complete. Manual review required." and exit gracefully with error code.

---

# Technical Implementation Plan

## Architecture Overview

The tool follows a **lightweight orchestration pattern** where the CLI acts as a thin coordinator that:
1. Discovers and loads custom agents
2. Configures MCP servers (filesystem + Azure DevOps)
3. Builds system prompts with context
4. Executes agents via GitHub Copilot SDK
5. Handles user interaction and error flows

**Agents perform all actual work** via MCP and CLI tools - the C# code doesn't interact directly with Azure DevOps or Git.

## Project Structure

```
Copiloter.CLI/
├── Program.cs                      # CLI entry point, command routing
├── Commands/
│   ├── PlanCommand.cs
│   ├── DevelopCommand.cs
│   └── ReviewCommand.cs
├── Services/
│   ├── AgentDiscoveryService.cs    # Find agent markdown files
│   ├── CopilotAgentService.cs      # Execute agents via SDK
│   └── McpConfigurationService.cs  # Configure MCP servers
├── Models/
│   ├── AgentConfig.cs              # Agent metadata
│   └── AIPlan.cs                   # Parsed plan structure
└── Utilities/
    ├── ConsoleHelper.cs            # Spectre.Console UI
    └── PlanParser.cs               # Markdown plan validation
```

## Core Dependencies

```xml
<PackageReference Include="Spectre.Console" />     <!-- CLI UI -->
<PackageReference Include="GitHub.Copilot.SDK" />  <!-- Agent execution -->
<PackageReference Include="System.CommandLine" />  <!-- Command parsing -->
```

**Runtime Requirements:**
- Node.js (npx) - for MCP servers
- Git - agents use git CLI
- Azure DevOps PAT in environment variable

## Key Components

### 1. AgentDiscoveryService
**Purpose**: Locate and load custom agent files

**Responsibilities:**
- Search predefined paths for `{agent-name}.agent.md` files
- Parse agent markdown
- Cache loaded agents
- Validate agent exists or error

### 2. CopilotAgentService
**Purpose**: Execute agents via GitHub Copilot SDK

**Responsibilities:**
- Initialize SDK session
- Configure MCP servers (filesystem, Azure DevOps)
- Build system prompts from templates + context
- Execute agent with prompt
- Stream/capture agent responses
- Handle timeouts and errors

### 3. McpConfigurationService
**Purpose**: Generate MCP server configurations

**Responsibilities:**
- Create filesystem MCP config pointing to working directory
- Create Azure DevOps MCP config with org/project/PAT from environment
- Extract org/project from git remote URL
- Validate prerequisites (npx available)

### 4. ConsoleHelper
**Purpose**: User interaction via Spectre.Console

**Responsibilities:**
- Display formatted messages (errors, warnings, info)
- Show plan in panel for review
- Prompt for user decisions (Accept/Reject/Edit)
- Progress indicators for long-running operations
- Countdown timers

### 5. PlanParser
**Purpose**: Validate agent-generated plans

**Responsibilities:**
- Parse markdown to extract sections
- Fuzzy match headers (case-insensitive)
- Validate required sections present
- Return structured AIPlan model

## Command Workflows

### Plan Command
```
User runs: azure-copilot plan 123
  ↓
Validate: git repo, work item ID format, AZURE_DEVOPS_PAT exists
  ↓
Discover: planner.agent.md
  ↓
Configure: Filesystem + Azure DevOps MCP
  ↓
Build Prompt: "Retrieve work item #123, analyze codebase, generate plan"
  ↓
Execute: Planner agent (agent does everything via MCP)
  ↓
Validate: Parse response, check headers
  ↓
User Review: Display plan, Accept/Reject/Edit (unless -y)
  ↓
Save: Send plan back to agent with "save to work item" instruction
  ↓
Agent: Creates/updates comment, updates work item state
```

### Develop Command
```
User runs: azure-copilot develop 123 -r
  ↓
Validate: git repo, AZURE_DEVOPS_PAT exists
  ↓
Discover: developer.agent.md
  ↓
Configure: Filesystem + Azure DevOps MCP
  ↓
Build Prompt: "Implement work item #123 following plan"
  Include instructions for: git branch, commit, build, test, PR creation
  ↓
Execute: Developer agent (agent does everything autonomously)
  ↓
Agent workflow:
  - Retrieves work item + plan via MCP
  - Creates branch via git CLI
  - Implements code via filesystem MCP
  - Runs build/test via CLI
  - Commits/pushes via git CLI
  - Creates PR via Azure DevOps MCP
  ↓
If -r flag: Wait 5 seconds, proceed to review
```

### Review Command
```
User runs: azure-copilot review 123
  ↓
Validate: git repo, AZURE_DEVOPS_PAT exists
  ↓
Discover: reviewer.agent.md
  ↓
Configure: Filesystem + Azure DevOps MCP
  ↓
Build Prompt: "Review feature/123 branch against plan"
  Include priority order, issue classification, fix instructions
  ↓
Execute: Reviewer agent (agent does everything autonomously)
  ↓
Agent workflow:
  - Validates branch exists via git CLI
  - Retrieves plan via Azure DevOps MCP
  - Runs static analysis via CLI (dotnet format, etc.)
  - Reviews code via filesystem MCP
  - Classifies issues (Critical/Major/Minor)
  - Fixes issues, commits via git CLI
  - Updates PR description/comments via MCP
  - Updates work item state via MCP
  ↓
Display: Review summary to user
```

## Agent System Prompts

Each command builds a system prompt that instructs the agent what to do. The prompt includes:
- **Context**: Work item ID, org, project, working directory
- **Available Tools**: What MCPs and CLIs the agent can use
- **Task**: Step-by-step instructions
- **Constraints**: Max retries, token limits, required formats
- **Custom Agent Content**: Loaded from agent markdown file

Example prompt structure:
```
{base_instructions}

Context:
- Work Item: #{id}
- Organization: {org}
- Project: {project}

Available via MCP:
- Azure DevOps: Query work items, PRs, comments
- Filesystem: Read/write project files

Available via CLI:
- git: All git commands
- dotnet: build, test, etc.

Task:
{specific_task_instructions}

Output Format:
{expected_format}

{agent_custom_instructions_from_markdown}
```

## Error Handling Strategy

**Validation Errors** (pre-flight):
- Missing environment variables → Exit with clear error
- Invalid git repo → Exit with error
- Missing custom agents → Exit with error
- Invalid work item ID → Exit with error

**Runtime Errors**:
- MCP connection failures → Retry 3x with backoff, then exit
- Agent execution timeout (5 min) → Exit with timeout error
- Agent returns invalid format → Show error, prompt retry (max 3)
- Agent reports failure → Display agent's error message to user

**User Decisions**:
- Plan rejected → Offer regenerate (max 3) or cancel
- Build/test failures → Agent retries internally (max 3), then reports
- Critical issues unfixable → Agent documents in PR, escalates to human

## Development Phases

### Phase 1: Foundation
- Project setup, dependencies
- Command line parsing with System.CommandLine
- ConsoleHelper with Spectre.Console
- Basic validation utilities

### Phase 2: Agent Infrastructure
- AgentDiscoveryService implementation
- CopilotAgentService with GitHub Copilot SDK
- McpConfigurationService
- System prompt templates

### Phase 3: Plan Command
- PlanCommand implementation
- PlanParser for validation
- User interaction flow (Accept/Reject/Edit)
- Agent execution and response handling

### Phase 4: Develop Command
- DevelopCommand implementation
- Developer agent system prompt
- Progress monitoring
- Optional review transition

### Phase 5: Review Command
- ReviewCommand implementation
- Reviewer agent system prompt
- Review summary display

### Phase 6: Polish
- Error handling refinement
- Logging and diagnostics
- Documentation
- Testing with real agents

## Security Considerations

**Environment Variables:**
- PATs must be in environment, never stored by tool
- User responsible for securing their environment

**Agent Permissions:**
- Filesystem MCP: Read/write access to working directory only
- Azure DevOps MCP: Scoped by PAT permissions
- Git CLI: Can modify repository
- Build/Test CLI: Can execute commands

**Mitigation:**
- Clear documentation of agent capabilities
- User reviews plans before execution (unless -y)
- Agent actions logged for audit

## Success Criteria

**Plan Command**: Agent generates valid plan, saves to work item
**Develop Command**: Agent implements feature, creates PR, links work item
**Review Command**: Agent reviews code, fixes issues, updates PR

**All Commands**:
- Clear error messages
- Graceful handling of failures
- User maintains control via prompts (unless -y)
- Agent actions are transparent and logged
