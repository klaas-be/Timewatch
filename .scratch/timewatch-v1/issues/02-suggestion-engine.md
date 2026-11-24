# SuggestionEngine: fit, fillability, ranking

Status: ready-for-agent
Type: task
Blocked by: 01

Spec: `.scratch/timewatch-v1/spec.md` (read "Testing Decisions" in full — the engine is the
only designed seam in the codebase and the full list of required test cases is there).

Implement `SuggestionEngine` in `Domain/`: given a `WatchWindow`, an instant, a
`TimewatchSettings` and a set of `Candidate`s, return the ordered `Suggestion`s or the
Spent result. No Jellyfin types, no I/O, no clock reading, no `Random` without an injected
seed.

## Scope

- Fit: a Candidate fits when its Effective Runtime is within Remaining Time plus
  `OvershootTolerance`.
- **Fillable**, one step only: after watching it, at least one other Candidate fits the
  remainder, or the remainder is small enough to count as spent. Only Fillable Candidates
  become Suggestions.
- Ranking, in this order: the native next-up episode when it fits; then items in progress;
  then next-up episodes of other series; then unwatched films and pilots. Least Slack wins
  within a tier. Randomise only between otherwise-equal Candidates, from an injected seed so
  tests are deterministic.
- At most one Suggestion per series.
- Honour `SuggestionCount`; return fewer when fewer are Fillable.
- Spent when the end time has passed OR nothing fits.

## Acceptance

Every bullet in the spec's Testing Decisions list has a test. Tests assert only on what is
offered and in what order — never on intermediate scores or private helpers.

Covers user stories 19-34, 42, 44.
