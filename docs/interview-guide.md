# Core-code interview guide

## Thirty-second explanation

“I built the requested file browser as a thin ASP.NET Core HTTP layer over a focused filesystem service. The key design decision is that the browser never sends or receives absolute server paths. Every operation re-resolves a portable relative path beneath one configured root, rejects traversal and linked-path escapes, and returns controlled failures if the filesystem changes. The vanilla JavaScript UI keeps browse/search state in the URL and uses accessible native dialogs for file and folder mutations.”

## Visual: explain the flow

```text
User action
   │
   ▼
URL + vanilla JS ──fetch──▶ Controller ──call──▶ Service
       ▲                                             │
       │                                             ▼
       └──── safe JSON ◀──── relative contract ◀─ Path resolver
                                                     │
                                                     ▼
                                            configured root only
```

## Read/write: walk through four decisions

### 1. Why relative normalized paths?

They avoid leaking machine-specific physical paths, keep deep links portable, and force all native path translation through one testable server boundary. The path is relative to the configured browser home, not the application folder.

### 2. Why separate resolver, service, and controller?

- The resolver answers **“may this path identify an entry?”**
- The service answers **“what does this file operation mean safely?”**
- The controller answers **“how does HTTP represent the request and result?”**

That separation lets path permutations run as fast unit tests while API tests focus on status and serialization.

### 3. Why stage upload and copy?

Writing directly to the requested destination can leave a valid-looking partial file or folder after cancellation/source loss. Staging under a generated sibling name and renaming only after success makes publication atomic on the same filesystem. Cleanup runs on failure.

### 4. How is a stale selection handled?

Selection is UI state, not a lock. The server resolves the source again when the operation starts. If another actor deleted it, the API returns `404` without creating the requested destination; the UI announces the problem and refreshes. This is honest TOCTOU handling for a local proof of concept.

## Aural: likely discussion prompts

- **Why not React?** The assignment asks for vanilla JavaScript/TypeScript; DOM and History APIs are sufficient and eliminate runtime/build overhead.
- **Why reject symbolic links?** Resolving links portably while preserving containment adds ambiguity. Explicit rejection gives a clear, testable boundary.
- **Why no database?** The filesystem is the assignment's source of truth; a database would introduce stale duplicated state.
- **What would production require?** Authentication/authorization, per-user roots, quotas, malware scanning, durable audit events, rate limits, hardened hosting, and possibly storage-native transactions/versioning.
- **What was challenging?** Filesystem state can change after rendering. The design treats every client selection as stale-capable and protects final transfer destinations through staging.
- **What demonstrates SDET judgment?** Requirements traceability, risk-based permutations across both data types, controlled failure contracts, direct TOCTOU coverage, accessibility checks, and evidence that does not rely on happy-path screenshots.

## Kinesthetic: hands-on teach-back

1. Set `FileBrowser__RootPath` to a disposable directory.
2. Predict the JSON path for one nested native file.
3. Place a breakpoint in `SafePathResolver.Resolve` and browse the folder.
4. List a file, delete it externally, then attempt copy from the stale row.
5. Explain why no destination appears and which layer chose the status code.
6. Change `SearchMaxResults`, restart, and verify truncation behavior.

## Quality Cube framing

The cube is not an algorithm claim or separate feature. It is a compact review heuristic: functionality, correctness, security, usability, performance, and maintainability. Each face points to concrete project evidence, turning “quality” from a slogan into a traceable engineering decision.
