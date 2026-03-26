# Claude Code Setup Guide for Tab Bridge

This guide walks you through setting up your local development environment for Tab Bridge with Claude Code, optimized for maximum productivity and automation.

## Prerequisites

- Windows 11 with Visual Studio 2026 Enterprise
- VS Code with C# Dev Kit extension
- .NET 10 SDK installed (`dotnet --version` → 10.x)
- Claude Code installed and authenticated (`claude --version`)
- Claude Pro subscription (Sonnet for routine tasks, Opus for complex architecture)
- Git installed, GitHub CLI (`gh`) recommended
- Waterfox 6.6.x installed with at least 2 Selectable Profiles configured

## Step 1: Create the GitHub repository

```powershell
# Create repo on GitHub with MPL 2.0 license
gh repo create relexx/tb-tab-bridge --public --license mpl-2.0 --description "Transfer browser tabs between profiles in Gecko-based browsers"

# Clone locally
git clone https://github.com/relexx/tb-tab-bridge.git
cd tb-tab-bridge
```

## Step 2: Copy the prepared files

Copy all files from the prepared repo structure into your cloned repository:

```
tb-tab-bridge/
├── README.md
├── CONTRIBUTING.md
├── .github/
│   └── ISSUE_TEMPLATE/
│       ├── bug_report.md
│       └── feature_request.md
├── docs/
│   ├── ARCHITECTURE.md
│   ├── SECURITY.md
│   └── PROJECT_PLAN.md
└── .claude/
    └── CLAUDE.md
```

Then commit:

```powershell
git add -A
git commit -m "docs: add architecture, security, project plan, and Claude Code setup"
git push origin main
```

## Step 3: Configure Claude Code

### 3.1 Verify Claude Code setup

```powershell
# Check Claude Code is working
claude --version

# Set default model for this project (Sonnet for speed, switch to Opus for complex tasks)
# Claude Code uses your Pro plan automatically
```

### 3.2 Project-level CLAUDE.md

The `.claude/CLAUDE.md` file is already created. Claude Code reads this automatically when you run `claude` from the project directory. It contains:

- Architecture summary
- Build commands
- Code conventions
- Security rules (NEVER-violate list)
- Testing strategy
- Task workflow

### 3.3 Start Claude Code in the project

```powershell
cd C:\path\to\tb-tab-bridge

# Start Claude Code in interactive mode
claude

# Or start with a specific task
claude "Scaffold the .NET 10 solution according to docs/ARCHITECTURE.md section 8.3"
```

## Step 4: Development workflow with Claude Code

### 4.1 Feature implementation (autonomous mode)

For each PoC task from `docs/PROJECT_PLAN.md`, use Claude Code in autonomous mode:

```powershell
# Example: Implement the NMH wire protocol
claude "Implement task #2 from docs/PROJECT_PLAN.md: NMH wire protocol (NativeMessageReader and NativeMessageWriter). Follow the architecture in docs/ARCHITECTURE.md section 7. Include unit tests."

# Example: Implement the broker
claude "Implement task #3: Named Pipe broker with DACL. Follow docs/ARCHITECTURE.md section 5.3 and docs/SECURITY.md section 2.2. Include the PipeSecurityFactory and all security guards."

# Example: Implement browser abstraction
claude "Implement task #6: Browser abstraction layer. Create BrowserDescriptor.cs and KnownBrowsers.cs as specified in docs/ARCHITECTURE.md section 3."
```

### 4.2 Review workflow

After Claude Code implements a feature:

1. **Review the code** – Claude Code will show you what it created/changed
2. **Run tests** – `dotnet test src/TabBridge.sln`
3. **Run security checklist** – Check the items in `docs/SECURITY.md` section 5
4. **Commit** – Use conventional commits:

```powershell
git add -A
git commit -m "feat(broker): implement Named Pipe broker with DACL security"
```

### 4.3 Conventional commit prefixes

| Prefix | Use for |
|---|---|
| `feat(scope)` | New feature |
| `fix(scope)` | Bug fix |
| `security(scope)` | Security-related change |
| `test(scope)` | Tests only |
| `docs` | Documentation |
| `refactor(scope)` | Code restructuring |
| `ci` | CI/CD pipeline |
| `chore` | Maintenance |

Scopes: `host`, `broker`, `nmh`, `security`, `detection`, `browser`, `extension`, `install`

## Step 5: Recommended task execution order

Follow this order for the PoC phase. Each task builds on the previous one:

```
Phase 1: Foundation
  #1  Scaffold .NET 10 solution
  #6  Browser abstraction layer (BrowserDescriptor, KnownBrowsers)
  #5  Message schema + protocol types (BridgeMessage, MessageType, TabPayload)
  #4  HMAC-SHA256 signing and validation
  #15 Unit tests for Security module

Phase 2: Infrastructure  
  #2  NMH wire protocol (NativeMessageReader/Writer)
  #3  Named Pipe broker with DACL
  #9  Auto-start broker from NMH, auto-shutdown
  #14 Replay protection + rate limiting

Phase 3: Profile detection
  #7  Profile detection (SQLite + legacy fallback)
  #8  --install / --uninstall / --status CLI
  #17 Unit tests for Detection module

Phase 4: Extension
  #10 Scaffold WebExtension
  #11 Tab context menu
  #12 Profile picker popup
  #13 Tab receive + notification

Phase 5: Integration
  #18 End-to-end test on Waterfox
```

For each phase, you can tell Claude Code:

```powershell
claude "Implement Phase 1 from the task execution order in docs/CLAUDE_CODE_SETUP.md. Work through tasks #1, #6, #5, #4, #15 sequentially. For each task, implement the code and write unit tests. Show me the result after each task."
```

## Step 6: GitHub Actions (post-PoC)

After the PoC is working, set up CI/CD:

```powershell
claude "Create a GitHub Actions workflow in .github/workflows/ci.yml that:
1. Triggers on push to main and on pull requests
2. Runs on windows-latest
3. Sets up .NET 10
4. Runs dotnet build
5. Runs dotnet test with test results
6. Publishes self-contained win-x64 binary as artifact
Follow standard GitHub Actions best practices."
```

For release automation:

```powershell
claude "Create a GitHub Actions release workflow in .github/workflows/release.yml that:
1. Triggers on tag push (v*)
2. Builds the self-contained win-x64 binary
3. Creates a GitHub Release with the binary attached
4. Also attaches the extension directory as a ZIP"
```

## Step 7: Tips for maximum productivity

### Use Opus for architecture decisions

```powershell
# Switch to Opus for complex design questions
claude --model opus "Review the current broker implementation against docs/SECURITY.md. Are there any security gaps? Suggest improvements."
```

### Use Sonnet for routine implementation

```powershell
# Sonnet is faster and cheaper for straightforward coding
claude "Add XML doc comments to all public members in TabBridge.Host/Security/"
```

### Batch related changes

```powershell
# Let Claude Code handle an entire module at once
claude "Implement the entire Install module (Installer.cs, Uninstaller.cs, StatusCheck.cs) following docs/ARCHITECTURE.md. The installer should register with all detected browsers, generate the HMAC secret, set ACLs, and write the NMH manifest."
```

### Use Claude Code for code review

```powershell
# After manual changes, let Claude Code review
claude "Review all files changed since the last commit against the security rules in .claude/CLAUDE.md. Flag any violations."
```

### Debug with context

```powershell
# When something breaks, give Claude Code the full context
claude "The broker crashes when a second profile connects. Here's the error: [paste error]. Debug this issue in TabBridge.Host/Broker/BrokerMode.cs."
```

## Step 8: Verify your setup

Before starting development, verify everything works:

```powershell
# 1. Claude Code reads the project instructions
cd C:\path\to\tb-tab-bridge
claude "What is this project about? What are the security rules I must follow?"
# → Should summarize Tab Bridge and list the 10 security rules from CLAUDE.md

# 2. .NET 10 SDK is available
dotnet --version
# → Should show 10.x

# 3. Waterfox profiles are set up
# Open Waterfox, check that you have at least 2 profiles via the Profiles menu

# 4. Waterfox profile DB exists
dir "%APPDATA%\Waterfox\Profile Groups\"
# → Should show a .sqlite file

# 5. Git and GitHub are configured
git remote -v
# → Should show github.com/relexx/tb-tab-bridge
```

## Automation ideas for later

Once the PoC is stable, consider:

- **Pre-commit hooks** via Husky: run `dotnet test` before every commit
- **Dependabot** for .NET package updates
- **CodeQL** analysis on GitHub for security scanning
- **Auto-versioning** via GitVersion based on conventional commits
- **Nightly builds** that run E2E tests against latest Waterfox
- **Extension linting** via `web-ext lint` in CI
- **SBOM generation** for the self-contained binary (required for enterprise adoption)
