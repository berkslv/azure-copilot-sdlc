# azure-copilot-sdlc

Azure DevOps work item lifecycle automation using GitHub Copilot CLI.

## Installation

```bash
cd src
uv sync
```

## Quick Start

### Development (with `uv run`)

Run commands directly without building executables:

```bash
# View all available commands
uv run azure-copilot-sdlc --help

# Plan a work item
uv run azure-copilot-sdlc plan execute 123

# Develop a feature
uv run azure-copilot-sdlc develop execute 123

# Review code changes
uv run azure-copilot-sdlc review execute 123
```

### Production (Compiled Executable)

Build a standalone executable to distribute:

```bash
# Build single-file executable (recommended)
uv run python build.py

# Run the executable
./dist/azure-copilot-sdlc --help
./dist/azure-copilot-sdlc plan execute 123
```

For more build options, see [BUILD.md](BUILD.md).

## Usage

### Plan
Generate an AI implementation plan for a work item:

```bash
uv run azure-copilot-sdlc plan execute 123
uv run azure-copilot-sdlc plan execute 123 -d /path/to/repo
uv run azure-copilot-sdlc plan execute 123 -y  # Skip confirmations
```

### Develop
Implement a feature based on the plan:

```bash
uv run azure-copilot-sdlc develop execute 123
uv run azure-copilot-sdlc develop execute 123 -d /path/to/repo
```

### Review
Review code changes:

```bash
uv run azure-copilot-sdlc review execute 123
uv run azure-copilot-sdlc review execute 123 -d /path/to/repo
```

## Configuration

### Environment Variables

Create `.env` in your user home directory:

**Windows:** `%USERPROFILE%\.azure-copilot-sdlc\.env`
**Linux/macOS:** `~/.azure-copilot-sdlc/.env`

Required variables:
```
ADO_MCP_AUTH_TOKEN=your_pat_token
GITHUB_PAT=your_github_token
```

Optional:
```
AZURE_DEVOPS_ORG=your_organization  # Leave blank to be prompted
```

The directory is created automatically on first run.

## Architecture

- **cli.py**: Main entry point with banner and environment loading
- **commands/**: CLI command implementations (plan, develop, review)
- **services/**: Business logic (agent discovery, copilot execution, MCP config, git operations)
- **models/**: Data structures
- **utilities/**: Helper functions, validators, and configuration loading

## Building Executables

See [BUILD.md](BUILD.md) for detailed build instructions.

Quick reference:
```bash
# Build single-file executable
uv run python build.py

# Build directory-based version (with all dependencies)
uv run python build.py --onedir

# Clean build artifacts
uv run python build.py --clean
```

## Project Structure

```
src/
├── cli.py                 # Main entry point
├── commands/              # Command implementations
├── services/              # Business logic
├── models/                # Data structures
├── utilities/             # Helpers and config
├── build.py               # PyInstaller build script
├── azure-copilot-sdlc.spec  # Build configuration
├── pyproject.toml         # Project metadata
├── .env.example           # Example environment file
├── BUILD.md              # Build documentation
└── README.md             # This file
```

## Requirements

- Python >= 3.12
- Node.js (for MCP servers)
- Git
- Copilot CLI (installed globally or available in PATH)
- UV package manager (for `uv run` and `uv sync`)

## Dependencies

All dependencies are defined in `pyproject.toml`:
- **typer** - CLI framework
- **rich** - Colorful terminal output
- **python-dotenv** - Environment variable loading
- **requests** - HTTP client
- **gitpython** - Git operations
- **pyinstaller** (dev) - Executable creation

## Troubleshooting

### Missing environment variables
The CLI will prompt for missing required variables (ADO_MCP_AUTH_TOKEN, GITHUB_PAT).

### Command not found
Ensure you're in the `src/` directory and have run `uv sync`.

### Build fails
See [BUILD.md](BUILD.md) troubleshooting section.

## License

See LICENSE file
