# Standalone start page

Status: ready-for-agent
Type: task
Blocked by: 06

Spec: `.scratch/timewatch-v1/spec.md`.

The only surface that starts a Watch Window in v1. Served by the plugin, authenticated,
bookmarkable, usable on a phone.

## Scope

- Start a Window (duration pre-filled from `DefaultWindowMinutes`), show Remaining Time,
  cancel.
- Must work for a non-admin user — this is deliberately NOT the dashboard page.
- No bundle view, no suggestions list: those are out of scope for v1.

## Acceptance

Loads and works against the real API; no framework, no build step — plain HTML served as an
embedded resource.

Covers user stories 1, 3, 4, 9.
