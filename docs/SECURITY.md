# Tab Bridge – Security Architecture

**Version:** 1.0-rc1  
**Date:** 2026-03-26  
**Classification:** Security design document  
**Companion to:** [Architecture](ARCHITECTURE.md)

---

## 1. Threat model

### 1.1 Security objectives

| Objective | Description |
|---|---|
| **Zero network surface** | The system must not open any TCP/UDP port. No remote attack possible. |
| **No privilege escalation** | The NMH must not have more rights than the calling browser process. |
| **No data exfiltration** | No uncontrolled access to browser data, filesystem, or other profiles. |
| **No code injection** | Messages must never be interpreted or executed as code. |
| **Message integrity** | Messages must not be tampered with undetected. |
| **No cross-user leaks** | The Named Pipe must only be reachable by the current Windows user. |

### 1.2 Attacker profiles

| # | Attacker | Threat level | Description |
|---|---|---|---|
| 1 | Local malware (same user) | Medium | Software running under the same Windows user, attempting to manipulate the Named Pipe or read the HMAC secret. |
| 2 | Local malware (other user) | Low | Software under a different user account. Blocked by Named Pipe DACL and filesystem ACLs. |
| 3 | Malicious website | Low | JavaScript on a webpage attempting to instrumentalize the extension. Blocked by absence of content scripts. |
| 4 | Rogue extension | Low-Medium | Another installed extension attempting to communicate with Tab Bridge via `runtime` API. |
| 5 | Network attacker | Out of scope | No TCP/UDP ports are opened. This is a core design goal. |

### 1.3 Attack matrix

| Attack vector | Countermeasure | Residual risk |
|---|---|---|
| Remote access to broker | No TCP/UDP. Named Pipe with DACL. | None (by design) |
| Named Pipe access from other user | DACL: Deny Everyone, Allow only owner SID | None |
| Named Pipe access from same user (malware) | HMAC-SHA256 on every message | Medium (§3.2) |
| Malicious URL injection via pipe | URL schema whitelist, no `javascript:`, no `file:` | Low |
| Message replay | Timestamp + nonce (`id` field) with 60s replay window | Low |
| Oversized messages (DoS) | 64 KiB hard limit, enforced before JSON parse | None |
| Tampered `tab-bridge.exe` | Filesystem ACL, production: code signing | Medium (§3.1) |
| Cross-extension messaging | No `externally_connectable`, no outward message API | Low |
| Content script injection | No content scripts in the extension | None |
| NMH invocation by foreign extension | `allowed_extensions` whitelist in NMH manifest | None (browser-enforced) |
| Reading/writing browser profile DB | Read-only mode only, schema validation | Low (§3.7) |
| Registry manipulation of NMH path | HKCU entry, ACL on registry key recommended | Low-Medium |

## 2. Security measures in detail

### 2.1 Zero network surface

The entire IPC runs over:
- **stdin/stdout pipes** between browser and NMH (OS-managed, not externally addressable)
- **Windows Named Pipes** between NMH instances and broker (kernel-based, no network interface)

No TCP or UDP socket is ever opened. The Named Pipe is a kernel object in the Object Manager namespace and is not reachable over the network unless SMB shares are configured on `\\.\pipe\` (not the case by default for custom pipes).

The codebase contains no `System.Net.Sockets` references. A runtime guard verifies no TCP listeners exist for the current process.

### 2.2 Named Pipe security (DACL)

**Pipe name:** `\\.\pipe\tab-bridge-{UserSID}`

The user SID in the pipe name prevents collisions between Windows users.

**DACL configuration:**

```csharp
var security = new PipeSecurity();
var currentUser = WindowsIdentity.GetCurrent().User!;

// DENY ALL for Everyone (evaluated before Allow rules)
security.AddAccessRule(new PipeAccessRule(
    new SecurityIdentifier(WellKnownSidType.WorldSid, null),
    PipeAccessRights.FullControl,
    AccessControlType.Deny));

// ALLOW ReadWrite only for current user
security.AddAccessRule(new PipeAccessRule(
    currentUser,
    PipeAccessRights.ReadWrite,
    AccessControlType.Allow));
```

Additionally, `PipeOptions.CurrentUserOnly` (.NET built-in guard) provides a second security ring.

### 2.3 Named Pipe squatting protection

An attacker could create the Named Pipe before the broker starts, intercepting all messages.

**Countermeasures:**
1. NMH client verifies server process identity via `GetNamedPipeServerProcessId` + image path check
2. HMAC validation: a squatter cannot produce valid responses without the secret
3. Broker uses a named Mutex for single-instance guarantee

### 2.4 Message authentication (HMAC-SHA256)

Every message is signed with HMAC-SHA256.

**Key generation:** `RandomNumberGenerator.GetBytes(32)` – 256 bits from OS CSPRNG.  
**Storage:** `%LOCALAPPDATA%\tab-bridge\secret.key`, ACL: owner read only.  
**Comparison:** `CryptographicOperations.FixedTimeEquals()` (timing-safe).  
**Canonical form:** Deterministic JSON serialization (sorted keys, no whitespace).

**Production upgrade:** DPAPI (`ProtectedData.Protect()` with `DataProtectionScope.CurrentUser`) to encrypt the secret at rest.

### 2.5 Message validation pipeline

**Stage 0 – Size limit (before JSON parse):**
64 KiB hard limit enforced before the JSON parser allocates memory.

**Stage 1 – Structural validation:**
- `JsonSerializerOptions { MaxDepth = 4 }`
- Required fields: `version`, `type`, `id`, `timestamp`, `hmac`
- `type` must be in the whitelist enum
- `id` must be UUID v4 format
- `timestamp` must be within ±30 seconds of local time

**Stage 2 – Payload validation (for `TAB_SEND`):**
- URL must be valid absolute URI
- URL schema whitelist: only `https://` and `http://`
- Explicitly blocked: `javascript:`, `data:`, `file:`, `about:`, `chrome:`, `moz-extension:`, `blob:`, `resource:`, `vbscript:`
- Title: optional, max 512 characters

**Stage 3 – Replay protection:**
ConcurrentDictionary with 60-second window. Duplicate `id` → message dropped.

**Stage 4 – Rate limiting:**
Token bucket: 20/s per profile connection, 100/min total.

### 2.6 Extension isolation

- **No content scripts** – no code injected into web pages
- **No `externally_connectable`** – other extensions cannot send messages
- **Minimal permissions:** `nativeMessaging`, `menus`, `tabs`, `activeTab`, `notifications`
- **NMH manifest whitelist:** `"allowed_extensions": ["tab-bridge@relexx.de"]` – browser-enforced

### 2.7 NMH process isolation

- **No code execution from messages** – messages are deserialized as typed C# records only
- **No filesystem write access** at runtime (except log directory)
- **No shell interaction** – no `Process.Start()` with variable arguments
- **Read-only SQLite access** to browser profile database with schema validation
- **Auto-termination:** NMH exits after 30s stdin inactivity; broker exits after 60s with no clients

## 3. Residual risks

### 3.1 Risk: compromised `tab-bridge.exe`

**Description:** If an attacker can replace the `.exe` on disk, they control the broker process.  
**Rating:** Medium. Requires write access to `%LOCALAPPDATA%\tab-bridge\`.  
**Mitigations:** Directory ACL (owner FullControl, inheritance disabled), production: Authenticode signing, SHA256 hash verification.  
**Residual:** An attacker with user-level write access could also directly manipulate browser profile data. Tab Bridge does not worsen the security posture.

### 3.2 Risk: secret key extraction

**Description:** The HMAC secret is stored as a file. A process under the same user can read it despite restrictive ACLs.  
**Rating:** Low-Medium.  
**Mitigations:** File ACL (owner read only), production: DPAPI encryption, secret rotation on broker start.

### 3.3 Risk: phishing via URL injection

**Description:** An attacker who can inject valid messages could send phishing URLs to a profile.  
**Rating:** Low. Requires HMAC secret + Named Pipe access.  
**Mitigations:** URL schema whitelist, notification with URL preview before opening, background tab (no focus).

### 3.4 Risk: Named Pipe squatting

**Description:** An attacker creates the pipe before the broker and intercepts messages.  
**Rating:** Low-Medium. Requires timing and same user context.  
**Mitigations:** Mutex lock, PID verification, HMAC validation, audit logging.

### 3.5 Risk: local denial of service

**Description:** A local process floods the pipe or blocks it.  
**Rating:** Low.  
**Mitigations:** Rate limiting, message size cap, connection termination on limit breach.

### 3.6 Risk: registry manipulation

**Description:** An attacker modifies `HKCU\Software\{Browser}\NativeMessagingHosts\tab_bridge` to point to a malicious binary.  
**Rating:** Low-Medium.  
**Mitigations:** `--status` checks registry integrity, production: ACL on registry key.

### 3.7 Risk: browser-internal SQLite database access

**Description:** Tab Bridge reads the SQLite database in the browser's `Profile Groups` directory to resolve profile names.  
**Rating:** Low-Medium.  

*Data integrity:* Read-only mode enforced at connection-string level. WAL mode supports concurrent reads. Corruption risk: practically zero.  

*Schema stability:* The schema is an internal Gecko API. Browser developers can change it without notice.  

**Mitigations:** Schema validation via `PRAGMA table_info(Profiles)` before first query; graceful degradation to legacy detection on schema mismatch; DB connection opened and closed per request.  
**Residual:** A browser update changes the schema → fallback to technical profile directory names instead of user-friendly names. UX degradation, not a security risk.

### 3.8 Risk: SMB network exposure

**Description:** If the machine exposes Named Pipes via SMB, they could theoretically be reachable remotely.  
**Rating:** Very low. Default Windows configuration does not expose custom Named Pipes via SMB.  
**Mitigations:** DACL contains Deny Everyone; user SID in pipe name; `--status` verifies local-only reachability.

## 4. Defense-in-depth overview

```
Layer 1: Zero network surface
  └── No TCP/UDP socket in entire codebase
  └── Named Pipes are kernel-local, not network-exposed

Layer 2: Named Pipe DACL
  └── Deny Everyone + Allow only owner SID
  └── PipeOptions.CurrentUserOnly (.NET built-in guard)
  └── User SID in pipe name (isolation between Windows users)

Layer 3: Message integrity (HMAC-SHA256)
  └── Every message signed
  └── Timing-safe comparison (CryptographicOperations.FixedTimeEquals)
  └── Secret protected with ACL (production: DPAPI-encrypted)

Layer 4: Message validation
  └── Size limit before JSON parse (64 KiB)
  └── Strict JSON schema (MaxDepth, enum whitelist, regex)
  └── URL schema whitelist (http/https only)
  └── Replay protection (60s nonce window)
  └── Rate limiting (token bucket per profile)

Layer 5: Process isolation
  └── No code execution from message contents
  └── No shell start, no Process.Start with user input
  └── Hardcoded paths for broker start
  └── Server identity verification at pipe connect
  └── SQLite DB read-only only, schema validation, no writes

Layer 6: Extension isolation
  └── No content scripts
  └── No externally_connectable
  └── Minimal permissions
  └── allowed_extensions in NMH manifest (browser-enforced)
```

## 5. Security review checklist

- [ ] No `System.Net.Sockets.TcpListener` or `UdpClient` in codebase
- [ ] No `Process.Start()` with variable arguments
- [ ] No `Assembly.Load()` or `Activator.CreateInstance()` with user input
- [ ] All message fields validated against schema
- [ ] HMAC comparison is timing-safe (`CryptographicOperations.FixedTimeEquals`)
- [ ] URL schema whitelist contains only `http` and `https`
- [ ] Size limit enforced before JSON deserialization
- [ ] `JsonSerializerOptions.MaxDepth` set to 4
- [ ] Extension has no content scripts
- [ ] Extension declares no `externally_connectable`
- [ ] Named Pipe DACL: Deny Everyone, Allow only owner
- [ ] `PipeOptions.CurrentUserOnly` is set
- [ ] Server PID verified at client connect
- [ ] Mutex prevents duplicate broker start
- [ ] Secret file has ACL: owner read only
- [ ] Directory ACL: no inheritance, owner FullControl only
- [ ] Rate limiting implemented and tested
- [ ] Replay protection implemented and tested
- [ ] Broker exits on inactivity (60s)
- [ ] NMH exits on pipe closure (30s)
- [ ] SQLite DB opened in read-only mode exclusively
- [ ] Schema validation before first query (PRAGMA table_info)
- [ ] Graceful fallback to legacy detection on schema mismatch
- [ ] Only `Profiles` table is read, no other tables
- [ ] DB connection closed after each access
- [ ] `--status` verifies all security invariants

## 6. Conclusion

The Tab Bridge architecture is designed to **not worsen the existing security posture of the Windows system**. The core argument: an attacker capable of circumventing Tab Bridge's security measures (local user access with filesystem and registry access) already has full access to all browser profile data and could directly read and write `places.sqlite`, `logins.json`, etc. Tab Bridge opens no new attack vector beyond what the existing user context already provides.

The most important invariant: **no open network port.** As long as this invariant holds, Tab Bridge cannot serve as an entry point for remote attacks. The six-layer defense-in-depth architecture ensures that even if one layer fails, the remaining layers contain the damage.
