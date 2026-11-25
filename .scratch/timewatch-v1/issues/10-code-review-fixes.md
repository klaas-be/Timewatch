# Code review fixes

Status: resolved
Type: task
Blocked by: 01, 02, 03, 04, 05, 06, 07, 08, 09

Findings from `/code-review high` over `main..feat/timewatch-v1`, highest first. Fix all of
them. Line numbers are as of commit e7284ed.

## High

1. **`Injection/timewatch.js:898` — the suppression leases are never acquired after a page
   reload during an active Watch Window.** `phase` is seeded from `WINDOW_MARKER_KEY`
   (`phase = readKey(...) ? PHASE_ACTIVE : PHASE_INERT`) without calling `leases.acquire()`,
   while `held` stays `false`. `reconcileOnLoad()` (:912) then *restores* the native overlay
   flag and deletes the lease key. The first poll returns 200/not-spent so `nextPhase` yields
   `PHASE_ACTIVE`, but `enterPhase` early-returns at :995 because `next === phase`, so
   `acquire()` never runs and `reassert()` (:372) no-ops forever.
   Repro: start a 2h Window, play an episode in the web client, press F5. For the rest of that
   session the native Up Next overlay is back on and `_playNextAfterEnded` is at its default,
   so Jellyfin auto-advances at media end — the exact behaviour this plugin exists to prevent.
   The comment at :910-911 states the intent that does not happen.

## Medium

2. **`timewatch.js:1030` — `poll()` returns an already-resolved promise when `inFlight`**, so
   a caller awaiting fresh data gets stale data. `dismiss` (:967) therefore re-opens the End
   Card with the previous `suggestions` array, still containing the item just dismissed.
   Repro: End Card opened by `onPlaybackGone` (which polls at :1165), click dismiss — the item
   stays listed and the slot does not refill until the next 15s tick. The spec requires the
   freed slot to refill immediately (user story 39).

3. **`timewatch.js:671` — `mountPoint()` prefers `.upNextContainer`**, which in jellyfin-web's
   video OSD ships with the `hide` class (`display: none !important`) and is only un-hidden by
   the native Up Next dialog rendering — which we deliberately suppress, so it never is. Teaser
   and End Card would then be appended inside a `display:none` ancestor and be invisible;
   `position: fixed` does not rescue a node whose parent is `display:none`. This would make the
   plugin look completely inert exactly where it matters. Mount on `document.body` and use the
   container only as a positioning reference, unless you can show the container is visible.

4. **`timewatch.js:134` — cancelling a Watch Window tells the user "your time is up".**
   `DELETE /Timewatch/WatchWindow` makes Suggestions answer 404, and `nextPhase` maps 404 with
   a non-inert previous phase to `PHASE_SPENT`, popping the modal Spent card (:1019). User
   story 4 is "cancel, so that I can go back to normal Jellyfin behaviour": the leases are
   released correctly, but the user who just cancelled gets a stopping overlay to dismiss.
   A 404 alone cannot distinguish cancel from expiry, so this needs a server-side signal —
   prefer a discriminator on the status endpoint saying why the Window ended (cancelled vs
   expired). On cancelled, go inert silently; keep the stopping card for genuine expiry.

## Low

5. **`timewatch.js:1123` — `maybeShowTeaser` early-returns when `teaserShownForItem ===
   playback.id`**, making `ui.showTeaser`'s own re-render path (its `dataset.itemId` check at
   :777, documented at :770-771) unreachable. A teaser naming Suggestion A keeps naming A after
   A stops being offered, and "Start now" then plays something the engine would not suggest.

6. **`timewatch.js:1037` — the poll loop's fulfilled handler is unguarded and
   `schedulePoll()` is its last statement.** A throw in `enterPhase` / `ui.showSpent` /
   `ui.hideOverlay` skips it, and as the fulfilled handler of a two-argument `.then` the
   sibling rejection handler does not catch it — polling stops permanently and the client is
   frozen in its last phase. Wrap the body in try/finally so `schedulePoll()` always runs.
   The file's own contract (header :16-17) is that surprises degrade rather than end the feature.

7. **`timewatch.js:1156` — "did the item finish?" is decided from `previous.remainingSeconds`**,
   a snapshot taken by a 1s `setInterval`. Browsers throttle background-tab timers to roughly
   once a minute while video keeps playing, so when an episode ends in a backgrounded tab the
   snapshot can be a minute stale, `remaining <= ENDED_WITHIN_SECONDS` is false, and the End
   Card never opens. The `playbackstop` handler (:1188) reads the same stale `lastPlayback`.
   Read the position at stop time rather than from the cached tick.

8. **`Storage/FileWatchWindowStore.cs:127` — the catch filter omits `IOException` and
   `UnauthorizedAccessException`** although the docstring says an "otherwise unreadable" file
   means no windows. A backup or antivirus holding `watch-windows.json` open, or wrong
   permissions after a container volume remount, turns every status and suggestions request
   into a 500 and freezes the script's phase machine.

9. **`FileWatchWindowStore.cs:165` — `catch { File.Delete(temporaryPath); throw; }`** replaces
   the original exception if the delete itself throws (file still locked on Windows, read-only
   directory), so the real cause of a write failure never reaches the log. Wrap the cleanup in
   its own try/catch.

10. **`Injection/WebClientPatches.cs:104` — `(string?)entry` on a non-primitive `JToken`
    throws** `InvalidCastException`/`ArgumentException`, and the surrounding catch only covers
    `JsonException` from `JObject.Parse`. If another plugin or a future jellyfin-web puts an
    object into `config.json`'s `plugins` array, the exception escapes into File
    Transformation's serving path instead of returning the contents unchanged as documented.
    Guard with `entry.Type == JTokenType.String` or a guarded `entry.Value<string>()`.

11. **`Api/startPage.html:268` — the sign-in panel invites "an access token (or an admin API
    key)"** but `TimewatchController.ActingUserIdAsync` (:251) returns null when
    `authorization.UserId` is empty, which is exactly what an API key gives. Pasting one always
    401s with a misleading message. Drop the API-key wording, or say only a user access token
    works.

12. **`Api/TimewatchController.cs:113` — `BadRequest(string)` on a
    `[Produces("application/json")]` controller** serialises the message as a JSON string, and
    the page renders `response.text()` verbatim (`startPage.html:399`), so the user sees the
    message wrapped in quotes. Return `ProblemDetails`, or parse the body on the page.

13. **`.gitignore:16` — `.claude/` ignores the whole directory** under the heading "Agent
    worktrees", but only `.claude/worktrees/` is one. A project-level `.claude/settings.json`
    would be silently untrackable. Narrow it.

## Reviewed and found correct — do not churn these

`SuggestionEngine` fit / Fillable / ranking / one-per-series including the magnitude-based
Slack tiebreak and the deterministic FNV tiebreak; `WatchWindow` UTC normalisation and record
copy semantics; `MediaTicks` nullable handling; `WholeSeconds` truncation direction; the store's
atomic temp-file-plus-rename and its per-path gate; `BuildScriptUrl` / `InjectScriptTag`
idempotency and base-URL handling; the reflection registration's exception translation; both
cache policies; `apiRoot` derivation on the start page; and the deliberate
Spent-when-only-one-candidate behaviour.

## Acceptance

- Every finding fixed, or argued down in writing with evidence.
- Findings 1, 2, 5, 6 and 7 are logic errors in code that has runnable tests
  (`tests/js/timewatch.test.cjs`, run with `node --test`): add a failing test first.
- `dotnet test Timewatch.slnx` green and `node --test tests/js/timewatch.test.cjs` green.

## Answer

All thirteen findings are fixed on `ticket/10-review-fixes`; none was argued down.

| # | What was done |
| --- | --- |
| 1 | The Window marker now drives `enterPhase(PHASE_ACTIVE)` at construction instead of seeding `phase`, so both leases are acquired after a reload. Test: "a reload during an active Watch Window takes both suppression leases again". |
| 2 | A poll made while one is in flight is that same request; `refresh()` is the awaited operation. Test: "a refresh resolves with fresh data, never with what the last poll knew". |
| 3 | Overlays mount on `document.body`. The safe route, as asked: `.upNextContainer` could not be observed on a live client. |
| 4 | The store remembers a cancellation; `WatchWindowStatus` carries `EndedReason` ("Cancelled"/"Expired"); the client asks for it on the transition out of an active Window and goes inert silently on a cancel. Endpoint table in ticket 06 updated. |
| 5 | The per-item teaser latch is gone, so `showTeaser`'s re-render path is reached. Test: "the teaser follows the Suggestion on offer instead of latching onto the first". |
| 6 | The poll handlers are try/finally around `schedulePoll()`, and a phase change is applied through a guard. Test: "a surprise while handling a response degrades the feature instead of ending polling". |
| 7 | New `remainingAtStop` carries the last snapshot forward by the wall clock (and not at all when it was taken while paused). Test: "an episode that ends in a throttled background tab still counts as finished". |
| 8 | The store's read filter is `IOException or UnauthorizedAccessException or JsonException or FormatException`. |
| 9 | The temp-file cleanup is wrapped so it cannot replace the original exception. |
| 10 | `AddClientPlugin` checks `JTokenType.String` instead of casting. Test: "AddsThePluginAlongsideEntriesThatAreNotStrings". |
| 11 | The sign-in panel asks for a user access token and says an API key will not do. |
| 12 | The refused duration is `ProblemDetails`; the start page reads `detail` out of a failed response body. |
| 13 | `.gitignore` narrowed to `.claude/worktrees/`. |

Nothing in the "reviewed and found correct" list was touched. The release zip is unchanged
in shape: one new source file, no new embedded resource.

Still wants live verification on a server: that the overlays are visible mounted on the
body in the video OSD (finding 3), and that the start page renders the ProblemDetails
`detail` for a refused duration (finding 12) — neither has a seam the suites can reach.
