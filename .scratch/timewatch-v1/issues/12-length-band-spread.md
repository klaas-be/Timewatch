# Spread Suggestions across Length Bands

Status: resolved
Type: task
Blocked by: 02

Reported after ticket 11 shipped: "The start page always prioritises large videos as
suggestions that fill the watch time almost completely. I want to have some smaller videos
in the mix as well."

Ranking is tier, then least Slack by magnitude, so in a library of feature films a fresh
two-hour Window answers with four films of nearly two hours. Every Suggestion commits the
whole evening; the list is four ways to spend all of it and no choice about how much of it
to spend. The same applies to the End Card, which shares the engine — it is just less
visible there, because a remainder mid-evening is usually small enough that few Candidates
fit it at all.

## Answer

The engine now spreads the Suggestion slots across Length Bands: equal slices of the
Remaining Time, four of them, matching the default Suggestion count. See
`docs/adr/0001-spread-suggestions-across-length-bands.md` and the Length Band entry in
`GLOSSARY.md`.

- Selection only. The best-ranked Candidate still takes the first slot, and the answer is
  still returned in ranking order, so the least-wasteful Suggestion is read first.
- Each later slot prefers the best-ranked Candidate from a band that has no Suggestion yet,
  falling back to the next by rank once every band present in the list is represented.
- Bands are slices of the Remaining Time, not fixed minute counts, so "short" means short
  *for this evening*: a 45 minute episode is a long option in an hour's Window and a quick
  one in three hours'.
- Fit, Fillable, the tiers and one-per-series are untouched — the spread chooses among
  Candidates that were already offerable.
- Nothing changes when fewer Candidates are offerable than there are slots: all of them are
  still offered.

Both surfaces get it, since both call the same engine. Two engine tests cover it; the
existing 202 pass unchanged, which is the evidence that ranking order itself did not move.
