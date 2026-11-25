# File Transformation registration

Status: resolved
Type: task
Blocked by: —

Spec: `.scratch/timewatch-v1/spec.md`. **The API facts file contains the exact reflection
payload, the callback contract and the `JObject` flavour — use it verbatim rather than
guessing.**

Register two transformations with the File Transformation plugin at server start, from an
`IScheduledTask` with a startup trigger.

## Scope

1. `index.html` — insert `<script defer src="<BaseUrl>/Timewatch/timewatch.js?v=...">`
   before `</body>`. External src, never inline (a user's reverse proxy may set a CSP).
   The path prefix must come from `NetworkConfiguration.BaseUrl`.
2. `config.json` — append `Timewatch` to the client's `plugins` array, which is what makes
   the client construct our class with `playbackManager` (see ticket 09).
- Reflection only: a direct assembly reference does not work across AssemblyLoadContexts.
- If File Transformation is absent, log a clear, actionable message and stay inert. Never
  throw from the task — a throw marks the plugin Malfunctioned.
- Both callbacks are `public static`, take one payload object with a `Contents` string, and
  return the whole new file contents as a `string`.

## Acceptance

- The string-patching callbacks are pure functions of their input, so they ARE unit
  testable without Jellyfin: test that the script tag lands before `</body>`, that
  `config.json` gains exactly one entry, and that both are idempotent (patching
  already-patched content must not duplicate).
- Missing-dependency path tested or at minimum provably non-throwing.

Covers user stories 58, 59.
