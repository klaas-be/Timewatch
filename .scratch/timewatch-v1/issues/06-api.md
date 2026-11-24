# HTTP API

Status: ready-for-agent
Type: task
Blocked by: 02, 03, 04, 05

Spec: `.scratch/timewatch-v1/spec.md`. API facts file: controller auto-registration, the
explicit `[Route]` requirement, `MediaBrowser.Common.Api.Policies`, and the fact that there
is NO global auth fallback — an action without `[Authorize]` is anonymous.

Thin controllers over the engine, the store, the candidate source and dismiss. No domain
logic in controllers.

## Scope

- Authenticated: start a Watch Window (duration), cancel it, read its status (Remaining
  Time), read Suggestions, dismiss an item. The acting user comes from the request's
  authenticated user, never from a client-supplied id.
- Anonymous, and ONLY this: serving the injected script. A `<script src>` carries no auth
  token. Everything else is `[Authorize]`.
- The script response honours `TimewatchSettings.DeveloperMode`: no-cache when on,
  cache-busted by plugin version when off.
- Explicit `[Route("Timewatch")]`; Jellyfin's base route convention is `[controller]`.

## Acceptance

Compiles clean; endpoints named in a short table in the ticket's completion note so ticket
09 can code against them without reading the controllers.

Covers user stories 1, 3, 4, 35, 36, 55.

## Comments

### Completion note — the endpoint table

All routes are relative to the server's base URL. Everything on `TimewatchController` is
`[Authorize]`; the script is the only anonymous endpoint and lives on its own controller so
that no action can drift into the wrong one. Durations cross the wire as whole seconds,
truncated, so Remaining Time is never overstated.

| Method | Route | Auth | Request | Response |
| --- | --- | --- | --- | --- |
| GET | `/Timewatch/WatchWindow` | authenticated | — | 200 `WatchWindowStatus` |
| POST | `/Timewatch/WatchWindow` | authenticated | query `minutes=120`, omitted for the admin default; no body | 200 `WatchWindowStatus`; 400 when the duration is not between a minute and 24 hours |
| DELETE | `/Timewatch/WatchWindow` | authenticated | — | 204; cancelling with nothing active is not an error |
| GET | `/Timewatch/Suggestions` | authenticated | query `playingItemId`, `playingSeriesId` (both optional; omit when nothing is playing) | 200 `SuggestionsResponse`; 404 when no Watch Window is active |
| POST | `/Timewatch/Suggestions/{itemId}/Dismiss` | authenticated | route `itemId` | 204; 404 when the id is not a single film or episode |
| GET | `/Timewatch/timewatch.js` | **anonymous** | `?v=` accepted and ignored | 200 `application/javascript`; 304 when validated |

Responses are **PascalCase**, and a null is omitted rather than written: that is Jellyfin's
own serializer configuration (`JsonDefaults.Options` has no naming policy and
`DefaultIgnoreCondition = WhenWritingNull`), so an absent key means null. No endpoint takes
a request body — that configuration is also case-sensitive, so `{ "minutes": 120 }` would
have bound to nothing and silently started a default-length window, where a query
parameter binds whatever its case.

`WatchWindowStatus`:

```json
{ "Active": true, "EndsAt": "2026-11-24T22:30:00+00:00", "RemainingSeconds": 5400, "DefaultWindowMinutes": 120 }
```

When nothing is active, `Active` is false, `RemainingSeconds` is 0 and `EndsAt` is absent.
Show Remaining Time by counting down to `EndsAt` rather than from `RemainingSeconds`, so a
sleeping tab cannot drift. `DefaultWindowMinutes` travels with the status so the start page
can prefill it without reading the plugin configuration, which needs an admin.

`SuggestionsResponse`:

```json
{ "Spent": false, "RemainingSeconds": 5400,
  "Suggestions": [
    { "ItemId": "…", "Title": "Pilot", "SeriesId": "…", "SeriesName": "Severance",
      "EffectiveRuntimeSeconds": 2580, "SlackSeconds": 1020, "IsNativeNextUp": true }
  ] }
```

`Spent: true` comes with an empty `Suggestions`; a film omits `SeriesId` and `SeriesName`.
`SlackSeconds` is negative for a Suggestion that overruns within the Overshoot Tolerance,
and the End Card should label it as such.

Notes for tickets 07 and 09:

- A Watch Window whose end time has passed reads as absent, so `GET /Timewatch/Suggestions`
  answers 404 rather than `spent: true` for it. A client that was running a Window treats
  that 404 as "the time is up": show the Spent card and release both suppression leases.
- Pass `playingItemId` whenever something is playing. The item now playing is a
  part-watched Candidate in the top ranking tier and holds its series' one-Suggestion slot,
  so without it the teaser suggests the episode already on screen.
- There is no pick endpoint. Picking plays through the client's own `playbackManager`; the
  server has nothing to add to that, and playlist creation is out of scope.
- Dismiss needs no active Window: it is a real watched-state write for the acting user. The
  freed slot refills on the next read, because the item is no longer a Candidate.
