# Planned 5–7 minute demo script

Recording and submission are intentionally deferred until prototype verification and candidate walkthrough approval.

## 0:00–0:40 — Purpose and architecture

- State the assignment: C# API plus vanilla JavaScript file browser.
- Show the README request-flow diagram.
- Explain the configured-root and relative `/`-normalized path contract.

## 0:40–2:40 — Feature demonstration

- Browse Home and a nested folder; point out breadcrumbs, counts, sizes, and URL deep link.
- Search recursively and reload the URL to prove state restoration.
- Upload and download a small evidence file.
- Create a folder, rename/move it, copy it with nested content, then delete it.
- Show explicit overwrite and recursive-delete choices.

## 2:40–3:35 — Failure and SDET demonstration

- Trigger a duplicate destination conflict.
- Select/list a source, remove it outside the app, then attempt copy.
- Show controlled stale-source feedback, refreshed UI, no partial destination, and a still-responsive server.

## 3:35–4:45 — Backend tour

- `FilesController`: thin HTTP/stream boundary.
- `FileBrowserService`: operation semantics and staged copy/upload.
- `SafePathResolver`: canonical containment, separator normalization, root/link rejection.
- Exception middleware: safe Problem Details and trace ID.

## 4:45–5:30 — Frontend tour

- `api.js`: one fetch/error boundary.
- `app.js`: URL state, safe DOM rendering, operation dialogs, stale refresh.
- `index.html`/CSS: semantics, keyboard focus, responsive design, no UI library.

## 5:30–6:20 — Tests, challenge, and tradeoffs

- Show test matrix and passing suite.
- Explain the stale-selection/TOCTOU challenge and atomic sibling staging.
- State production exclusions honestly: auth, malware scanning, quotas, and durable audit.

## 6:20–7:00 — AI disclosure and close

- Summarize AI use exactly as documented.
- Explain that scope decisions, review, risk priorities, and final validation remained human-directed.
- Close on Quality Cube evidence: quality dimensions tied to business-risk reduction, not decoration.
