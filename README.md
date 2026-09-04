# Quality File Browser — MapLarge Test Project

A C# ASP.NET Core API and vanilla JavaScript single-page application for safely browsing and changing files beneath one configured server directory. The implementation favors correct, demonstrable behavior over framework or visual complexity.

## Reviewer navigation

- [Requirements traceability](docs/requirements-traceability.md)
- [Architecture and request flow](docs/architecture.md)
- [Safe relative-path contract](#safe-relative-path-contract)
- [Supported operations](#supported-operations)
- [Quality Cube](#quality-cube)
- [Test strategy and results](docs/test-plan.md)
- [Security and accessibility](#security-and-accessibility)
- [Performance and complexity](#performance-and-complexity)
- [Tradeoffs](#tradeoffs)
- [AI use disclosure](docs/ai-usage.md)
- [Backend project](TestProject/TestProject.csproj),
  [application entry point](TestProject/Program.cs),
  [service tests](TestProject.Tests/FileBrowserServiceTests.cs), and
  [API tests](TestProject.Tests/FileBrowserApiTests.cs)
- [Core-code interview guide](docs/interview-guide.md) and [5–7 minute demo script](docs/demo-script.md)

## Quick start

Prerequisite: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or newer.

```powershell
dotnet restore .\TestProject\TestProject.sln
dotnet run --project .\TestProject\TestProject.csproj
```

Open the HTTP or HTTPS URL printed by ASP.NET Core. The default home is [`TestProject/SampleFiles`](TestProject/SampleFiles/). To browse another directory for the current shell session:

```powershell
$env:FileBrowser__RootPath = "C:\Path\To\Safe\Demo\Directory"
dotnet run --project .\TestProject\TestProject.csproj
```

Do not point this proof of concept at sensitive data. It intentionally supports destructive operations and has no authentication because it is designed for local assessment use, not public deployment.

## Architecture and request flow

```text
┌──────────────────────────────┐
│ Browser: HTML + CSS + JS     │
│ URL state, dialogs, rendering│
└──────────────┬───────────────┘
               │ JSON / streams
               ▼
┌──────────────────────────────┐
│ FilesController              │
│ HTTP contract only           │
└──────────────┬───────────────┘
               ▼
┌──────────────────────────────┐
│ FileBrowserService           │
│ Browse/search/CRUD/copy      │
└──────────────┬───────────────┘
               ▼
┌──────────────────────────────┐
│ SafePathResolver             │
│ Normalize + contain + reject │
└──────────────┬───────────────┘
               ▼
┌──────────────────────────────┐
│ Configured server home only  │
└──────────────────────────────┘
```

The thin [API controller](TestProject/Controllers/FilesController.cs) delegates behavior to [FileBrowserService](TestProject/Services/FileBrowserService.cs). Every operation independently passes through [SafePathResolver](TestProject/Services/SafePathResolver.cs); the UI cannot grant itself access by changing client state. See the [architecture notes](docs/architecture.md) for endpoint and complexity details.

## Safe relative-path contract

The API exchanges paths relative to the configured home and normalizes separators to `/`:

| Server-native path | API path |
|---|---|
| `C:\Demo\reports\summary.txt` | `reports/summary.txt` |
| `/srv/demo/reports/summary.txt` | `reports/summary.txt` |

This contract:

1. avoids disclosing absolute server paths;
2. keeps browser bookmarks portable across Windows, Linux, and macOS hosts;
3. gives the server one place to translate separators and enforce containment; and
4. rejects absolute paths, `.`/`..` traversal, linked-path escapes, and root mutation.

The path is relative to the **configured file-browser home**, not the application installation directory. Query-string state such as `?path=reports&q=quality` makes browse and search views directly bookmarkable.

## Supported operations

| Data type | Create | Read | Update | Delete | Copy |
|---|---|---|---|---|---|
| File | Upload | Browse, search, download | Move/rename, replace upload | Delete | Atomic staged copy |
| Folder | New folder | Browse, search | Move/rename | Empty or explicit recursive delete | Recursive staged copy |

Move, copy, rename, and delete are implemented for both files and folders. Native [`<dialog>`](TestProject/wwwroot/index.html) elements keep mutation flows keyboard-accessible without adding a UI framework. Collision, root, self/descendant, linked-path, cancellation, and stale-source behavior are covered in the [test plan](docs/test-plan.md).

## Quality Cube

The original cube mark is intentionally secondary to the file browser. It represents six checks applied across the delivery:

| Dimension | Evidence in this project |
|---|---|
| Functionality | Required browse/search/upload/download plus selected bonus operations |
| Correctness | Unit and API-level permutations for files, folders, collisions, and races |
| Security | Server-owned root boundary, portable paths, safe errors, linked-path rejection |
| Usability | Breadcrumbs, current-view summaries, dialogs, URL deep links, responsive layout |
| Performance | Bounded search, streamed upload/download, atomic staging, no client framework |
| Maintainability | Thin HTTP layer, focused service/resolver boundaries, traceability and tests |

The business ROI is practical: defects are prevented at the lowest-cost boundary, risky changes are independently testable, and reviewers can trace claims to evidence quickly.

## Security and accessibility

Security controls include root containment on every operation, no absolute paths in API responses, generic production-safe filesystem errors, no HTML injection from filenames (the client uses `textContent`), explicit overwrite/recursive-delete choices, and rejection of symbolic links and reparse points.

Accessibility choices include semantic landmarks and tables, a skip link, visible keyboard focus, live status announcements, explicit labels, target-specific action labels, native modal focus management, sufficient text contrast, responsive layouts, and reduced-motion support.

## Performance and complexity

- Immediate directory browse is \(O(n \log n)\) because results are sorted after one enumeration.
- Recursive search is \(O(n)\), cancellation-aware, and capped by `SearchMaxResults`.
- Upload and download use streams, so memory use is bounded independently of file size.
- Copy is \(O(b)\) in bytes copied. It stages beside the destination and publishes only after success, preventing a partial final destination.
- Vanilla JavaScript ships no client runtime dependency or build pipeline.

Configuration lives in [`appsettings.json`](TestProject/appsettings.json) and can be overridden with standard ASP.NET Core environment variables.

## Tradeoffs

- **Local assessment boundary:** authentication, authorization, quotas, malware scanning, distributed locks, and audit persistence are production concerns deliberately excluded from this compact exercise.
- **Linked files:** symbolic links/reparse points are rejected instead of resolved. This gives a clear containment guarantee with less platform-specific ambiguity.
- **Search:** filename substring search is bounded and deterministic; content indexing would add infrastructure beyond the assignment.
- **Concurrent filesystem changes:** the API revalidates when an operation begins and returns controlled `404`/`409` responses. Atomic staging protects final copy/upload destinations, but no local filesystem API can make a multi-file source snapshot immutable without stronger storage support.

## Verification

Run the complete automated suite:

```powershell
dotnet test .\TestProject\TestProject.sln
```

Collect line and branch coverage:

```powershell
dotnet test .\TestProject\TestProject.sln --collect:"XPlat Code Coverage"
```

Automated evidence is complemented by the browser-focused checks in the [test plan](docs/test-plan.md). Test counts and prototype results are recorded there after final verification.
