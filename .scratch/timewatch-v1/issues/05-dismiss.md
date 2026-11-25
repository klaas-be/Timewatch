# Dismiss: mark a single item played

Status: resolved
Type: task
Blocked by: 01

Spec: `.scratch/timewatch-v1/spec.md`. API facts file: `IUserDataManager.SaveUserData`,
`UserItemData.Played`, `UserDataSaveReason`.

Dismiss writes a real Played flag so the item leaves Next Up and Continue Watching on every
client. One item only — never a season or a series.

## Scope

- A port in `Domain/` ("mark this item watched for this user") plus the Jellyfin adapter.
- Must set the played flag for exactly the one item id passed.
- No confirmation, no Timewatch-side undo (Jellyfin's own UI can unmark).

## Acceptance

Thin adapter, no decisions. Behaviour that matters — that a dismissed Candidate is excluded
from the next Suggestion set and its slot refilled — is engine behaviour and belongs in
ticket 02's tests.

Covers user stories 36, 37, 38, 40.
