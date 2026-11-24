# Injected client: teaser, End Card, suppression

Status: ready-for-agent
Type: task
Blocked by: 06, 08

Spec: `.scratch/timewatch-v1/spec.md`. **API facts file has the exact client-plugin
constructor contract, the native trigger thresholds, the stable CSS hooks and both
suppression switches. Read it first.** Note the two UNVERIFIED assumptions at the end of
that section — if the constructor never fires, stop and report rather than working around
it by patching the bundle.

The injected script, served as an embedded resource by ticket 06's anonymous endpoint.

## Scope

- Register as a client plugin: `window.Timewatch = async () => class { constructor({ playbackManager, events, inputManager, confirm, ServerConnections }) {...} }`.
  Never patch minified chunks or hardcode mangled identifiers.
- Do nothing at all when no Watch Window is active.
- While a Window is active: suppress the native overlay (the per-user localStorage flag,
  re-read every tick) and the native auto-advance (`playbackManager._playNextAfterEnded =
  false`), BEFORE media end — reacting to `playbackstop` is too late. Treat both as a lease:
  restore on Window end/cancel, and reconcile on script load so a crash cannot leave the
  native overlay permanently off.
- Teaser at the native moment (~40s remaining, episodes only): small, native-styled, names
  the top Suggestion, offers "Start now" and "Choose…", and MUST NOT cover the video.
  Persists until the item ends if ignored.
- End Card: opens on "Choose…", and automatically at media end. Shows `SuggestionCount`
  Suggestions with Effective Runtime and Slack labels. Pick plays immediately. Dismiss marks
  played and refills the slot.
- Spent state: says the time is up, offers nothing, has NO extend control.

## Acceptance

Not unit testable (no Jellyfin, no browser here) — verified by hand on the live server. Keep
it one dependency-free script, defensive about everything it reads off the client, and
degrade to inert rather than broken if anything it expects is missing.

Covers user stories 11-18, 19-35, 39, 41-50.
