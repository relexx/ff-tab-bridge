# Tab Bridge – Architecture & PoC Concept

**Version:** 1.0-rc1  
**Date:** 2026-03-26  
**Codename:** `tab-bridge`  
**Target platform:** Windows 10/11, .NET 10 LTS  
**Browser compatibility:** Gecko-based browsers with Selectable Profiles (Firefox 138+, Waterfox 6.6.x+, LibreWolf, Floorp, Zen)

---

## 1. Goal

Retrofit an Edge-equivalent "Move Tab to Profile" feature for Gecko-based browsers via a WebExtension paired with a Native Messaging Host (NMH). Design priority: **Security First** – no additional attack surface on the system.

## 2. High-level architecture

```
┌──────────────────────────────────────────────────────────────────────────┐
│                           Windows 10/11                                  │
│                                                                          │
│  ┌──────────────────────┐              ┌──────────────────────────┐      │
│  │   Browser Profile A  │              │    Browser Profile B     │      │
│  │  (separate process)  │              │   (separate process)     │      │
│  │                      │              │                          │      │
│  │  ┌────────────────┐  │              │  ┌────────────────────┐  │      │
│  │  │  tab-bridge    │  │              │  │  tab-bridge        │  │      │
│  │  │  WebExtension  │  │              │  │  WebExtension      │  │      │
│  │  │                │  │              │  │                    │  │      │
│  │  │ • Context menu │  │              │  │ • Receiver         │  │      │
│  │  │ • Tab picker   │  │              │  │ • Tab creation     │  │      │
│  │  │ • UI / Popup   │  │              │  │ • Notification     │  │      │
│  │  └───────┬────────┘  │              │  └──────┬─────────────┘  │      │
│  │          │ stdin/stdout│              │         │ stdin/stdout   │      │
│  └──────────┼───────────┘              └─────────┼────────────────┘      │
│             │                                    │                       │
│             ▼                                    ▼                       │
│  ┌──────────────────┐              ┌──────────────────┐                  │
│  │  NMH Instance A  │              │  NMH Instance B  │                  │
│  │  (tab-bridge.exe)│              │  (tab-bridge.exe) │                  │
│  │  mode: --nmh     │              │  mode: --nmh      │                  │
│  └────────┬─────────┘              └────────┬──────────┘                  │
│           │ Named Pipe (client)             │ Named Pipe (client)        │
│           │                                 │                            │
│           ▼                                 ▼                            │
│  ┌─────────────────────────────────────────────────────────────┐         │
│  │                    Broker process                            │         │
│  │                    (tab-bridge.exe --broker)                 │         │
│  │                                                             │         │
│  │  • Named Pipe server: \\.\pipe\tab-bridge-{UserSID}        │         │
│  │  • DACL: current user SID only                              │         │
│  │  • No TCP/UDP – zero network surface                        │         │
│  │  • Message validation, HMAC, rate limiting                  │         │
│  │  • Profile registration & routing                           │         │
│  │  • Auto-shutdown after 60s with no clients                  │         │
│  └─────────────────────────────────────────────────────────────┘         │
│                                                                          │
│  Filesystem:                                                             │
│  %LOCALAPPDATA%\tab-bridge\                                              │
│  ├── tab-bridge.exe         Self-contained .NET 10 binary                │
│  ├── tab_bridge.json        NMH manifest (read by browser)              │
│  ├── secret.key             HMAC secret (256 bit, ACL: owner only)      │
│  ├── config.json            Optional configuration                       │
│  └── logs\                                                               │
│      └── broker.log         Audit log (ACL: owner only)                 │
│                                                                          │
│  Registry (per detected browser):                                        │
│  HKCU\Software\{Browser}\NativeMessagingHosts\tab_bridge                 │
│  └── (Default) = "C:\...\tab-bridge\tab_bridge.json"                    │
│                                                                          │
└──────────────────────────────────────────────────────────────────────────┘
```

### Core principle: one binary, two modes

The entire native side is a single self-contained `.exe`. Behavior is controlled via CLI arguments:

| Invocation | Mode | Started by |
|---|---|---|
| `tab-bridge.exe --nmh` | NMH instance (stdin/stdout ↔ Named Pipe client) | Browser (via NMH manifest) |
| `tab-bridge.exe --broker` | Broker process (Named Pipe server) | First NMH instance (auto-start) |
| `tab-bridge.exe --install` | Installation (registry, secret, ACLs) | User (one-time) |
| `tab-bridge.exe --uninstall` | Uninstallation | User |
| `tab-bridge.exe --status` | Diagnostics (broker running? permissions OK?) | User |

## 3. Browser abstraction layer

Tab Bridge is not limited to Firefox – all Gecko-based browsers with Selectable Profile support and Native Messaging can be supported. Since each browser uses its own paths for registry, AppData, and profiles, Tab Bridge encapsulates these differences in a `BrowserDescriptor`.

### 3.1 Known browsers and their paths

| Browser | Registry path (NMH) | Roaming AppData | Profile Groups |
|---|---|---|---|
| Firefox | `Software\Mozilla\NativeMessagingHosts\` | `%APPDATA%\Mozilla\Firefox\` | `...\Profile Groups\` |
| Waterfox | `Software\Waterfox\NativeMessagingHosts\` | `%APPDATA%\Waterfox\` | `...\Profile Groups\` |
| LibreWolf | `Software\LibreWolf\NativeMessagingHosts\` | `%APPDATA%\LibreWolf\` | `...\Profile Groups\` |
| Floorp | `Software\Ablaze\Floorp\NativeMessagingHosts\` | `%APPDATA%\Floorp\` | `...\Profile Groups\` |
| Zen Browser | `Software\Mozilla\NativeMessagingHosts\` * | `%APPDATA%\zen\` | `...\Profile Groups\` |

\* Some forks reuse Mozilla's registry path. Tab Bridge registers with all detected browsers during `--install`.

### 3.2 BrowserDescriptor

```csharp
public record BrowserDescriptor(
    string Name,
    string RegistrySubKey,    // e.g. @"Software\Mozilla\NativeMessagingHosts"
    string AppDataSubPath,    // e.g. @"Mozilla\Firefox" (relative to %APPDATA%)
    string ProfileGroupsDir   // e.g. "Profile Groups"
)
{
    public string GetRoamingPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        AppDataSubPath);

    public string GetProfilesIniPath() => Path.Combine(GetRoamingPath(), "profiles.ini");

    public string GetProfileGroupsPath() => Path.Combine(GetRoamingPath(), ProfileGroupsDir);

    public string GetRegistryPath(string nmhName) => $@"{RegistrySubKey}\{nmhName}";
}

public static class KnownBrowsers
{
    public static readonly BrowserDescriptor[] All =
    [
        new("Firefox",   @"Software\Mozilla\NativeMessagingHosts",       @"Mozilla\Firefox", "Profile Groups"),
        new("Waterfox",  @"Software\Waterfox\NativeMessagingHosts",      "Waterfox",         "Profile Groups"),
        new("LibreWolf", @"Software\LibreWolf\NativeMessagingHosts",     "LibreWolf",        "Profile Groups"),
        new("Floorp",    @"Software\Ablaze\Floorp\NativeMessagingHosts", "Floorp",           "Profile Groups"),
        new("Zen",       @"Software\Mozilla\NativeMessagingHosts",       "zen",              "Profile Groups"),
    ];

    public static BrowserDescriptor DetectFromParentProcess()
    {
        var parentExe = GetParentProcessExeName().ToLowerInvariant();
        foreach (var browser in All)
            if (parentExe.Contains(browser.Name, StringComparison.OrdinalIgnoreCase))
                return browser;
        foreach (var browser in All)
            if (Directory.Exists(browser.GetRoamingPath()))
                return browser;
        return All[0]; // fallback
    }

    public static IEnumerable<BrowserDescriptor> DetectInstalled()
        => All.Where(b => Directory.Exists(b.GetRoamingPath()));
}
```

### 3.3 Installation registers with all detected browsers

```csharp
foreach (var browser in KnownBrowsers.DetectInstalled())
{
    var regPath = browser.GetRegistryPath("tab_bridge");
    using var key = Registry.CurrentUser.CreateSubKey(regPath);
    key.SetValue("", Path.Combine(appDir, "tab_bridge.json"));
}
```

## 4. Message protocol

All messages are JSON with a strict schema. No free-form fields.

```json
{
  "version": 1,
  "type": "TAB_SEND",
  "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "timestamp": 1711468800,
  "source_profile": "Work",
  "target_profile": "Personal",
  "payload": {
    "url": "https://example.com/page",
    "title": "Example Page",
    "pinned": false,
    "group_id": null
  },
  "hmac": "base64-encoded-hmac-sha256"
}
```

Allowed `type` values (whitelist): `REGISTER`, `TAB_SEND`, `TAB_SEND_BATCH`, `ACK`, `NACK`, `HEARTBEAT`, `PROFILE_LIST_REQUEST`, `PROFILE_LIST_RESPONSE`.

Any other type is dropped. Maximum message size: **64 KiB**. Payloads are validated against a JSON schema before forwarding.

## 5. Component design

### 5.1 WebExtension

**Manifest permissions (least privilege):**

```json
{
  "manifest_version": 3,
  "name": "Tab Bridge",
  "version": "0.1.0",
  "description": "Transfer tabs between browser profiles",
  "browser_specific_settings": {
    "gecko": {
      "id": "tab-bridge@relexx.de",
      "strict_min_version": "128.0"
    }
  },
  "permissions": [
    "nativeMessaging",
    "menus",
    "tabs",
    "activeTab",
    "notifications"
  ],
  "background": {
    "scripts": ["background.js"],
    "type": "module"
  },
  "action": {
    "default_popup": "popup.html",
    "default_icon": "icons/icon-48.png"
  }
}
```

No `<all_urls>`, no `webRequest`, no `storage.sync`, no content access, no `externally_connectable`.

### 5.2 Native Messaging Host (.NET 10)

**Publish configuration:**

```xml
<PropertyGroup>
  <OutputType>Exe</OutputType>
  <TargetFramework>net10.0</TargetFramework>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <PublishSingleFile>true</PublishSingleFile>
  <SelfContained>true</SelfContained>
  <PublishTrimmed>true</PublishTrimmed>
  <InvariantGlobalization>true</InvariantGlobalization>
</PropertyGroup>
```

Result: a single `tab-bridge.exe` (~15–20 MB), no .NET runtime dependency.

### 5.3 Named Pipe configuration

**Pipe name:** `\\.\pipe\tab-bridge-{UserSID}`

**DACL:**

```csharp
private static PipeSecurity CreateRestrictedPipeSecurity()
{
    var security = new PipeSecurity();
    var currentUser = WindowsIdentity.GetCurrent().User!;

    security.AddAccessRule(new PipeAccessRule(
        new SecurityIdentifier(WellKnownSidType.WorldSid, null),
        PipeAccessRights.FullControl,
        AccessControlType.Deny));

    security.AddAccessRule(new PipeAccessRule(
        currentUser,
        PipeAccessRights.ReadWrite,
        AccessControlType.Allow));

    return security;
}
```

### 5.4 NMH manifest and registry registration

**NMH manifest (`tab_bridge.json`):**

```json
{
  "name": "tab_bridge",
  "description": "Tab Bridge – Cross-Profile Tab Transfer",
  "path": "C:\\Users\\<USER>\\AppData\\Local\\tab-bridge\\tab-bridge.exe",
  "type": "stdio",
  "allowed_extensions": ["tab-bridge@relexx.de"]
}
```

**Registry entries (per detected browser):**

```
HKCU\Software\Mozilla\NativeMessagingHosts\tab_bridge    ← Firefox
HKCU\Software\Waterfox\NativeMessagingHosts\tab_bridge   ← Waterfox
HKCU\Software\LibreWolf\NativeMessagingHosts\tab_bridge  ← LibreWolf
... (all via KnownBrowsers.DetectInstalled())
```

## 6. Gecko profile architecture and profile detection

### 6.1 Two-layer model (Firefox 138+ / compatible forks)

Gecko-based browsers with new profile management use a two-layer model:

**Layer 1 – Toolkit Profile Service (`profiles.ini`):**  
Manages `nsIToolkitProfile` entries in `profiles.ini`. What appears as `[Profile0]`, `[Profile1]` etc. are **profile groups**, not individual profiles.

**Layer 2 – Selectable Profile Service (SQLite):**  
The actual user-visible profiles are stored in a SQLite database in the `Profile Groups` subdirectory of the browser data directory (path resolved via `BrowserDescriptor.GetProfileGroupsPath()`). All profiles within a group share a `storeID` stored in the `toolkit.profiles.storeID` preference. The database file is named `{storeID}.sqlite`.

**Verified Profiles table schema:**

```sql
CREATE TABLE "Profiles" (
    id      INTEGER NOT NULL,
    path    TEXT NOT NULL UNIQUE,
    name    TEXT NOT NULL,
    avatar  TEXT NOT NULL,
    themeId TEXT NOT NULL,
    themeFg TEXT NOT NULL,
    themeBg TEXT NOT NULL,
    PRIMARY KEY(id)
);
```

The `path` field contains the profile directory path relative to the browser's `Profiles` directory. The `name` field contains the user-assigned profile name.

### 6.2 Profile detection (three-stage)

```csharp
public static class ProfileDetector
{
    public static async Task<ProfileInfo> DetectAsync()
    {
        var browser = KnownBrowsers.DetectFromParentProcess();
        var ownProfilePath = ResolveOwnProfilePath();
        var storeId = PrefsParser.ReadValue(
            Path.Combine(ownProfilePath, "prefs.js"), "toolkit.profiles.storeID");

        if (storeId is not null)
        {
            var dbPath = Path.Combine(browser.GetProfileGroupsPath(), $"{storeId}.sqlite");
            if (File.Exists(dbPath))
                return await ReadSelectableProfile(dbPath, ownProfilePath);
        }

        return ReadLegacyProfile(browser, ownProfilePath);
    }

    private static async Task<ProfileInfo> ReadSelectableProfile(string dbPath, string ownProfilePath)
    {
        var connStr = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadOnly // WAL mode supports concurrent reads
        }.ToString();

        await using var conn = new SqliteConnection(connStr);
        await conn.OpenAsync();

        var relativePath = GetRelativeProfilePath(ownProfilePath);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, avatar, themeBg FROM Profiles WHERE path = @path";
        cmd.Parameters.AddWithValue("@path", relativePath);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
            return new ProfileInfo(reader.GetInt32(0), reader.GetString(1),
                reader.GetString(2), reader.GetString(3), IsSelectableProfile: true);

        throw new InvalidOperationException($"Profile not found for path: {relativePath}");
    }

    public static async Task<IReadOnlyList<ProfileInfo>> ListAllProfilesAsync()
    {
        var browser = KnownBrowsers.DetectFromParentProcess();
        // ... same pattern, SELECT * FROM Profiles ORDER BY name
    }
}

public record ProfileInfo(int Id, string Name, string Avatar, string ThemeColor, bool IsSelectableProfile);
```

## 7. NMH wire protocol

The browser's Native Messaging protocol uses a binary length-prefix format:

```
[4 bytes: uint32 LE – message length][N bytes: UTF-8 JSON]
```

Maximum message length enforced **before** JSON parse: 64 KiB.

## 8. PoC scope

### 8.1 Feature matrix

| Feature | PoC | v1.0 |
|---|---|---|
| Send single tab via context menu | ✅ | ✅ |
| Profile picker popup | ✅ (basic) | ✅ (polished, with theme colors) |
| Send multiple selected tabs | ❌ | ✅ |
| Send tab groups | ❌ | ✅ |
| Receive notification | ✅ | ✅ |
| Close source tab after transfer | ❌ | ✅ (configurable) |
| HMAC message signing | ✅ | ✅ |
| JSON schema validation | ✅ | ✅ |
| Auto broker start/stop | ✅ | ✅ |
| Installer (`--install` / `--uninstall`) | ✅ | ✅ (+ optional MSIX) |
| Diagnostics (`--status`) | ✅ | ✅ |
| Linux/macOS support | ❌ | v1.5 (UDS instead of Named Pipe) |

### 8.2 Technology stack

| Component | Technology | Rationale |
|---|---|---|
| WebExtension | JavaScript (Manifest V3) | Standard browser extension |
| NMH + Broker | C# / .NET 10 (self-contained) | Named Pipes native, DACL native, no runtime needed |
| IPC | Windows Named Pipes | No network, OS-native ACL control |
| Auth | HMAC-SHA256 (`System.Security.Cryptography`) | Shared secret, generated at install |
| Serialization | `System.Text.Json` (source generator) | Trim-compatible, no reflection |
| Profile DB | `Microsoft.Data.Sqlite` | Read-only access to Selectable Profiles DB |
| Logging | `Microsoft.Extensions.Logging` + file sink | Minimal, structured |

### 8.3 Solution structure

```
tb-tab-bridge/
├── README.md
├── CONTRIBUTING.md
├── LICENSE                                → MPL 2.0 (generated by GitHub)
├── .github/
│   ├── ISSUE_TEMPLATE/
│   │   ├── bug_report.md
│   │   └── feature_request.md
│   └── workflows/                         → CI/CD (post-PoC)
├── docs/
│   ├── ARCHITECTURE.md                    → This document
│   ├── SECURITY.md                        → Security design document
│   └── PROJECT_PLAN.md                    → Roadmap
├── src/
│   ├── TabBridge.sln
│   ├── TabBridge.Host/                    .NET 10 console app (NMH + broker)
│   │   ├── Program.cs                     CLI routing (--nmh, --broker, --install, ...)
│   │   ├── TabBridge.Host.csproj
│   │   ├── Nmh/
│   │   │   ├── NmhMode.cs                stdin/stdout ↔ Named Pipe bridge
│   │   │   ├── NativeMessageReader.cs     Browser NMH protocol (length-prefix)
│   │   │   └── NativeMessageWriter.cs
│   │   ├── Broker/
│   │   │   ├── BrokerMode.cs              Named Pipe server, client routing
│   │   │   ├── ClientSession.cs           Per-client state (profile, pipe, limiter)
│   │   │   └── ShutdownTimer.cs           Auto-shutdown at 0 clients
│   │   ├── Security/
│   │   │   ├── HmacValidator.cs           HMAC-SHA256 generation & validation
│   │   │   ├── MessageValidator.cs        JSON schema + URL whitelist
│   │   │   ├── ReplayGuard.cs             Nonce window (60s)
│   │   │   ├── RateLimiter.cs             Token bucket per profile
│   │   │   └── PipeSecurityFactory.cs     DACL configuration
│   │   ├── Protocol/
│   │   │   ├── BridgeMessage.cs           Message record with JsonSerializer
│   │   │   ├── MessageType.cs             Enum (whitelist)
│   │   │   └── TabPayload.cs              Payload record
│   │   ├── Install/
│   │   │   ├── Installer.cs               Registry, secret, ACLs
│   │   │   ├── Uninstaller.cs
│   │   │   └── StatusCheck.cs             Diagnostics
│   │   ├── Detection/
│   │   │   ├── ProfileDetector.cs         Profile name detection (orchestrator)
│   │   │   ├── SelectableProfileReader.cs SQLite access to Profile Groups DB
│   │   │   ├── LegacyProfileReader.cs     Fallback: profiles.ini parser
│   │   │   └── PrefsParser.cs             prefs.js key-value extraction
│   │   └── Browser/
│   │       ├── BrowserDescriptor.cs       Path abstraction per browser
│   │       └── KnownBrowsers.cs           Registry of known Gecko forks
│   ├── TabBridge.Host.Tests/              Unit tests
│   │   ├── HmacValidatorTests.cs
│   │   ├── MessageValidatorTests.cs
│   │   ├── ReplayGuardTests.cs
│   │   ├── RateLimiterTests.cs
│   │   └── ProfileDetectorTests.cs
│   └── extension/
│       ├── manifest.json
│       ├── background.js                  NMH connection, message logic
│       ├── popup.html                     Profile picker UI
│       ├── popup.js
│       ├── popup.css
│       └── icons/
│           ├── icon-48.png
│           └── icon-96.png
└── .claude/
    └── CLAUDE.md                          → Claude Code project instructions
```

## 9. Known limitations

**Not transferable (API boundary):**
- Back/forward navigation history of the tab
- Session cookies / login state
- `about:` pages and `file://` URLs (extension cannot open these via `tabs.create`)
- Scroll position within the page
- Service worker state / IndexedDB data of the website

**Organizational constraints:**
- Extension must be installed separately in each profile
- NMH must be installed once at OS level (`--install`)
- AMO distribution only possible for the extension; NMH must be distributed as a separate binary

## 10. Evolution path

| Phase | Scope | Estimated effort |
|---|---|---|
| PoC | Single tab, Windows, auto-broker, installer | ~4–5 PD |
| v0.5 | Multi-tab, notification with undo, config dialog | ~3 PD |
| v1.0 | Tab groups, polished popup UI, AMO signing, GitHub Releases | ~5–6 PD |
| v1.5 | Cross-platform: Linux (UDS) + macOS via `#if` abstraction | ~4–5 PD |
| v2.0 | Optional: drag-and-drop between profile windows | ~5 PD |
| v2.5 | MSIX installer, auto-update for NMH | ~3–4 PD |
