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
