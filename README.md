# Timewatch

A Jellyfin plugin for people who mean to stop watching at some point.

You tell Timewatch how long you have — say two hours — and it turns that into a **watch
window** with a real end time. Near the end of each episode, instead of Jellyfin's native
"Up Next" popup counting down to the next one, you get suggestions that **fit the time you
have left**, each labelled with what it would cost you:

> *Episode 4 — 43 min · leaves 17 min*

Pick one and it plays. Dismiss one you're never going to watch and it's marked watched,
gone from Next Up everywhere, and replaced by the next-best fit. When your time is up, the
card says so and offers nothing — there is deliberately no "extend" button.

A suggestion only appears if the time left *after* it can also be filled, so you're never
handed a choice that strands you with a useless forty-minute hole.

## Requirements

- **Jellyfin 12.4.** Plugins are compiled against one server version; this one targets 12.4
  and will show as *Not Supported* on anything else.
- **[File Transformation](https://github.com/IAmParadox27/jellyfin-plugin-file-transformation)**,
  installed separately. Jellyfin has no official way for a plugin to change the web client,
  and plugin manifests have no dependency field, so Jellyfin cannot install this for you.
  Without it Timewatch stays inert and says so in the log.

## Which clients show the end card

The end card works by injecting JavaScript into the web client, so it reaches clients that
load the web client **from your server**:

| Shows the end card | Does not |
| --- | --- |
| Browsers | Samsung / Tizen¹ |
| Jellyfin Desktop | Android TV |
| LG webOS | Roku |
| Jellyfin for Android (mobile) | Kodi |
| Jellyfin for iOS | Swiftfin |

¹ Tizen *does* run the web client, but bundles its own copy inside the app, so nothing the
server injects ever reaches it.

On the clients in the right-hand column a watch window still exists and can be started, but
Timewatch cannot intervene in playback — those clients decide auto-advance themselves.

## Installing

1. Dashboard → Plugins → Repositories → add:
   `https://raw.githubusercontent.com/klaas-be/Timewatch/main/manifest.json`
2. Install **Timewatch** from the catalogue, and **File Transformation** from
   [its own repository](https://github.com/IAmParadox27/jellyfin-plugin-file-transformation).
3. Restart Jellyfin.

## Development

```sh
dotnet test Timewatch.slnx          # the suggestion logic
dotnet build Timewatch.slnx         # compile
jprm plugin build . --version=x.y.z # package as an installable zip
```

Every push to `main` publishes an installable build: CI increments the build number, attaches
the zip to a GitHub release and updates `manifest.json`, so there is never a version string
to edit by hand.

See `GLOSSARY.md` for the domain vocabulary and `.scratch/timewatch-v1/spec.md` for the
specification this is being built to.

## Licence

MIT
