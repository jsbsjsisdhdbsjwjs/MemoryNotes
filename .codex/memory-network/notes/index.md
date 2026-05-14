# Project Memory

## Active Goal

Build `learning-memory-notes`, a local-first app for quick study note capture, project organization, knowledge trees, and generated associative memory networks.

## Decisions

- Use dependency-free HTML/CSS/JS for the first version because the current environment has Node but no working `python`, `py`, or `gh`.
- Store user data in browser `localStorage` initially, with JSON export as a safety path.

## Risks

- GitHub upload is not complete until a remote repository URL is provided or GitHub CLI/auth is available.
- The memory-network CLI could not run because Python is not installed.
