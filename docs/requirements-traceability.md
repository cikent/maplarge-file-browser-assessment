# Requirements traceability

This matrix maps the provided MapLarge assignment and follow-up email expectations to implementation and evidence. “Selected bonus” means the candidate intentionally elevated that optional assignment item into submission scope.

| Requirement | Implementation | Verification evidence |
|---|---|---|
| C# web API | ASP.NET Core [.NET 8 project](../TestProject/TestProject.csproj) and [controller](../TestProject/Controllers/FilesController.cs) | Solution build and API integration tests |
| Vanilla JavaScript/TypeScript UI; no UI framework | Static [HTML](../TestProject/wwwroot/index.html), [CSS](../TestProject/wwwroot/styles.css), and ES modules ([app](../TestProject/wwwroot/scripts/app.js), [API client](../TestProject/wwwroot/scripts/api.js)) | Static-app smoke test and browser prototype checks |
| Browse files and folders as JSON | `GET /api/files/browse` with portable entry contracts | Browse service and API tests |
| Search files and folders as JSON | `GET /api/files/search`, recursive case-insensitive filename matching | Search match, cap, and API checks |
| Configurable server home | `FileBrowser:RootPath` in [configuration](../TestProject/appsettings.json), overridable as `FileBrowser__RootPath` | Integration factory uses a unique temporary root |
| Working end-to-end UI/API flow | URL state → fetch → controller → service → filesystem → JSON → DOM | End-to-end API workflow and browser prototype |
| Upload and download | Staged multipart upload; streamed range-enabled download | File CRUD unit/API workflow and manual download |
| Current-view counts and sizes | `DirectorySummaryResponse` for browse and search result views | Mixed-entry summary unit test and rendered summary cards |
| URL deep linking | `path` and `q` query state plus History API navigation | Direct nested-path/search prototype checks |
| Client-side HTML rendering | DOM nodes created with `createElement` and `textContent` | Source inspection and browser rendering checks |
| Dialog component (selected bonus) | Native accessible dialogs for create, move/copy, and delete | Keyboard and focus prototype checks |
| Delete files and folders (selected bonus) | `DELETE` route with explicit recursive flag | File/folder CRUD, non-empty guard, stale-source tests |
| Move files and folders (selected bonus) | Shared move/rename contract with collision and descendant checks | File/folder CRUD and invalid descendant test |
| Copy files and folders (selected bonus) | Atomic sibling staging and publish-on-success | File/folder CRUD, cancellation cleanup, stale-source tests |
| Performance attention | Bounded search, streaming I/O, staged transfers, framework-free UI | Complexity review, truncation test, prototype timing |
| “Cool stuff” without scope drift | Quality Cube, reviewer traceability, safe portable paths, accessibility | README evidence and prototype review |
| 5–7 minute video | Feature/code/challenge/AI disclosure plan | [Demo script](demo-script.md); recording remains separately gated |
| AI disclosure | Specific assistance and human verification statement | [AI use disclosure](ai-usage.md) |

## Submission boundary

The implemented scope is deliberately a file browser, not a general algorithm demonstration. The Quality Cube adds SDET personality by tying six quality dimensions to visible evidence; it does not replace or distract from assignment behavior.
