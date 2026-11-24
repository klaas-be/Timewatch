# Timewatch

A Jellyfin 12.4 server plugin that time-boxes an evening's watching: you set a duration,
and suggestions that fit the remaining time replace the web client's native "Up Next"
behaviour until the time is up.

See `GLOSSARY.md` for the domain vocabulary and `.scratch/timewatch-v1/spec.md` for the v1
spec.

## Agent skills

### Issue tracker

Issues and specs live as markdown files under `.scratch/<feature-slug>/` in this repo.
See `docs/agents/issue-tracker.md`.

### Triage labels

The five canonical triage roles, with label strings equal to their names, recorded as a
`Status:` line in each issue file. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: `GLOSSARY.md` and `docs/adr/` at the repo root. See `docs/agents/domain.md`.
