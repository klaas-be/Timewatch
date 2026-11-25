# Suggestions on the start page

Status: resolved
Type: task
Blocked by: 07

Asked for after v1 shipped: "sometimes I just don't know where to start, but want to watch
something for 2 hours". v1 could only answer that at the *end* of an episode, which is no
help when starting an evening from nothing.

Ticket 07 deliberately excluded a suggestions list from the start page. This reverses that
decision for single-item Suggestions only; multi-item bundles remain out of scope.

## Answer

The start page now lists the same Suggestions the End Card shows, once a Watch Window is
running. No new server work: `GET /Timewatch/Suggestions` already answers without a playing
item, and with nothing playing the engine's ranking reads as "finish what you started, then
the next episode of a series you are following, then something new".

- Each Suggestion shows how long it runs and what it leaves ("43 min · leaves 17 min",
  "fills it", "5 min over").
- The title is a link into the web client's item page, because this page runs outside
  jellyfin-web and has no `playbackManager` to start playback with. One extra tap.
- "Not tonight" dismisses, through the same endpoint as the End Card, so it marks the item
  watched everywhere and the list refills.
- "Show me something else" re-asks; the seed is derived from the instant at millisecond
  resolution, so equally-fitting Candidates shuffle between calls.
- A Spent Window says "Nothing fits the time left" and offers no reroll.

Suggestions appear only while a Window is active: you commit to the two hours, then see what
fits it. A 404 hides the list rather than raising an error, since the status call is what
reports an absent Window.
