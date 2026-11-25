# Domain types: Candidate, Suggestion, Watch Window

Status: resolved
Type: task
Blocked by: —

Spec: `.scratch/timewatch-v1/spec.md`. Vocabulary: `GLOSSARY.md`. API facts (do not
re-research): `/tmp/claude-1000/-home-klaas-Timewatch/77a38f29-16c5-43af-885c-dd6dc9ef94df/scratchpad/notes/jellyfin-12.4-api-facts.md`.

Establish the Jellyfin-free domain vocabulary everything else is built on, in
`src/Jellyfin.Plugin.Timewatch/Domain/` next to the existing `TimewatchSettings`.

Jellyfin's assemblies are referenced with `ExcludeAssets=runtime` and are therefore absent
from test output. Nothing in `Domain/` may derive from, expose, or reference a Jellyfin
type — that is what makes the engine testable at all. Items are identified by `Guid` and
described by plain data.

## Scope

- `Candidate`: item id, title, series id and series name (both null for a film), full
  runtime, playback position, whether it is the user's native next-up episode, and enough
  to express the ranking tiers (in progress / next up / cold). Exposes **Effective
  Runtime** = full runtime minus playback position.
- `WatchWindow`: owning user id, UTC end time. Computes **Remaining Time** against a
  supplied instant. Must not read the clock itself — take time as a parameter so tests can
  fix it.
- `Suggestion`: the chosen Candidate plus its **Slack** against a given Remaining Time.
- Whatever result type expresses "here are the Suggestions" versus "**Spent**", so callers
  cannot confuse an empty list with a spent window.

## Acceptance

- Unit tests for Effective Runtime (unwatched, part-watched, zero/absent runtime), Remaining
  Time (before, at, after the end time), and Slack sign (positive = dead air, negative =
  overshoot) as the glossary defines them.
- `dotnet test Timewatch.slnx` green; no Jellyfin type reachable from `Domain/`.

Covers user stories 2, 3, 21, 22, 34, 41.
