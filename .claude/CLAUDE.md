# CLAUDE.md – Tab Bridge project instructions

## Project overview

Tab Bridge is a WebExtension + Native Messaging Host that enables transferring browser tabs between profiles in Gecko-based browsers (Firefox, Waterfox, LibreWolf, etc.). It fills the gap of a missing "Move tab to profile" feature.

**Repository:** https://github.com/relexx/ff-tab-bridge  
**Author:** relexx (relexx.de)  
**License:** MPL 2.0

## Architecture summary

Three components, one binary:

1. **WebExtension** (JavaScript, Manifest V3) – UI, context menu, profile picker
2. **NMH** (C# .NET 10, `--nmh` mode) – stdin/stdout bridge to broker via Named Pipe
3. **Broker** (same binary, `--broker` mode) – Named Pipe server, message routing

Key design constraint: **zero network surface**. No TCP/UDP ports. IPC via Windows Named Pipes with DACL. All messages HMAC-SHA256 signed.

Read `docs/ARCHITECTURE.md` and `docs/SECURITY.md` before making changes.

## Tech stack

- **Host:** C# / .NET 10 LTS, self-contained single-file publish, `win-x64`
- **Extension:** JavaScript ES modules, Manifest V3, no bundler
- **IPC:** Windows Named Pipes
- **Auth:** HMAC-SHA256 (`System.Security.Cryptography`)
- **Profile DB:** `Microsoft.Data.Sqlite` (read-only access)
- **Serialization:** `System.Text.Json` with source generators (trim-compatible)
- **Testing:** xUnit, FluentAssertions

## Build commands

```bash
# Build
dotnet build src/TabBridge.sln

# Test
dotnet test src/TabBridge.sln

# Publish self-contained
dotnet publish src/TabBridge.Host -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true

# Install NMH (after publish)
./publish/tab-bridge.exe --install

# Check status
./publish/tab-bridge.exe --status
```

## Code conventions

- File-scoped namespaces (`namespace TabBridge.Host.Security;`)
- Records for DTOs and messages (`public record BridgeMessage(...)`)
- `System.Text.Json` source generators for all serialization (no reflection)
- Async/await everywhere, pass `CancellationToken`
- All public APIs documented with XML doc comments
- All code and comments in English
- No `var` for non-obvious types

## Security rules (NEVER violate these)

1. **No `System.Net.Sockets`** – no TCP/UDP anywhere
2. **No `Process.Start()` with variable arguments** – only hardcoded paths
3. **No `eval`, `Assembly.Load`, `CSharpScript`** – no dynamic code execution
4. **No `extern "unsafe"`** unless absolutely necessary for Win32 interop
5. **HMAC comparison must use `CryptographicOperations.FixedTimeEquals`**
6. **SQLite DB must be opened read-only** (`SqliteOpenMode.ReadOnly`)
7. **URL whitelist: only `http://` and `https://`** – block everything else
8. **Message size limit (64 KiB) must be enforced before JSON parse**
9. **Named Pipe DACL must Deny Everyone + Allow only current user SID**
10. **`PipeOptions.CurrentUserOnly` must always be set**

## Browser abstraction

Never hardcode browser-specific paths. Use `BrowserDescriptor` and `KnownBrowsers` from `src/TabBridge.Host/Browser/`. The browser is detected at runtime from the parent process.

## Project structure

```
src/
├── TabBridge.Host/           Main .NET 10 project
│   ├── Program.cs            CLI entry point
│   ├── Nmh/                  NMH mode (stdin/stdout ↔ Named Pipe)
│   ├── Broker/               Broker mode (Named Pipe server)
│   ├── Security/             HMAC, validation, replay, rate limiting
│   ├── Protocol/             Message types, serialization
│   ├── Install/              --install, --uninstall, --status
│   ├── Detection/            Profile detection (SQLite + legacy)
│   └── Browser/              Browser abstraction layer
├── TabBridge.Host.Tests/     Unit tests
└── extension/                WebExtension (Manifest V3)
    ├── manifest.json
    ├── background.js
    ├── popup.html/js/css
    └── icons/
```

## Testing strategy

- **Unit tests** for Security module (HMAC, schema validation, replay guard, rate limiter)
- **Unit tests** for Detection module (profile detection, prefs.js parser, SQLite reader)
- **Unit tests** for Protocol module (message serialization, NMH wire format)
- **Integration tests** for Broker (mock Named Pipe, message routing)
- **Manual E2E test** on Waterfox with two profiles (PoC phase)
- **GitHub Actions CI** (post-PoC): build + test on every push

## Task workflow

When implementing a feature:

1. Read the relevant section in `docs/ARCHITECTURE.md` and `docs/SECURITY.md`
2. Check `docs/PROJECT_PLAN.md` for task dependencies
3. Implement the feature with full error handling
4. Write unit tests (aim for >80% coverage on Security and Detection modules)
5. Run `dotnet test` and fix any failures
6. Run `dotnet publish` and verify the binary works
7. Present the result for review

## Important notes

- Primary test browser is **Waterfox** (not Firefox) – this tests the browser abstraction from day one
- The Selectable Profiles SQLite schema is undocumented and may change – always validate schema before querying
- Extension ID is `tab-bridge@relexx.de`
- NMH name is `tab_bridge` (underscore, not hyphen – NMH names only allow alphanumeric + dots + underscores)
