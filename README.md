# Tab Bridge

**Transfer browser tabs between profiles in Gecko-based browsers – the feature Firefox won't ship.**

Tab Bridge adds a right-click context menu entry to send any tab (including its URL and title) to another browser profile. It works with Firefox, Waterfox, LibreWolf, and other Gecko-based browsers that support the Selectable Profiles system.

> Think of it as Microsoft Edge's "Move tab to profile" – but for the Gecko ecosystem.

## Status

🚧 **Pre-release / PoC in development** – Not ready for daily use yet.

## Why?

Since Firefox 138, Gecko-based browsers support [Selectable Profiles](https://support.mozilla.org/en-US/kb/profile-management) – separate browsing environments for work, personal, shopping, etc. But unlike Microsoft Edge, there is no built-in way to move a tab from one profile to another. Your only option is to manually copy-paste URLs.

Tab Bridge fills that gap with a WebExtension + Native Messaging Host architecture that prioritizes **security above all else**: zero network surface, no open ports, HMAC-signed messages, strict DACL-protected IPC.

## How it works

```
┌─────────────────┐         ┌─────────────────┐
│  Profile "Work"  │         │ Profile "Personal"│
│                  │         │                   │
│  Tab Bridge      │         │  Tab Bridge       │
│  Extension       │         │  Extension        │
│       │          │         │       ▲           │
└───────┼──────────┘         └───────┼───────────┘
        │ stdin/stdout               │ stdin/stdout
        ▼                            │
   ┌─────────┐    Named Pipe    ┌─────────┐
   │  NMH A  │◄────────────────►│  NMH B  │
   └────┬────┘   (DACL: owner   └────┬────┘
        │         only, no TCP)      │
        └────────────┬───────────────┘
                     ▼
              ┌────────────┐
              │   Broker   │
              │ (auto-start│
              │  auto-stop)│
              └────────────┘
```

**Key security properties:**
- Zero network surface – no TCP/UDP ports opened, ever
- IPC via Windows Named Pipes with DACL restricted to current user SID
- HMAC-SHA256 on every message
- URL schema whitelist (http/https only)
- Replay protection, rate limiting, message size limits
- No content scripts, no `<all_urls>` permission

## Architecture

Tab Bridge consists of three components:

| Component | Technology | Role |
|---|---|---|
| **WebExtension** | JavaScript (Manifest V3) | UI, tab context menu, profile picker popup |
| **Native Messaging Host** | C# / .NET 10 (self-contained exe) | stdin/stdout bridge to broker |
| **Broker** | Same exe, `--broker` mode | Named Pipe server, message routing |

All three components are a single `tab-bridge.exe` binary (self-contained, no runtime required) plus a WebExtension that must be installed in each profile.

## Browser compatibility

| Browser | Version | Selectable Profiles | Tested |
|---|---|---|---|
| Waterfox | 6.6.x+ (Gecko 140+) | ✅ | ✅ Primary |
| Firefox | 138+ | ✅ | Planned |
| LibreWolf | Based on Firefox 138+ | Likely ✅ | Planned |
| Floorp | Based on Firefox 138+ | Likely ✅ | Planned |

## Quick start (PoC)

> ⚠️ The PoC is under active development. These instructions will be updated.

### Prerequisites
- Windows 10/11
- .NET 10 SDK (for building)
- A Gecko-based browser with Selectable Profiles enabled

### Build & Install

```powershell
# Build self-contained exe
dotnet publish src/TabBridge.Host -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:PublishTrimmed=true

# Install (registers NMH with all detected browsers, generates HMAC secret)
.\publish\tab-bridge.exe --install

# Check installation
.\publish\tab-bridge.exe --status
```

Then load the extension in each profile via `about:debugging` → "Load Temporary Add-on".

## Documentation

| Document | Description |
|---|---|
| [Architecture](docs/ARCHITECTURE.md) | System design, component interaction, protocol spec |
| [Security](docs/SECURITY.md) | Threat model, defense-in-depth analysis, residual risks |
| [Project plan](docs/PROJECT_PLAN.md) | Roadmap from PoC to v1.0 |
| [Contributing](CONTRIBUTING.md) | How to contribute (solo project for now) |

## Roadmap

| Milestone | Scope |
|---|---|
| **PoC** | Single tab transfer, Waterfox, auto-broker, installer |
| **v0.5** | Multi-tab, notifications with undo, config dialog |
| **v1.0** | Tab groups, polished UI, AMO signing, GitHub Releases |
| **v1.5** | Cross-platform (Linux/macOS via Unix Domain Sockets) |

## License

[Mozilla Public License 2.0](LICENSE)

## Author

Built by [relexx](https://github.com/relexx) • Published under [relexx.de](https://relexx.de)
