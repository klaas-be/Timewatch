# Glossary

## Watch Window

A per-user, deadline-anchored span of time the user wants to fill with watching. Created
by stating a duration ("2 hours"); stored as an absolute end time, not as a countdown of
runtime watched. A Watch Window exists for exactly one User and expires when its end time
passes.

Because the anchor is wall-clock, anything that consumes real time — pauses, breaks,
a phone call — shrinks the Remaining Time without the system having to observe it.

## Remaining Time

The Watch Window's end time minus now. The quantity every Suggestion must fit. Recomputed
at each request; never stored.

## Candidate

A library item eligible to appear in a Suggestion for a given User: a Movie or Episode
that is unwatched or partially watched, and visible to that User. A fully watched item is
not a Candidate.

## Effective Runtime

The time a Candidate would actually consume for this User: full runtime for an unwatched
item, runtime minus playback position for a partially watched one. Packing uses Effective
Runtime, never raw runtime.

## Overshoot Tolerance

The amount by which a Suggestion may exceed Remaining Time before it is rejected.
Configurable; non-zero by default, because finishing a few minutes late beats leaving a
large gap of Slack.

## Slack

Remaining Time minus the total Effective Runtime of a Suggestion. Positive Slack is dead
air; negative Slack is overshoot, permitted only within Overshoot Tolerance.

## End Card

The overlay shown in the player as an item finishes, listing Suggestions that fit the
Remaining Time. It appears only while a Watch Window is active, and while it is shown the
client's native up-next auto-advance is cancelled, so no item starts without being chosen.

## Suggestion

A single Candidate offered to the User, labelled with how it fits: its Effective Runtime
and the Slack it would leave. A Suggestion is one item, not a combination — combinations
are only shown outside the End Card, where there is room to read them.

## Fillable

A Candidate is Fillable when the time left after watching it can itself be filled: at
least one other Candidate fits that remainder within Overshoot Tolerance, or the remainder
is small enough to count as spent. Only Fillable Candidates become Suggestions, which is
what stops the User being stranded with an unusable gap.

## Spent

The state of a Watch Window whose Remaining Time can no longer be filled — either the end
time has passed, or no Candidate fits. A Spent Window shows the User that their time is up
and offers nothing. It cannot be extended; that is the point of it.

## Cancel

The User's act of ending their own Watch Window early. A cancelled Window is over at once
and never becomes active again; the User is back to normal Jellyfin behaviour, with both
native suppressions released. Cancel is not Spent: Spent is the evening running out and is
worth telling the User about, whereas a Cancel is what the User just asked for and is
therefore silent. Both read as "no Watch Window", so which of the two happened is
remembered for the client to ask.

## Dismiss

The User's act of removing a Suggestion they no longer intend to watch. Dismissing marks
the item Played in Jellyfin — it is a real watched-state change, visible everywhere in the
server, not a Timewatch-local hint — so the item stops being a Candidate for good.
