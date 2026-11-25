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

## When the end card does not appear

Work down this list; each step tells you something the next one cannot.

**1. Is a watch window running?** Timewatch is deliberately inert without one, and inert
means Jellyfin behaves exactly as it did before. Open `/Timewatch/Start` — you should see a
countdown, not the duration picker.

**2. Did the server register the injection?**

```sh
journalctl -u jellyfin --since today | grep -i timewatch
```

"registered its web client injection" is what you want. "needs the File Transformation
plugin" means that dependency is missing — install it and restart.

**3. Is the browser using a stale `config.json`?** This is the most likely cause, and it
looks exactly like a broken plugin. Jellyfin sets `no-cache` only on `index.html`, so a
browser can reuse a months-old `config.json` — the one file that tells the web client to
load Timewatch at all. The script loads, and nothing happens.

Compare what your browser sees with what the server sends:

```js
// in the web client's console
fetch('/web/config.json').then(r => r.json()).then(c => console.log(c.plugins))
```

```sh
curl -s http://YOUR-SERVER:8096/web/config.json | grep Timewatch
```

If the server lists `Timewatch` and the browser does not: DevTools → Network → tick
**Disable cache** → reload. (Ctrl+Shift+R alone often will not do it for `fetch()`-ed
files.) Or DevTools → Application → **Clear site data**.

**4. What does the console say?** Set the log level to **Verbose** — Timewatch's startup
line is `console.debug`. You want `Loading plugin (via window): Timewatch` from jellyfin-web,
then `[Timewatch] client plugin started`. Any `[Timewatch]` warning says what is wrong.

**5. Is the suppression lease held?** While a window runs:

```js
localStorage.getItem(ApiClient.getCurrentUserId() + '-enableNextVideoInfoOverlay')  // "false"
```

`null` or `"true"` means Timewatch is not holding it, and Jellyfin's own Up Next dialog
will keep appearing.

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
