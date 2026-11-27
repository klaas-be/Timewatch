# ADR 0001: Spread Suggestions across Length Bands

Date: 2026-11-27
Status: accepted

## Context

Suggestions are ranked by tier, then by least Slack by magnitude. That ordering answers
"which single item wastes least of my evening" correctly, and v1 used it to fill every
Suggestion slot. In a library whose Candidates are mostly feature films, a fresh two-hour
Watch Window therefore offers four films of nearly two hours each: the list is four ways to
spend the entire evening and offers no choice about *how much* of it to spend. Reported from
the start page, where a Window is at its longest, but it is a property of the engine and so
of the End Card too.

Least Slack is still the right answer for the *first* Suggestion. The defect is in using it
for all of them: the slots after the first are where a real choice lives, and spending them
on the second- and third-best fit buys almost nothing, because those differ from the first
by minutes.

## Decision

Keep the ranking exactly as it is and change only which Candidates occupy the slots.

Cut the Remaining Time into four equal **Length Bands** and give each slot after the first
to the best-ranked Candidate from a band that has no Suggestion yet; once every band
present in the list is represented, fall back to the next Candidate by rank. The answer is
then returned in ranking order, so the least-wasteful Suggestion is still the one read
first.

Bands are slices of the Remaining Time rather than fixed minute counts, because "short" is
relative to the evening: a 45 minute episode is a long option in an hour's Window and a
quick one in three hours'. Four bands, because that is the default Suggestion count — the
ideal End Card is one Candidate per band.

## Consequences

- A long Window reliably shows a long option, a middling one and something quick, so the
  user chooses how much of the evening to commit, not only to what.
- The second-best fit can be displaced by a much shorter item. This is the point, and it is
  bounded: Fit, Overshoot Tolerance, Fillable, the ranking tiers and one-per-series all
  still decide *whether* a Candidate is offerable, and the spread only chooses among those.
- Slack on later Suggestions is larger by design. Each Suggestion is still labelled with
  the Slack it leaves, and Fillable still guarantees that Slack can be filled.
- Ranking order is unobservably different when there are no more Candidates than slots:
  all of them are offered either way.
- Rejected: scoring Slack non-linearly (a soft preference for variety). It would have made
  the ranking itself untestable by inspection, and the complaint is about the composition of
  the set, which is a selection question, not a scoring one.
