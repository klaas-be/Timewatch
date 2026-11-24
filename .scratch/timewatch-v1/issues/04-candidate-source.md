# Candidate source: the Jellyfin adapter

Status: ready-for-agent
Type: task
Blocked by: 01

Spec: `.scratch/timewatch-v1/spec.md`. **Read the API facts file before writing any
query** — exact signatures for `ILibraryManager.GetItemList`, `InternalItemsQuery`,
`IUserDataManager.GetResumeUserDataBatch` and `ITVSeriesManager.GetNextUp` are there, and
several changed in 12.x.

Implement the port the engine consumes for Candidates, plus its Jellyfin-backed adapter.
The port lives in `Domain/` and speaks only in domain types; the adapter lives outside it.

## Scope

- Query movies and episodes for a user: unwatched or in progress, not virtual, recursive,
  restricted to `TimewatchSettings.LibraryIds` when non-empty.
- Read resume positions with the BATCH user-data API. A per-item call over a whole library
  is an N+1 and will be rejected in review.
- Mark which Candidate is the user's native next-up episode, via `ITVSeriesManager`.
- Populate series id/name so the engine can enforce one Suggestion per series.
- Fully watched items must not appear.

## Acceptance

The adapter cannot be unit tested (Jellyfin assemblies are absent at test runtime — see the
note in ticket 01), so keep it a thin, obvious translation with no decision-making: all
filtering choices that *can* live in the engine must live in the engine. Any logic worth
testing belongs behind the port and gets tests there. Compiles clean.

Covers user stories 29, 33, 54, and feeds 26-28.
