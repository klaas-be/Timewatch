# Watch Window store and lifecycle

Status: ready-for-agent
Type: task
Blocked by: 01

Spec: `.scratch/timewatch-v1/spec.md`. API facts file as in ticket 01 (`DataFolderPath`).

At most one active Watch Window per user, persisted as JSON under
`BasePlugin.DataFolderPath`, keyed by user id, end times stored in UTC. NOT in the plugin
configuration XML — that is for dashboard settings, and rewriting it per Window start would
abuse it.

## Scope

- A port (interface) in `Domain/` for reading/writing/clearing the Window for a user, and a
  file-backed implementation. The port must be fakeable without touching disk.
- Start replaces any existing Window for that user. Cancel clears it. A Window whose end
  time has passed reads as absent (expiry needs no sweeper).
- Concurrent access from several requests must not corrupt or truncate the file.
- Survives a server restart.

## Acceptance

- Tests against the port with a fake: replace-on-start, cancel, expiry-on-read, isolation
  between two users.
- Tests of the file implementation using a temporary directory: round-trip, restart
  (re-read from a new instance), corrupt/empty file reads as absent rather than throwing.

Covers user stories 5, 6, 7, 8, 10.
