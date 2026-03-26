# Tab Bridge – Project Plan

## Milestones

### PoC (Proof of Concept)

**Goal:** Functional single-tab transfer between two profiles on Windows, tested on Waterfox.

| # | Task | Component | Priority |
|---|---|---|---|
| 1 | Scaffold .NET 10 solution with project structure | Host | P0 |
| 2 | Implement NMH wire protocol (length-prefix read/write) | Host/Nmh | P0 |
| 3 | Implement Named Pipe broker with DACL | Host/Broker | P0 |
| 4 | Implement HMAC-SHA256 signing and validation | Host/Security | P0 |
| 5 | Implement message schema validation | Host/Security | P0 |
| 6 | Implement browser abstraction layer (`BrowserDescriptor`, `KnownBrowsers`) | Host/Browser | P0 |
| 7 | Implement profile detection (Selectable Profiles SQLite + legacy fallback) | Host/Detection | P0 |
| 8 | Implement `--install` / `--uninstall` / `--status` CLI commands | Host/Install | P0 |
| 9 | Implement auto-start broker from NMH, auto-shutdown on idle | Host/Broker | P0 |
| 10 | Scaffold WebExtension (Manifest V3, background.js) | Extension | P0 |
| 11 | Implement tab context menu ("Send to profile...") | Extension | P0 |
| 12 | Implement profile picker popup (basic, with theme colors from DB) | Extension | P0 |
| 13 | Implement tab receive + notification | Extension | P0 |
| 14 | Implement replay protection | Host/Security | P1 |
| 15 | Implement rate limiting | Host/Security | P1 |
| 16 | Write unit tests for Security module (HMAC, validation, replay, rate limiter) | Tests | P1 |
| 17 | Write unit tests for Detection module (profile detection, prefs parser) | Tests | P1 |
| 18 | End-to-end manual test on Waterfox with 2 profiles | QA | P0 |

**Estimated effort:** 4–5 person-days  
**Definition of done:** Tab URL transfers successfully between two Waterfox profiles. `--install` and `--status` work. All P0 unit tests pass.

### v0.5

**Goal:** Multi-tab support, polished notifications, configuration.

| # | Task | Component |
|---|---|---|
| 1 | Multi-tab send (all highlighted tabs) | Extension |
| 2 | Receive notification with "Undo" action (5s timeout) | Extension |
| 3 | Close source tab after successful ACK (configurable) | Extension |
| 4 | Configuration dialog (popup settings page) | Extension |
| 5 | Pipe squatting protection (PID verification) | Host/Security |
| 6 | Integration tests with mocked Named Pipe | Tests |

**Estimated effort:** 3 person-days

### v1.0

**Goal:** Production-ready, distributable on AMO and GitHub Releases.

| # | Task | Component |
|---|---|---|
| 1 | Tab group transfer support | Extension + Host |
| 2 | Polished popup UI with profile avatars and theme colors | Extension |
| 3 | AMO extension signing and submission | Distribution |
| 4 | GitHub Actions: CI build, test, release artifacts | CI/CD |
| 5 | GitHub Releases with self-contained exe + XPI | Distribution |
| 6 | MSIX package exploration (optional) | Distribution |
| 7 | Comprehensive README with screenshots | Docs |
| 8 | Security audit against checklist | QA |

**Estimated effort:** 5–6 person-days

### v1.5 (future)

Cross-platform support (Linux via Unix Domain Sockets, macOS), Authenticode signing for NMH binary.

### v2.0 (future)

Experimental drag-and-drop between profile windows, auto-update for NMH.

## Technical decisions log

| Decision | Choice | Rationale |
|---|---|---|
| Runtime | .NET 10 LTS | LTS until Nov 2028, self-contained publish, native Named Pipe + DACL support |
| IPC | Windows Named Pipes | Zero network surface, OS-native ACL, kernel-managed |
| Auth | HMAC-SHA256 | No asymmetric crypto overhead needed for local-only communication |
| Extension manifest | V3 | Future-proof, required for AMO submission |
| Primary test browser | Waterfox | Tests browser abstraction from day one |
| Profile detection | SQLite read-only | New Selectable Profiles use SQLite, not profiles.ini |
| License | MPL 2.0 | Copyleft per-file, allows proprietary integration |
| Distribution | AMO + GitHub Releases | Extension via AMO, NMH binary via GitHub Releases |
