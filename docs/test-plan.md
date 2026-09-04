# Risk-based test plan

## Strategy

Testing is layered so failures identify the cheapest responsible boundary:

1. **Smoke:** solution builds, server starts, static client loads, root browse returns JSON.
2. **Unit:** path normalization/containment and service behavior on temporary filesystems.
3. **API integration:** serialization, status codes, middleware, multipart upload, streaming download, and complete workflows.
4. **Browser prototype:** real rendering, URL state, dialogs, keyboard use, responsive layout, and user-visible recovery.
5. **Nonfunctional:** security boundaries, accessibility heuristics, bounded search, transfer cleanup, and concise source review.

Tests use a unique temporary configured root. They never read or modify the developer's real files.

## Operation and data-type matrix

| Operation | File permutations | Folder permutations |
|---|---|---|
| Browse/read | zero-byte/non-empty, size, modified time, download | empty/non-empty, nested path, counts |
| Search | case-insensitive name, nested match, no match, cap | matching folder, nested scope, cap |
| Create | upload, duplicate, replace, oversize | valid name, invalid name, duplicate |
| Update | move, rename, destination collision, missing source | move, rename, collision, own-descendant rejection |
| Delete | existing, missing/stale | empty, non-empty without recursive, explicit recursive, root rejection |
| Copy | same/cross-folder name, collision, cancellation, stale source | recursive content, collision, own-descendant, cancellation cleanup |

## Security and boundary permutations

- Empty root and nested valid relative paths.
- Windows `\` input normalized to `/` output.
- Leading `/`, UNC/root-relative, drive-qualified, `.`, `..`, and mixed traversal rejection.
- Similar root-prefix path cannot escape canonical containment.
- Absolute physical root absent from successful and error JSON.
- Invalid leaf names and embedded separators rejected.
- Symbolic links/reparse points rejected or omitted from enumeration.
- Configured root cannot be moved, copied, renamed, or deleted.
- Destination collision never silently overwrites; upload replacement requires an explicit flag.
- Folder cannot be moved or copied onto itself or into a descendant.
- Untrusted filenames render through `textContent`, not `innerHTML`.

## Explicit stale-source / TOCTOU scenario

This scenario captures the requested boundary permutation:

1. create a source file;
2. browse it so it is a valid selected UI target;
3. delete it outside the application before copy executes;
4. submit the original relative source path;
5. expect controlled `404` Problem Details;
6. verify no final or staging destination exists;
7. verify the server remains responsive; and
8. in the browser, verify the error is announced and the stale view refreshes.

The automated service and API suites exercise steps 1–6. Browser prototype testing covers steps 7–8.

## Browser prototype checklist

- [x] Root and nested folders render; breadcrumbs navigate both directions.
- [x] A direct `?path=reports` URL restores the same view after reload.
- [x] A direct `?path=&q=quality` URL restores search and Clear returns to browse.
- [x] Counts and total matched/immediate file size update with the rendered view.
- [x] Upload/download preserve exact file content.
- [x] Create, rename/move, copy, and delete work for files and folders.
- [x] Duplicate and non-recursive non-empty delete failures are controlled and understandable.
- [x] Externally deleted selected source reports a stale-source error and refreshes.
- [x] Visible focus, native control semantics, dialog focus transfer/Escape close, and live status are usable.
- [x] A true 390 px viewport retains access to operations without page-width overflow.
- [x] Browser checks contain no uncaught JavaScript or `console.error` failures.

## Verification commands

```powershell
dotnet build .\TestProject\TestProject.sln --configuration Release
dotnet test .\TestProject\TestProject.sln --configuration Release
dotnet test .\TestProject\TestProject.sln --configuration Release --collect:"XPlat Code Coverage"
node --check .\TestProject\wwwroot\scripts\api.js
node --check .\TestProject\wwwroot\scripts\app.js
```

## Recorded results

- Release build: **passed**, 0 warnings and 0 errors.
- Automated suite: **35 passed, 0 failed, 0 skipped**.
- .NET coverage: **88.0% lines** (411/467) and **79.9% branches** (123/154). `SafePathResolver` and `FileBrowserService` each exceed 91% line coverage.
- JavaScript syntax: both ES modules passed Node's parser check.
- Live-process API prototype: index, browse, search, summaries, upload, download, file copy/move, folder copy/delete, stale-source `404`, no partial destination, and post-failure health all passed.
- Installed Edge visual review: desktop and narrow layouts passed after correcting cube overlap, singular labels, and narrow table discoverability.
- Installed Edge browser automation: direct URL state, true 390 px containment, table-action reachability, folder create/copy/move/rename/delete, dialog focus/Escape, stale-row refresh, controlled feedback, and zero browser errors passed.

Browser checks use the locally installed Edge DevTools protocol rather than adding a browser-test package to this compact submission. A candidate-led hands-on walkthrough remains the final pre-recording acceptance step.
