# Timewatch v1 — time-boxed watch suggestions for Jellyfin

Status: ready-for-agent
Target: Jellyfin 12.4 (`net10.0`, `Jellyfin.Controller` 12.4.0, `targetAbi` 12.4.0.0)

## Problem Statement

I sit down to watch something with a real amount of time available — "I have two hours
before bed" — and Jellyfin has no idea. It offers me the next episode and a wall of
rows, all of them indifferent to my evening. Two things go wrong as a result.

First, I overshoot. The next episode auto-plays, then the one after it, and the decision
to keep going is never actually made: it happens by default, in the dark, thirty seconds
after the previous episode ended. I go to bed an hour later than I meant to.

Second, I undershoot and waste the slot. With ninety minutes left I don't start the
two-hour film, so I scroll, pick something short, and end up with forty dead minutes I
didn't want. Nothing in Jellyfin tells me "this fits" or "this doesn't", because nothing
in Jellyfin knows when I want to stop.

What I want is for my available time to be a first-class fact the server knows about, and
for the moment I'm most likely to make a bad decision — the end of an episode, when the
next one is already counting down — to become the moment I'm shown choices that fit.

## Solution

Timewatch is a Jellyfin server plugin. I tell it how long I have; it turns that into a
**Watch Window** with a wall-clock end time, held server-side for my user. While a Window
is active, Timewatch replaces the web client's native "Up Next" behaviour with its own.

About forty seconds before an episode ends I get a small popup in the same style as
Jellyfin's native one. It names the best-fitting thing to watch next — which is the
episode Jellyfin would have played anyway, whenever that still fits my remaining time —
and offers "Start now" and "Choose…". It does not cover the video: the episode is still
playing and may still be worth watching. If I ignore it, it stays, and when the episode
actually ends it opens into the **End Card**: four **Suggestions**, each labelled with how
it fits ("43 min · leaves 17 min").

Each Suggestion is a single item, and crucially only appears if it is **Fillable** — the
time left after watching it can itself be filled by something. So I am never handed a
choice that strands me with a fifty-minute hole. I pick one and it plays. If a Suggestion
is something I have decided I'm never going to watch, I **Dismiss** it: that marks it
watched in Jellyfin for real, it disappears from Next Up and Continue Watching everywhere,
and another Suggestion takes its slot immediately.

When my time is up — the end time has passed, or nothing fits any more — the End Card says
so and offers nothing. There is no "extend" button. The whole value of the feature is that
the evening has an end, and a button to push it back would hand the decision straight back
to the default I was trying to escape.

When no Watch Window is active, Timewatch does nothing at all: Jellyfin's native overlay
and auto-advance behave exactly as they do today.

## User Stories

### Setting up and running a Watch Window

1. As a Jellyfin user, I want to start a Watch Window by choosing a duration, so that the
   server knows how much time I actually have.
2. As a Jellyfin user, I want the Watch Window to be anchored to a wall-clock end time
   rather than counting runtime watched, so that a phone call, a break or a slow start
   reduce the time I have left just as they do in reality.
3. As a Jellyfin user, I want to see the Remaining Time of my active Watch Window, so that
   I know where I stand without doing arithmetic.
4. As a Jellyfin user, I want to cancel an active Watch Window, so that I can go back to
   normal Jellyfin behaviour without waiting for it to expire.
5. As a Jellyfin user, I want starting a new Watch Window while one is active to replace
   it, so that there is never more than one and Remaining Time is never ambiguous.
6. As a Jellyfin user, I want my Watch Window to survive a server restart, so that an
   evening is not reset by a service restart or a crash.
7. As a Jellyfin user, I want my Watch Window to be visible from any device I log in from,
   so that starting it on my phone and watching on my laptop works.
8. As a Jellyfin user, I want the Watch Window to belong to my user account, so that
   another household member watching at the same time is unaffected by my time box.
9. As a Jellyfin user, I want to start my Watch Window from a page I can bookmark and open
   on a phone, so that I don't need an admin account or a dashboard to begin.
10. As an admin, I want the Watch Window to expire by itself when its end time passes, so
    that no stale state accumulates.

### The teaser popup

11. As a viewer, I want a small popup near the end of an episode, so that I can decide
    what's next before the episode has finished and without dead air.
12. As a viewer, I want that popup styled like Jellyfin's native Up Next popup, so that it
    feels like part of the client rather than a bolted-on extra.
13. As a viewer, I want the popup to name the best-fitting Suggestion, so that the common
    case is a single glance and a single click.
14. As a viewer, I want the popup to offer "Start now", so that accepting the obvious
    choice is as fast as it is in stock Jellyfin.
15. As a viewer, I want the popup to offer "Choose…", so that I can see the full set of
    Suggestions when I don't want the obvious one.
16. As a viewer, I want the popup *not* to cover the video, so that content still playing
    — an outro, a stinger, a credits scene — is not hidden from me by something I didn't
    ask to open.
17. As a viewer, I want the popup to stay on screen until the episode ends if I ignore it,
    so that it doesn't vanish like a toast and leave my Window quietly running down.
18. As a viewer, I want the End Card to open automatically when the item actually ends, so
    that I am not left looking at a stopped player waiting for me to press something.

### The End Card and its Suggestions

19. As a viewer, I want the End Card to show several Suggestions, so that I have a real
    choice rather than one take-it-or-leave-it item.
20. As a viewer, I want each Suggestion to be a single item rather than a combination, so
    that I can read and judge it at a glance in an overlay.
21. As a viewer, I want each Suggestion labelled with its Effective Runtime, so that I can
    see what I'm committing to.
22. As a viewer, I want each Suggestion labelled with the Slack it would leave, so that I
    can see what the rest of my evening would look like.
23. As a viewer, I want Suggestions that fit my Remaining Time, so that nothing offered to
    me would run past the end of my Window beyond the configured tolerance.
24. As a viewer, I want a Suggestion that slightly overruns to still be offered when the
    alternative is a large gap, so that finishing a few minutes late beats twenty-five
    minutes of dead air.
25. As a viewer, I want only Fillable Suggestions, so that choosing one never strands me
    with a remainder that nothing can fill.
26. As a viewer, I want the episode Jellyfin would have played next to come first whenever
    it fits, so that the plugin respects the momentum of what I'm already watching.
27. As a viewer, I want items I am part-way through ranked above items I haven't started,
    so that finishing things is easier than starting things.
28. As a viewer, I want next-up episodes of my other in-progress series ranked above cold
    starts, so that series I'm following stay in view.
29. As a viewer, I want unwatched films and series pilots offered when nothing in progress
    fits, so that a short remainder still has something to put in it.
30. As a viewer, I want better-fitting items to win ties, so that the Suggestion that
    wastes least of my evening is the one I see first.
31. As a viewer, I want some randomness between otherwise-equal Suggestions, so that
    repeated End Cards don't show me the same four things all evening.
32. As a viewer, I want at most one Suggestion per series, so that four cards aren't four
    episodes of the same show.
33. As a viewer, I want fully watched items excluded entirely, so that the End Card is
    about what I haven't seen.
34. As a viewer, I want a part-watched item priced at its *remaining* runtime, so that a
    film I'm eighty minutes into is correctly offered as a thirty-minute option.
35. As a viewer, I want picking a Suggestion to start it playing immediately, so that
    choosing is one action and not a trip through the library.

### Dismissing

36. As a viewer, I want to Dismiss a Suggestion I've decided never to watch, so that the
    End Card stops offering it.
37. As a viewer, I want Dismiss to mark the item watched in Jellyfin for real, so that it
    also stops appearing in Next Up and Continue Watching on all my clients.
38. As a viewer, I want Dismiss to apply to only the single item shown, so that I cannot
    accidentally mark a whole season or series watched from an overlay.
39. As a viewer, I want the dismissed card's slot refilled immediately with the next-best
    Suggestion, so that the End Card doesn't shrink as I prune it.
40. As a viewer, I want no confirmation dialog on Dismiss, so that pruning is fast — I
    accept that undoing it means unmarking the item in Jellyfin's own UI.

### When the time is up

41. As a viewer, I want the End Card to tell me explicitly when my Watch Window is Spent,
    so that I get a clear stopping signal instead of a silent absence of suggestions.
42. As a viewer, I want a Spent Window to offer no Suggestions at all, so that the plugin
    doesn't undermine the limit I set.
43. As a viewer, I want no "extend" control, so that continuing requires a deliberate act
    outside the player rather than a reflex click at 1 a.m.
44. As a viewer, I want a Window with time left but nothing that fits to be treated as
    Spent, so that I'm not shown an empty card with no explanation.

### Not interfering

45. As a viewer, I want Jellyfin's native Up Next overlay suppressed while a Watch Window
    is active, so that two overlays don't compete for the same corner of the screen.
46. As a viewer, I want Jellyfin's auto-advance suppressed while a Watch Window is active,
    so that the next episode cannot start before I have chosen.
47. As a viewer, I want both suppressions reversed when my Watch Window ends, so that
    normal Jellyfin behaviour returns without me fixing settings by hand.
48. As a viewer, I want suppression to leave my stored server-side playback preferences
    untouched, so that my native TV clients keep auto-playing episodes as they always did.
49. As a viewer, I want suppression restored even if the browser crashed mid-Window, so
    that a dirty shutdown cannot permanently disable my Up Next overlay.
50. As a viewer with no active Watch Window, I want Timewatch to do nothing whatsoever, so
    that installing it costs me no change in everyday behaviour.

### Configuration

51. As an admin, I want to set the default Watch Window duration, so that the common case
    is one tap.
52. As an admin, I want to set the Overshoot Tolerance, so that I decide how much overrun
    is acceptable in my household.
53. As an admin, I want to set how many Suggestions the End Card shows, so that it matches
    my screen and my appetite for choice.
54. As an admin, I want to choose which libraries Candidates are drawn from, so that home
    videos, music videos and clips never show up as Suggestions.
55. As an admin, I want a developer-mode toggle that serves the injected script
    uncached, so that iterating on the End Card is a page refresh.

### Installing and updating

56. As an admin, I want to install Timewatch from a plugin repository URL, so that I never
    hand-copy files onto my server.
57. As an admin, I want new builds to appear in the Jellyfin plugin catalogue
    automatically, so that updating is the normal plugin update flow.
58. As an admin, I want a clear, visible message when File Transformation is missing, so
    that I understand why the End Card never appears instead of debugging silence.
59. As an admin, I want the plugin to be inert rather than broken when it cannot inject, so
    that a missing dependency or a web-client change cannot take my server down.
60. As an admin, I want to know which clients the End Card cannot reach, so that I'm not
    confused when my Android TV keeps auto-playing episodes.

## Implementation Decisions

### Shape

A single server plugin, assembly and namespace `Jellyfin.Plugin.Timewatch`, targeting
`net10.0` against `Jellyfin.Controller` / `Jellyfin.Model` 12.4.0 with
`<ExcludeAssets>runtime</ExcludeAssets>`, `targetAbi` 12.4.0.0, and a freshly generated
plugin GUID. Layout is `src/` and `tests/`, MIT licensed, public GitHub repository.

Jellyfin plugins are effectively compatible with exactly one server version: the assembly
is rejected at load with a `NotSupported` status if it was compiled against a different
`Jellyfin.Controller`. v1 therefore targets 12.4 only and says so.

### Modules

- **SuggestionEngine** — the whole of the domain logic and the only designed seam. Takes a
  Watch Window, a clock, the plugin settings and a set of Candidates; returns an ordered
  set of Suggestions, or the Spent state. Owns Effective Runtime, Slack, Overshoot
  Tolerance, Fillable, the ranking tiers, one-per-series, in-tier randomisation, and
  Suggestion count. Has no reference to Jellyfin types.
- **CandidateSource** (port + Jellyfin adapter) — produces Candidates for a user. The
  adapter queries `ILibraryManager.GetItemList` with an `InternalItemsQuery` scoped to the
  user, `IncludeItemTypes` of Movie and Episode, `IsPlayed = false`, `IsVirtualItem =
  false`, `Recursive = true`, restricted to the configured libraries; reads resume
  positions in bulk via `IUserDataManager.GetResumeUserDataBatch` to avoid an N+1; and
  identifies the native next-up episode via `ITVSeriesManager.GetNextUp`. Runtime comes
  from `BaseItem.RunTimeTicks`.
- **WatchWindowStore** (port + file adapter) — at most one active Watch Window per user,
  persisted as JSON under `BasePlugin.DataFolderPath`, keyed by user id, with UTC end
  times. Not the plugin configuration XML: that is for admin settings edited in the
  dashboard, and rewriting it on every Window start would abuse it.
- **DismissService** — marks a single item Played via `IUserDataManager.SaveUserData` with
  an appropriate save reason. Scoped to one item; never a season or series.
- **API controllers** — thin adapters over the above. Authenticated endpoints for Window
  start / cancel / status, Suggestions, pick and dismiss; one **anonymous** endpoint
  serving the injected script, because a `<script src>` tag carries no auth token. Plugin
  controllers are discovered automatically because Jellyfin passes plugin assemblies to
  `AddJellyfinApi`; routes must be declared explicitly since the base route convention is
  `[controller]`.
- **Dashboard configuration page** via `IHasWebPages`, carrying exactly the five settings
  above and nothing else.
- **Standalone page**, served by the plugin, authenticated: start a Window, show Remaining
  Time, cancel. This is the only surface that starts a Window in v1.
- **Injection registration** — a startup-triggered `IScheduledTask` that registers two
  transformations with File Transformation by reflection.
- **Injected client script** — the End Card, the teaser, and suppression.

### Injection and the client seam

Jellyfin exposes no official way for a server plugin to alter the web client; the only
official UI surface is a dashboard page. Timewatch therefore depends on
**`jellyfin-plugin-file-transformation`** (≥ 3.0.3.0, itself version-locked to 12.4),
registering via reflection into its `PluginInterface.RegisterTransformation` — a direct
assembly reference does not work because plugins load into separate
`AssemblyLoadContext`s. Rolling our own response-intercepting middleware is technically
possible (an `IStartupFilter` registered from `IPluginServiceRegistrator`, which is exactly
how File Transformation itself works) but was rejected: two plugins both swapping
`Response.Body` fight, and being the plugin that breaks everyone else's injection is a
worse outcome than one extra install step.

Two transformations are registered:

1. `index.html` — append a `<script defer src="…/timewatch.js?v=…">` before `</body>`.
   External `src` rather than inline script, so a reverse proxy's CSP cannot block it.
2. `config.json` — append `Timewatch` to the client's `plugins` array.

This second transformation is the important decision. `jellyfin-web`'s `pluginManager`
loads a plugin named in `config.json` by looking the name up on `window`, calling it as a
factory, and constructing the returned class **with `playbackManager`, `Events`,
`inputManager`, `confirm` and `ServerConnections` as arguments**. So the injected script
sets `window.Timewatch = async () => class { constructor({ playbackManager, events, … }) }`
and receives real, unmangled references to the player machinery. The alternative —
string-surgery on the minified bundle to reach module internals — requires hardcoding
mangled identifiers per web release, which is why the comparable community plugin carries
a table of names like `p.Ay` and `r(46782)` and breaks on every update. We accept a
dependency on an undocumented-but-stable loader path instead.

Known-unverified assumptions, to be confirmed on the live server during the first install:
that a `window`-registered client plugin is constructed as expected; the execution ordering
between our deferred script and the client's plugin loading.

### Teaser, End Card and suppression

- The teaser fires on the same signal the native overlay uses: the player's time-update
  events, at roughly forty seconds remaining (the native trigger is 30/35/40 s depending on
  runtime, episodes only, suppressed in the final 20 s). The teaser is positioned and
  styled to match the native dialog and must not occlude the video frame.
- The End Card opens on explicit user action while media is still playing, and
  automatically on media end.
- Suppressing the native **overlay**: the client reads a per-user, client-local flag out of
  browser storage on every time-update tick, so clearing it takes effect immediately and
  restoring it needs no reload.
- Suppressing the native **auto-advance**: this is a separate switch, fired on the player's
  stopped event at media end — *not* on a timer — so it must be disabled *before* the item
  ends, not in reaction to playback stopping. It is suppressed in-session via the
  playbackManager instance handed to us, deliberately **not** by writing the user's
  server-side auto-play preference, which would also change behaviour on native clients the
  End Card cannot serve.
- Both suppressions are treated as a lease that must be released: restored when the Window
  ends or is cancelled, and reconciled on script load so a crash mid-Window cannot leave
  the native overlay permanently off.

### Client coverage

The End Card reaches clients that load the web client from the server: browsers, Jellyfin
Desktop, LG webOS, Jellyfin for Android (mobile) and iOS. It does **not** reach
Samsung/Tizen — which runs `jellyfin-web` but bundles its own copy inside the app, so
server-side injection never arrives — nor Android TV, Roku, Kodi or Swiftfin, which are
native. On those clients a Watch Window still exists and can be started from the standalone
page, but auto-advance is decided by the client and Timewatch cannot intervene. The README
must state this plainly.

### Distribution

CI builds with `jprm` on push to main, auto-incrementing the build number so every push
yields an installable version with no manual bump; the zip is attached to a GitHub Release
and `manifest.json` is committed and served over `raw.githubusercontent.com` — no Pages
setup, and it updates as soon as CI pushes. The admin adds that URL as a plugin repository.
Plugin manifests have no dependency field, so Jellyfin cannot install File Transformation
for us: its absence must be detected and reported in the plugin's own UI and logs rather
than failing silently.

Because the first install must come through the plugin manager, the injected script is
served with cache-busting and an admin developer-mode toggle for no-cache headers, so
iterating on client behaviour is a page refresh rather than a release cycle.

## Testing Decisions

A good test here exercises external behaviour: given a Watch Window, a clock, settings and
a set of Candidates, *what is offered and in what order*. Tests must not assert on
intermediate scoring, private helpers or the order in which Jellyfin is queried.

There is no prior art — this is a new repository with no existing tests and no ADRs.

**One seam: `SuggestionEngine`.** It is the only module with designed-in testability, and
everything interesting lives in it. xUnit tests drive it directly with a fake candidate
source, a fake window store and a fixed clock. Cases to cover:

- An item whose Effective Runtime exceeds Remaining Time beyond Overshoot Tolerance is not
  offered; one within tolerance is.
- A part-watched item is priced at remaining, not full, runtime.
- A Candidate is excluded when the remainder it would leave cannot be filled, and included
  when it can (one-step lookahead only).
- The native next-up episode ranks first when it fits, and is not special-cased into the
  list when it does not.
- Ranking tier order: in-progress items, then next-up for other series, then unwatched
  films and pilots.
- Least Slack wins within a tier; randomisation only reorders equals (seeded, so asserted
  deterministically).
- At most one Suggestion per series.
- Suggestion count honours configuration, and fewer are returned when fewer are Fillable.
- A Window past its end time is Spent; a Window with time left but no fitting Candidate is
  also Spent.
- After a Dismiss, the next-best Fillable Suggestion appears in the freed slot.

**Not tested automatically**, deliberately: the API controllers (thin translation to engine
calls), the File Transformation reflection call (it either registers or logs that the
dependency is missing), the injected JavaScript, and the teaser / End Card / suppression
behaviour. These need a running Jellyfin 12.4 and a real player, which this environment
does not have; they are verified by hand on the live server after the first install.

## Out of Scope

- Multi-item bundles — several alternative *combinations* that fill the Window. The End
  Card shows single items only; the standalone page gets bundles in a later version, once
  it's known whether the page is used mid-evening at all. (Single-item Suggestions on the
  start page were added after v1 shipped — see ticket 11. Bundles remain out of scope.)
- An injected "start a Watch Window" control in the player or home screen. v1 starts
  Windows only from the standalone page.
- Extending a Watch Window. There is no extend control anywhere, by design.
- Series-level muting ("not this show tonight"). Dismiss is per item.
- Native client support: Android TV, Roku, Kodi, Swiftfin, Samsung/Tizen. No End Card
  there, ever, by the nature of the injection mechanism.
- A custom home-screen section. Possible later by layering on the Home Screen Sections
  plugin, since the API would already exist.
- Rewatching: watched items are never Candidates, so a fully watched library yields
  nothing.
- Pausing a Watch Window, stacking Windows, or more than one active Window per user.
- Playlist creation. An earlier design created a playlist per pick to bridge from an
  external page into a client; inside the player it is pure overhead.

## Further Notes

The feature's whole value rests on one behavioural claim: that the moment *just before an
episode ends* is where overshoot is decided, and that intervening there — with the fitting
information attached — is what changes the outcome. Every design decision that looks
awkward follows from taking that seriously: the teaser must not cover still-playing video,
the auto-advance must be disabled before media end rather than reacted to afterwards, and
there is no extend button.

The injection mechanism is the standing risk. Jellyfin's web client is not a plugin
platform, a home-screen redesign landed in 2026, and the dependency chain is locked to one
server version. The mitigation is the client-plugin loader seam: it hands us real objects
instead of requiring us to recognise minified ones, so an ordinary `jellyfin-web` rebuild
should not break us. If it breaks anyway, the plugin must degrade to inert, never to a
broken client.
