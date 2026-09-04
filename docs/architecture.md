# Architecture

## Design goal

Expose useful file-browser behavior while making one security invariant obvious and independently testable:

> A client-supplied path can identify only an entry beneath the configured server home.

## Ownership boundaries

```text
URL state ─▶ API client ─▶ FilesController ─▶ FileBrowserService ─▶ SafePathResolver ─▶ filesystem
   ▲              │               │                    │                    │
   └── render ◀───┴── JSON ◀──────┴── contracts ◀──────┴── relative path ◀──┘
```

- [`app.js`](../TestProject/wwwroot/scripts/app.js) owns browser state, safe DOM rendering, dialogs, and refresh behavior.
- [`api.js`](../TestProject/wwwroot/scripts/api.js) owns HTTP serialization and Problem Details parsing.
- [`FilesController`](../TestProject/Controllers/FilesController.cs) maps HTTP requests to domain operations and streams downloads.
- [`FileBrowserService`](../TestProject/Services/FileBrowserService.cs) owns browse, search, summaries, transfer semantics, staging, and CRUD behavior.
- [`SafePathResolver`](../TestProject/Services/SafePathResolver.cs) owns normalization, native translation, configured-root containment, leaf-name validation, and linked-path rejection.
- [`FileBrowserExceptionMiddleware`](../TestProject/Middleware/FileBrowserExceptionMiddleware.cs) maps expected failures to safe RFC-style Problem Details without returning physical paths.

## Relative path algorithm

For every request, the resolver:

1. accepts an empty path as the configured root;
2. rejects rooted, drive-qualified, and traversal paths;
3. accepts either separator at the API boundary and splits into segments;
4. combines those segments with the configured physical root;
5. canonicalizes the result with `Path.GetFullPath`;
6. compares the result to the root using the host filesystem's case semantics;
7. rejects existing symbolic-link/reparse-point segments; and
8. returns `/`-normalized relative paths to clients.

The check runs again when each operation executes. Client-side selection is never treated as authorization.

## API

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/files/browse?path=` | Immediate children and current-view summary |
| `GET` | `/api/files/search?path=&query=` | Bounded recursive filename search |
| `GET` | `/api/files/download?path=` | Stream one file with range support |
| `POST` | `/api/files/upload` | Staged multipart upload; optional explicit replace |
| `POST` | `/api/files/folders` | Create one folder |
| `POST` | `/api/files/move` | Move or rename a file/folder |
| `POST` | `/api/files/copy` | Staged copy of a file/folder |
| `DELETE` | `/api/files?path=&recursive=` | Delete a file or folder |

All JSON uses ASP.NET Core's camel-case defaults. Entry contracts expose `name`, portable `path`, `type`, `sizeBytes`, and `modifiedUtc`—never a physical root.

## Consistency and failures

Uploads and copies write to generated sibling staging paths and rename into the requested destination only after success. Cancellation, a disappearing source, linked content, or an I/O error triggers staging cleanup. Existing final destinations are not overwritten except when file-upload replacement is explicitly selected.

Expected failures use stable status semantics:

- `400`: malformed/unsafe path or invalid destination relationship;
- `403`: host filesystem denied access;
- `404`: source or folder disappeared before execution;
- `409`: collision or concurrent filesystem conflict;
- `413`: configured upload limit exceeded; and
- `500`: unexpected failure with a trace ID and no physical-path detail.

## Complexity

Browse enumerates \(n\) immediate entries and sorts them in \(O(n \log n)\). Search visits up to \(n\) descendants in \(O(n)\) and stops after the configured result cap. Transfers require \(O(b)\) work for \(b\) bytes; upload/download memory remains bounded by stream buffers.
