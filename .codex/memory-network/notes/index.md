# Project Memory

## Active Goal

Build `learning-memory-notes`, a local-first app for quick study note capture, project organization, knowledge trees, and generated associative memory networks.

## Decisions

- Use dependency-free HTML/CSS/JS for the first version because the current environment has Node but no working `python`, `py`, or `gh`.
- Store user data in browser `localStorage` initially, with JSON export as a safety path.
- Add a WPF desktop floating note executable for browser-independent quick capture.

## Recent Changes

- Added `learning-memory-notes/desktop-floating-note`, a .NET WPF project that builds `MemoryNotesFloating.exe`.
- Desktop notes default to `E:\MemoryNotes\data\notes.json`, with `%APPDATA%\MemoryNotes\notes.json` as fallback.

## User Preferences

- Default all project data to E drive when the environment supports it.
- Mirror project knowledge into Obsidian notes with tags and ops runbooks.
- For new ideas and deployments, update project notes and push relevant Git repositories.

## Obsidian

- Vault: `C:\Users\JC\Documents\Obsidian Vault`
- Added notes for MemoryNotes and BearingDiagnose.
- Added project workflow note for `project-ops-memory`.
- Obsidian Vault is now a standalone local Git repository.
- First local vault commit: `f8524c9 Initialize Obsidian project vault`.
- Obsidian remote: `https://github.com/jsbsjsisdhdbsjwjs/obsidian`
- MemoryNotes desktop captures now sync into Obsidian `Projects/` and `Ops/` notes.

## Risks

- GitHub upload is not complete until a remote repository URL is provided or GitHub CLI/auth is available.
- The memory-network CLI could not run because Python is not installed.
- Keep Obsidian Vault and MemoryNotes repo pushes separate.
