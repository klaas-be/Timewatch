/*
 * Timewatch's injected client script.
 *
 * Registers as a jellyfin-web CLIENT PLUGIN: our config.json transformation appends
 * "Timewatch" to the web client's `plugins` array, and pluginManager then awaits
 * `window.Timewatch`, calls the factory, and constructs the class with real, unmangled
 * references:
 *
 *     plugin = new PluginClass({ events, loading, appSettings, playbackManager, globalize,
 *                                appHost, appRouter, inputManager, toast, confirm,
 *                                dashboard, ServerConnections });
 *
 * Nothing here recognises a minified identifier or patches a bundle chunk; that approach
 * needs a per-release table of mangled names and breaks on every jellyfin-web rebuild.
 * If this seam ever stops handing us a playbackManager, the plugin must degrade to inert
 * — never to a broken client. Hence: every read off the client is guarded, and any
 * surprise ends the feature rather than throwing into jellyfin-web's own code paths.
 *
 * What it does, in order of how the evening goes:
 *   1. Polls `GET /Timewatch/Suggestions` (with playingItemId/playingSeriesId) to learn
 *      whether a Watch Window is active, what the Suggestions are, and whether the Window
 *      is Spent. A 404 means no Window at all — but for a client that WAS running one it
 *      means the time is up.
 *   2. While a Window is active it holds two LEASES over native behaviour: the Up Next
 *      overlay (per-user localStorage flag, re-read by jellyfin-web on every tick) and the
 *      auto-advance (`playbackManager._playNextAfterEnded`). Both are taken long before
 *      media end, because auto-advance fires on the player's `stopped` event and reacting
 *      to `playbackstop` is already too late. Both are restored on Window end or cancel,
 *      and reconciled on script load so a crash cannot leave the native overlay off.
 *   3. At the native moment (~30-40s remaining, episodes only) it shows a small teaser at
 *      the edge of the screen naming the top Suggestion, with "Start now" and "Choose…".
 *   4. At media end — or on "Choose…" — it opens the End Card: the Suggestions with their
 *      Effective Runtime and Slack, a pick that plays immediately, and a dismiss that
 *      writes watched state server-side and refills the slot.
 *   5. When the Window is Spent it says the time is up and offers nothing. There is no
 *      extend control, deliberately.
 *
 * Jellyfin JSON is PascalCase and case-sensitive; every property read here matches the
 * endpoint table in the API ticket exactly.
 *
 * The file is a classic script with no dependencies and no module syntax, so it can be
 * served as a plain <script src>. The small block at the bottom hands its pure decision
 * functions to a CommonJS loader when one is present, which is how the node tests under
 * tests/js reach them; browsers never take that branch.
 */
(function () {
    'use strict';

    var LOG_PREFIX = '[Timewatch]';

    /** How often the Suggestions endpoint is polled while a Watch Window is active. */
    var POLL_ACTIVE_MS = 15000;
    /** How often it is polled while we believe there is no Window. Cheap heartbeat. */
    var POLL_IDLE_MS = 30000;
    /** The playback ticker. One second is enough for a teaser at 30-40s remaining. */
    var TICK_MS = 1000;
    /** Nothing closer to the end than this counts as "the user seeked past the teaser". */
    var TEASER_FLOOR_SECONDS = 20;
    /** Below this much remaining when playback stops, the item is treated as finished. */
    var ENDED_WITHIN_SECONDS = 15;

    var OVERLAY_FLAG_SUFFIX = '-enableNextVideoInfoOverlay';
    /** Records the overlay value we took over, so a crashed session can be repaired. */
    var OVERLAY_LEASE_KEY = 'timewatch-overlay-lease';
    /** Survives a reload so that an expiry afterwards still shows the stopping signal. */
    var WINDOW_MARKER_KEY = 'timewatch-window-was-running';

    var PHASE_INERT = 'inert';
    var PHASE_ACTIVE = 'active';
    var PHASE_SPENT = 'spent';

    var TICKS_PER_SECOND = 10000000;

    // ---------------------------------------------------------------------------------
    // Pure decisions. Everything below the UI line is browser-only; these are not, and
    // they are the parts that would go wrong invisibly, so they are unit tested.
    // ---------------------------------------------------------------------------------

    /**
     * How far before the end the teaser appears, matching jellyfin-web's own Up Next
     * trigger: 30s remaining, 35s from a 40 minute runtime, 40s from 50 minutes, and
     * nothing at all under 10 minutes. Returns null when there should be no teaser.
     */
    function teaserLeadSeconds(runtimeSeconds) {
        var runtime = Number(runtimeSeconds);
        if (!isFinite(runtime) || runtime < 600) {
            return null;
        }

        if (runtime >= 3000) {
            return 40;
        }

        return runtime >= 2400 ? 35 : 30;
    }

    /**
     * True inside the teaser's lead. The floor mirrors jellyfin-web, which does not show
     * Up Next when the user seeks into the last seconds — there is no time to read it.
     * Once shown, the teaser is kept until the item ends; that is the caller's business.
     */
    function shouldShowTeaser(state) {
        var lead = teaserLeadSeconds(state && state.runtimeSeconds);
        if (lead === null) {
            return false;
        }

        var remaining = Number(state && state.remainingSeconds);
        if (!isFinite(remaining)) {
            return false;
        }

        return remaining > TEASER_FLOOR_SECONDS && remaining <= lead;
    }

    /**
     * The phase this client is in after a Suggestions response.
     *
     * A Watch Window whose end time has passed reads as absent server-side, so expiry and
     * "there never was one" are the same 404. The difference is whether this client was
     * running a Window: if it was, the 404 is the time being up and the Spent card is the
     * point of the whole feature. Anything else — a network blip, a 500, an expired token
     * — must change nothing, or a dropped request would fake the end of the evening.
     */
    function nextPhase(state) {
        var previous = (state && state.previous) || PHASE_INERT;
        var status = Number(state && state.status) || 0;

        if (status === 200) {
            return state && state.spent ? PHASE_SPENT : PHASE_ACTIVE;
        }

        if (status === 404) {
            return previous === PHASE_INERT ? PHASE_INERT : PHASE_SPENT;
        }

        return previous;
    }

    /** Minutes and hours the way a person says them. */
    function formatDuration(seconds) {
        var minutes = Math.max(0, Math.round((Number(seconds) || 0) / 60));
        var hours = Math.floor(minutes / 60);
        var rest = minutes % 60;

        if (hours > 0 && rest > 0) {
            return hours + ' h ' + rest + ' min';
        }

        if (hours > 0) {
            return hours + ' h';
        }

        return minutes + ' min';
    }

    /**
     * How a Suggestion fits: its Effective Runtime, and the Slack it leaves. Negative
     * Slack is an overshoot the engine allowed within the Overshoot Tolerance, and the End
     * Card has to say so rather than hide it.
     */
    function fitLabel(suggestion) {
        var runtime = formatDuration(suggestion && suggestion.effectiveRuntimeSeconds);
        var slack = Number(suggestion && suggestion.slackSeconds) || 0;

        if (Math.abs(slack) < 60) {
            return runtime + ' · fits exactly';
        }

        return slack > 0
            ? runtime + ' · leaves ' + formatDuration(slack)
            : runtime + ' · overruns by ' + formatDuration(-slack);
    }

    /**
     * Puts the native overlay flag back exactly as it was found, absence included: the
     * flag defaults to on when the key is missing, so writing "true" over a key that was
     * never there would be a change of its own.
     */
    function restoreOverlayFlag(storage, userId, recorded) {
        var key = String(userId) + OVERLAY_FLAG_SUFFIX;
        try {
            if (recorded === null || recorded === undefined) {
                storage.removeItem(key);
            } else {
                storage.setItem(key, String(recorded));
            }
        } catch (error) {
            // A storage that throws is a storage we cannot repair. Nothing else to do.
        }
    }

    // ---------------------------------------------------------------------------------
    // Guarded plumbing.
    // ---------------------------------------------------------------------------------

    function warn(message, detail) {
        try {
            console.warn(LOG_PREFIX + ' ' + message, detail === undefined ? '' : detail);
        } catch (error) {
            // A console that throws is not worth a second attempt.
        }
    }

    function debug(message, detail) {
        try {
            console.debug(LOG_PREFIX + ' ' + message, detail === undefined ? '' : detail);
        } catch (error) {
            // As above.
        }
    }

    /** localStorage, or null where it is blocked (private mode, embedded webviews). */
    function openStorage() {
        try {
            var storage = window.localStorage;
            var probe = 'timewatch-probe';
            storage.setItem(probe, '1');
            storage.removeItem(probe);
            return storage;
        } catch (error) {
            warn('localStorage is unavailable; the native overlay cannot be suppressed', error);
            return null;
        }
    }

    function readKey(storage, key) {
        try {
            return storage ? storage.getItem(key) : null;
        } catch (error) {
            return null;
        }
    }

    function writeKey(storage, key, value) {
        try {
            if (storage) {
                storage.setItem(key, value);
            }
        } catch (error) {
            // Ignored: a failed write degrades a feature, it does not break the client.
        }
    }

    function removeKey(storage, key) {
        try {
            if (storage) {
                storage.removeItem(key);
            }
        } catch (error) {
            // As above.
        }
    }

    function callOn(target, name, argument) {
        try {
            if (target && typeof target[name] === 'function') {
                return target[name](argument);
            }
        } catch (error) {
            return undefined;
        }

        return undefined;
    }

    function secondsFromTicks(ticks) {
        var value = Number(ticks);
        return isFinite(value) && value >= 0 ? value / TICKS_PER_SECOND : null;
    }

    // ---------------------------------------------------------------------------------
    // The two suppression leases.
    // ---------------------------------------------------------------------------------

    /**
     * Holds the native Up Next overlay and the native auto-advance off, as a lease rather
     * than a setting: whatever was there is recorded, and put back on release.
     *
     * The overlay flag is re-read by jellyfin-web on every timeupdate, so it is also
     * re-asserted on every one of our ticks — another tab, or the user's own settings
     * page, can write it back underneath us. `_playNextAfterEnded` is re-asserted for the
     * same reason: it is session state owned by playbackManager, and a new playback
     * session may reset it.
     */
    function createLeases(storage, playbackManager, userId) {
        var held = false;
        var recordedAutoAdvance;

        function overlayKey() {
            return String(userId) + OVERLAY_FLAG_SUFFIX;
        }

        function suppressOverlay() {
            if (readKey(storage, overlayKey()) !== 'false') {
                writeKey(storage, overlayKey(), 'false');
            }
        }

        function suppressAutoAdvance() {
            try {
                if (playbackManager) {
                    playbackManager._playNextAfterEnded = false;
                }
            } catch (error) {
                warn('could not suppress the native auto-advance', error);
            }
        }

        return {
            /**
             * Repairs a lease a crashed or closed session left behind. Called once on load,
             * before anything else: an abandoned lease would otherwise leave the user's
             * native Up Next overlay off for good, with nothing in the UI to explain it.
             */
            reconcileOnLoad: function () {
                var raw = readKey(storage, OVERLAY_LEASE_KEY);
                if (!raw) {
                    return;
                }

                removeKey(storage, OVERLAY_LEASE_KEY);

                var lease;
                try {
                    lease = JSON.parse(raw);
                } catch (error) {
                    lease = null;
                }

                if (!lease || lease.userId !== userId) {
                    // A lease belonging to another user on this browser is that session's
                    // to repair; its own script load will do it.
                    if (lease && lease.userId) {
                        writeKey(storage, OVERLAY_LEASE_KEY, raw);
                    }
                    return;
                }

                debug('restoring a suppression lease left behind by an earlier session');
                if (storage) {
                    restoreOverlayFlag(storage, userId, lease.previous);
                }
            },

            acquire: function () {
                if (held) {
                    return;
                }

                held = true;
                writeKey(storage, OVERLAY_LEASE_KEY, JSON.stringify({
                    userId: userId,
                    previous: readKey(storage, overlayKey()),
                    takenAt: new Date().toISOString()
                }));

                try {
                    recordedAutoAdvance = playbackManager
                        ? playbackManager._playNextAfterEnded
                        : undefined;
                } catch (error) {
                    recordedAutoAdvance = undefined;
                }

                debug('suppressing the native overlay and auto-advance for this Watch Window');
                suppressOverlay();
                suppressAutoAdvance();
            },

            /** Cheap, and runs on every tick: the client re-reads the flag that often. */
            reassert: function () {
                if (!held) {
                    return;
                }

                suppressOverlay();
                suppressAutoAdvance();
            },

            release: function () {
                if (!held) {
                    return;
                }

                held = false;
                var raw = readKey(storage, OVERLAY_LEASE_KEY);
                var previous = null;
                if (raw) {
                    try {
                        previous = JSON.parse(raw).previous;
                    } catch (error) {
                        previous = null;
                    }
                }

                if (storage) {
                    restoreOverlayFlag(storage, userId, previous);
                }
                removeKey(storage, OVERLAY_LEASE_KEY);

                try {
                    if (playbackManager) {
                        playbackManager._playNextAfterEnded =
                            recordedAutoAdvance === undefined ? true : recordedAutoAdvance;
                    }
                } catch (error) {
                    // Nothing to do; the next playback session resets it anyway.
                }

                debug('released the suppression leases; native behaviour is back');
            },

            isHeld: function () {
                return held;
            }
        };
    }

    // ---------------------------------------------------------------------------------
    // The server.
    // ---------------------------------------------------------------------------------

    /**
     * The two endpoints this script needs, as `{ status, data }` and never a rejection:
     * the phase machine wants the status code, and a 404 is information rather than an
     * error. Status 0 means the request did not reach a verdict.
     */
    function createApi(apiClient) {
        function statusOf(error) {
            if (!error) {
                return 0;
            }

            var status = Number(error.status);
            return isFinite(status) && status > 0 ? status : 0;
        }

        function viaApiClient(method, path, query, wantsJson) {
            return apiClient.ajax({
                type: method,
                url: apiClient.getUrl(path, query || {}),
                dataType: wantsJson ? 'json' : undefined
            }).then(function (data) {
                return { status: 200, data: wantsJson ? (data || null) : null };
            }, function (error) {
                return { status: statusOf(error), data: null };
            });
        }

        function viaFetch(method, path, query, wantsJson) {
            var url;
            try {
                url = apiClient.getUrl(path, query || {});
            } catch (error) {
                return Promise.resolve({ status: 0, data: null });
            }

            var headers = {};
            try {
                var token = callOn(apiClient, 'accessToken');
                if (token) {
                    headers['X-Emby-Token'] = token;
                }
            } catch (error) {
                // An unauthenticated request will simply come back 401.
            }

            return fetch(url, { method: method, headers: headers, credentials: 'same-origin' })
                .then(function (response) {
                    if (!response.ok) {
                        return { status: response.status, data: null };
                    }

                    if (!wantsJson) {
                        return { status: 200, data: null };
                    }

                    return response.json().then(function (data) {
                        return { status: 200, data: data || null };
                    }, function () {
                        return { status: 0, data: null };
                    });
                }, function () {
                    return { status: 0, data: null };
                });
        }

        function request(method, path, query, wantsJson) {
            try {
                if (apiClient && typeof apiClient.ajax === 'function' &&
                    typeof apiClient.getUrl === 'function') {
                    return viaApiClient(method, path, query, wantsJson);
                }

                if (apiClient && typeof apiClient.getUrl === 'function' &&
                    typeof fetch === 'function') {
                    return viaFetch(method, path, query, wantsJson);
                }
            } catch (error) {
                warn('a request could not be made', error);
            }

            return Promise.resolve({ status: 0, data: null });
        }

        return {
            /**
             * playingItemId matters: the item on screen is itself a part-watched Candidate
             * in the top ranking tier and holds its series' one-Suggestion slot, so
             * without it the teaser suggests the episode already playing.
             */
            suggestions: function (playing) {
                var query = {};
                if (playing && playing.id) {
                    query.playingItemId = playing.id;
                }
                if (playing && playing.seriesId) {
                    query.playingSeriesId = playing.seriesId;
                }

                return request('GET', 'Timewatch/Suggestions', query, true);
            },

            dismiss: function (itemId) {
                return request(
                    'POST',
                    'Timewatch/Suggestions/' + encodeURIComponent(itemId) + '/Dismiss',
                    {},
                    false);
            }
        };
    }

    // ---------------------------------------------------------------------------------
    // What is on screen.
    // ---------------------------------------------------------------------------------

    /**
     * A snapshot of the current playback, or null when nothing video-ish is playing.
     * Position is read in ticks where playbackManager offers them, because milliseconds
     * and seconds are indistinguishable from a single reading; whatever is read is then
     * sanity-checked against the runtime rather than trusted.
     */
    function readPlayback(playbackManager) {
        try {
            if (!playbackManager || typeof playbackManager.getCurrentPlayer !== 'function') {
                return null;
            }

            var player = playbackManager.getCurrentPlayer();
            if (!player) {
                return null;
            }

            var item = callOn(playbackManager, 'currentItem', player);
            if (!item || !item.Id) {
                return null;
            }

            var runtimeSeconds = secondsFromTicks(item.RunTimeTicks);
            var positionSeconds = readPosition(playbackManager, player, runtimeSeconds);

            return {
                id: item.Id,
                type: item.Type,
                seriesId: item.SeriesId,
                runtimeSeconds: runtimeSeconds,
                positionSeconds: positionSeconds,
                remainingSeconds: runtimeSeconds !== null && positionSeconds !== null
                    ? runtimeSeconds - positionSeconds
                    : null
            };
        } catch (error) {
            warn('could not read the current playback state', error);
            return null;
        }
    }

    function readPosition(playbackManager, player, runtimeSeconds) {
        function plausible(value) {
            return isFinite(value) && value >= 0 &&
                (runtimeSeconds === null || value <= runtimeSeconds + 60);
        }

        var ticks = Number(callOn(playbackManager, 'getCurrentTicks', player));
        if (isFinite(ticks)) {
            var fromTicks = ticks / TICKS_PER_SECOND;
            if (plausible(fromTicks)) {
                return fromTicks;
            }
        }

        var current = Number(callOn(playbackManager, 'currentTime', player));
        if (isFinite(current)) {
            if (plausible(current / 1000)) {
                return current / 1000;
            }
            if (plausible(current)) {
                return current;
            }
        }

        return null;
    }

    // ---------------------------------------------------------------------------------
    // UI. Self-contained: no jellyfin-web class names are relied on, because those are
    // the parts of the client that a redesign moves. Only the mount point is shared.
    // ---------------------------------------------------------------------------------

    var STYLE_ID = 'timewatch-styles';

    var STYLES = [
        '.timewatch-teaser{position:fixed;right:2.5vw;bottom:12vh;z-index:1000;max-width:22em;',
        'padding:0.9em 1.1em;border-radius:0.4em;background:rgba(20,20,20,0.92);color:#fff;',
        'box-shadow:0 0.2em 1em rgba(0,0,0,0.6);font-size:0.95em;line-height:1.35;',
        'font-family:inherit;pointer-events:auto;}',
        '.timewatch-teaser-label{opacity:0.7;font-size:0.82em;text-transform:uppercase;',
        'letter-spacing:0.08em;margin-bottom:0.3em;}',
        '.timewatch-teaser-title{font-weight:600;margin-bottom:0.15em;}',
        '.timewatch-teaser-fit{opacity:0.75;font-size:0.85em;margin-bottom:0.7em;}',
        '.timewatch-actions{display:flex;flex-wrap:wrap;gap:0.5em;}',
        '.timewatch-button{appearance:none;border:0;border-radius:0.25em;cursor:pointer;',
        'padding:0.5em 0.9em;font:inherit;font-size:0.9em;background:rgba(255,255,255,0.14);',
        'color:#fff;}',
        '.timewatch-button:hover{background:rgba(255,255,255,0.24);}',
        '.timewatch-button-primary{background:#00a4dc;color:#fff;}',
        '.timewatch-button-primary:hover{background:#0092c4;}',
        '.timewatch-overlay{position:fixed;inset:0;z-index:1001;display:flex;',
        'align-items:center;justify-content:center;background:rgba(0,0,0,0.86);color:#fff;',
        'font-family:inherit;padding:1.5em;box-sizing:border-box;}',
        '.timewatch-card{width:min(38em,100%);max-height:86vh;overflow-y:auto;',
        'background:rgba(28,28,28,0.98);border-radius:0.5em;padding:1.4em 1.5em;',
        'box-shadow:0 0.4em 2em rgba(0,0,0,0.7);}',
        '.timewatch-card h2{margin:0 0 0.2em;font-size:1.35em;font-weight:600;}',
        '.timewatch-card-note{opacity:0.75;margin:0 0 1em;font-size:0.95em;}',
        '.timewatch-list{list-style:none;margin:0 0 1em;padding:0;}',
        '.timewatch-item{display:flex;align-items:center;gap:0.8em;padding:0.7em 0;',
        'border-top:1px solid rgba(255,255,255,0.12);}',
        '.timewatch-item-text{flex:1 1 auto;min-width:0;}',
        '.timewatch-item-title{font-weight:600;overflow:hidden;text-overflow:ellipsis;',
        'white-space:nowrap;}',
        '.timewatch-item-fit{opacity:0.72;font-size:0.86em;}',
        '.timewatch-item-fit.timewatch-over{color:#ffb86b;}',
        '.timewatch-empty{opacity:0.8;margin:0 0 1em;}'
    ].join('');

    function ensureStyles() {
        try {
            if (document.getElementById(STYLE_ID)) {
                return;
            }

            var style = document.createElement('style');
            style.id = STYLE_ID;
            style.textContent = STYLES;
            document.head.appendChild(style);
        } catch (error) {
            warn('could not install styles', error);
        }
    }

    /**
     * Where an overlay goes. `.upNextContainer` is the native Up Next mount point and the
     * one stable hook worth using: it is inside the video view, so a teaser placed there
     * disappears with the player. Falling back to the body is harmless.
     */
    function mountPoint() {
        try {
            return document.querySelector('.upNextContainer') || document.body;
        } catch (error) {
            return document.body;
        }
    }

    function element(tag, className, text) {
        var node = document.createElement(tag);
        if (className) {
            node.className = className;
        }
        if (text !== undefined && text !== null) {
            node.textContent = String(text);
        }
        return node;
    }

    function button(label, className, onClick) {
        var node = element('button', 'timewatch-button' + (className ? ' ' + className : ''), label);
        node.type = 'button';
        node.addEventListener('click', function (event) {
            event.preventDefault();
            event.stopPropagation();
            try {
                onClick();
            } catch (error) {
                warn('an action failed', error);
            }
        });
        return node;
    }

    function suggestionLines(suggestion) {
        var title = suggestion.Title || 'Untitled';
        if (suggestion.SeriesName) {
            title = suggestion.SeriesName + ' — ' + title;
        }

        return {
            title: title,
            fit: fitLabel({
                effectiveRuntimeSeconds: suggestion.EffectiveRuntimeSeconds,
                slackSeconds: suggestion.SlackSeconds
            }),
            overruns: Number(suggestion.SlackSeconds) < 0
        };
    }

    /**
     * The teaser, the End Card and the Spent card. One at a time, torn down by whoever
     * puts up the next one, and all removable without trace so that an inert plugin
     * leaves no DOM behind.
     */
    function createUi(handlers) {
        var teaser = null;
        var overlay = null;
        var escapeListener = null;

        function removeTeaser() {
            if (teaser && teaser.parentNode) {
                teaser.parentNode.removeChild(teaser);
            }
            teaser = null;
        }

        function removeOverlay() {
            if (overlay && overlay.parentNode) {
                overlay.parentNode.removeChild(overlay);
            }
            overlay = null;

            if (escapeListener) {
                document.removeEventListener('keydown', escapeListener);
                escapeListener = null;
            }
        }

        function openOverlay(build) {
            removeOverlay();
            ensureStyles();

            overlay = element('div', 'timewatch-overlay');
            var card = element('div', 'timewatch-card');
            build(card);
            overlay.appendChild(card);
            mountPoint().appendChild(overlay);

            escapeListener = function (event) {
                if (event.key === 'Escape') {
                    handlers.onCloseOverlay();
                }
            };
            document.addEventListener('keydown', escapeListener);
        }

        return {
            /**
             * Small, at the edge, and never over the middle of the picture: the whole
             * premise is that the decision is made while the episode is still playing, so
             * covering the video would defeat it. Re-rendered in place if the Suggestion
             * changes, so an ignored teaser simply persists until the item ends.
             */
            showTeaser: function (suggestion) {
                ensureStyles();
                var lines = suggestionLines(suggestion);

                if (teaser && teaser.dataset.itemId === suggestion.ItemId) {
                    return;
                }

                removeTeaser();
                teaser = element('div', 'timewatch-teaser');
                teaser.dataset.itemId = suggestion.ItemId || '';
                teaser.appendChild(element('div', 'timewatch-teaser-label', 'Fits your time'));
                teaser.appendChild(element('div', 'timewatch-teaser-title', lines.title));
                teaser.appendChild(element('div', 'timewatch-teaser-fit', lines.fit));

                var actions = element('div', 'timewatch-actions');
                actions.appendChild(button('Start now', 'timewatch-button-primary', function () {
                    handlers.onPick(suggestion);
                }));
                actions.appendChild(button('Choose…', null, function () {
                    handlers.onChoose();
                }));
                teaser.appendChild(actions);

                mountPoint().appendChild(teaser);
            },

            hideTeaser: removeTeaser,

            /**
             * The End Card. Opened on "Choose…" while the item plays, and automatically at
             * media end — at which point nothing starts on its own, because the
             * auto-advance lease has been held since the Window began.
             */
            showEndCard: function (view) {
                openOverlay(function (card) {
                    card.appendChild(element('h2', null, 'What fits the time you have left'));
                    card.appendChild(element(
                        'p',
                        'timewatch-card-note',
                        formatDuration(view.remainingSeconds) + ' left in your Watch Window'));

                    if (!view.suggestions.length) {
                        card.appendChild(element(
                            'p',
                            'timewatch-empty',
                            'Nothing in your library fits the time that is left.'));
                    } else {
                        var list = element('ul', 'timewatch-list');
                        view.suggestions.forEach(function (suggestion) {
                            var lines = suggestionLines(suggestion);
                            var row = element('li', 'timewatch-item');
                            var text = element('div', 'timewatch-item-text');
                            text.appendChild(element('div', 'timewatch-item-title', lines.title));
                            text.appendChild(element(
                                'div',
                                'timewatch-item-fit' + (lines.overruns ? ' timewatch-over' : ''),
                                lines.fit));
                            row.appendChild(text);
                            row.appendChild(button('Play', 'timewatch-button-primary', function () {
                                handlers.onPick(suggestion);
                            }));
                            row.appendChild(button('Not tonight', null, function () {
                                handlers.onDismiss(suggestion);
                            }));
                            list.appendChild(row);
                        });
                        card.appendChild(list);
                    }

                    var actions = element('div', 'timewatch-actions');
                    actions.appendChild(button('Close', null, function () {
                        handlers.onCloseOverlay();
                    }));
                    card.appendChild(actions);
                });
            },

            /**
             * Spent: it says the time is up and offers nothing. No extend control — that
             * absence is the feature, not an omission.
             */
            showSpent: function () {
                openOverlay(function (card) {
                    card.appendChild(element('h2', null, 'Your time is up'));
                    card.appendChild(element(
                        'p',
                        'timewatch-card-note',
                        'The Watch Window has ended. Nothing more fits tonight.'));

                    var actions = element('div', 'timewatch-actions');
                    actions.appendChild(button('Good night', 'timewatch-button-primary', function () {
                        handlers.onAcknowledgeSpent();
                    }));
                    card.appendChild(actions);
                });
            },

            isOverlayOpen: function () {
                return overlay !== null;
            },

            hideOverlay: removeOverlay,

            destroy: function () {
                removeTeaser();
                removeOverlay();
            }
        };
    }

    // ---------------------------------------------------------------------------------
    // The controller: polling, phases, playback, and the handful of callbacks the UI has.
    // ---------------------------------------------------------------------------------

    function createController(context) {
        var playbackManager = context.playbackManager;
        var events = context.events;
        var apiClient = context.apiClient;
        var storage = context.storage;
        var userId = context.userId;

        var api = createApi(apiClient);
        var leases = createLeases(storage, playbackManager, userId);

        var phase = readKey(storage, WINDOW_MARKER_KEY) ? PHASE_ACTIVE : PHASE_INERT;
        var suggestions = [];
        var remainingSeconds = 0;
        var spentShown = false;
        var teaserShownForItem = null;
        var lastPlayback = null;
        var endCardForItem = null;
        var pollTimer = null;
        var tickTimer = null;
        var inFlight = false;
        var destroyed = false;

        // A marker-derived starting phase must not look like a fresh lease: reconcile
        // first, then let the first poll re-acquire if the Window really is still running.
        leases.reconcileOnLoad();

        var ui = createUi({
            onPick: function (suggestion) {
                play(suggestion);
            },
            onChoose: function () {
                openEndCard();
            },
            onDismiss: function (suggestion) {
                dismiss(suggestion);
            },
            onCloseOverlay: function () {
                ui.hideOverlay();
            },
            onAcknowledgeSpent: function () {
                ui.hideOverlay();
                removeKey(storage, WINDOW_MARKER_KEY);
            }
        });

        function serverId() {
            var id = callOn(apiClient, 'serverId');
            return id || undefined;
        }

        function play(suggestion) {
            if (!suggestion || !suggestion.ItemId) {
                return;
            }

            ui.hideTeaser();
            ui.hideOverlay();
            teaserShownForItem = null;

            try {
                var result = playbackManager.play({
                    ids: [suggestion.ItemId],
                    serverId: serverId()
                });
                if (result && typeof result.catch === 'function') {
                    result.catch(function (error) {
                        warn('playback of a picked Suggestion failed', error);
                    });
                }
            } catch (error) {
                warn('playback of a picked Suggestion failed', error);
            }
        }

        function dismiss(suggestion) {
            if (!suggestion || !suggestion.ItemId) {
                return;
            }

            api.dismiss(suggestion.ItemId).then(function (response) {
                if (response.status !== 200 && response.status !== 204) {
                    warn('dismissing a Suggestion failed', response.status);
                }

                // The slot refills from a fresh read: the dismissed item is no longer a
                // Candidate, so the engine puts something else in its place.
                return poll().then(function () {
                    if (ui.isOverlayOpen()) {
                        openEndCard();
                    }
                });
            }).catch(function (error) {
                warn('dismissing a Suggestion failed', error);
            });
        }

        function openEndCard() {
            if (phase === PHASE_SPENT) {
                ui.showSpent();
                return;
            }

            ui.hideTeaser();
            ui.showEndCard({ suggestions: suggestions, remainingSeconds: remainingSeconds });
        }

        function enterPhase(next) {
            if (next === phase) {
                return;
            }

            debug('phase ' + phase + ' -> ' + next);
            phase = next;

            if (next === PHASE_ACTIVE) {
                writeKey(storage, WINDOW_MARKER_KEY, '1');
                spentShown = false;
                leases.acquire();
                return;
            }

            leases.release();
            ui.hideTeaser();
            teaserShownForItem = null;
            endCardForItem = null;

            if (next === PHASE_SPENT) {
                // The marker stays until the user acknowledges, so that a reload during a
                // Spent evening still shows the stopping signal rather than nothing.
                if (!spentShown) {
                    spentShown = true;
                    ui.showSpent();
                }
                return;
            }

            suggestions = [];
            removeKey(storage, WINDOW_MARKER_KEY);
            ui.hideOverlay();
        }

        function poll() {
            if (destroyed || inFlight) {
                return Promise.resolve();
            }

            inFlight = true;
            var playing = lastPlayback;

            return api.suggestions(playing).then(function (response) {
                inFlight = false;
                var body = response.data || {};

                if (response.status === 200) {
                    suggestions = Array.isArray(body.Suggestions) ? body.Suggestions : [];
                    remainingSeconds = Number(body.RemainingSeconds) || 0;
                }

                enterPhase(nextPhase({
                    previous: phase,
                    status: response.status,
                    spent: body.Spent === true
                }));

                schedulePoll();
            }, function (error) {
                inFlight = false;
                warn('the Suggestions endpoint could not be read', error);
                schedulePoll();
            });
        }

        function schedulePoll() {
            if (destroyed) {
                return;
            }

            if (pollTimer) {
                clearTimeout(pollTimer);
            }

            pollTimer = setTimeout(poll, phase === PHASE_ACTIVE ? POLL_ACTIVE_MS : POLL_IDLE_MS);
        }

        /**
         * One ticker rather than a `timeupdate` subscription: the event's name and payload
         * on playbackManager (as opposed to on the player) is the part of this seam we
         * could not verify from source, whereas currentItem/getCurrentTicks are read
         * through the same guarded path everywhere else here.
         */
        function tick() {
            if (destroyed) {
                return;
            }

            try {
                if (phase === PHASE_ACTIVE) {
                    leases.reassert();
                }

                var now = readPlayback(playbackManager);
                var previous = lastPlayback;
                lastPlayback = now;

                if (!now) {
                    if (previous) {
                        onPlaybackGone(previous);
                    }
                    return;
                }

                if (previous && previous.id !== now.id) {
                    // A new item started: last item's teaser and card are stale.
                    ui.hideTeaser();
                    ui.hideOverlay();
                    teaserShownForItem = null;
                    endCardForItem = null;
                    poll();
                }

                if (phase !== PHASE_ACTIVE) {
                    return;
                }

                maybeShowTeaser(now);
            } catch (error) {
                warn('the playback tick failed', error);
            }
        }

        function maybeShowTeaser(playback) {
            if (playback.type !== 'Episode') {
                return;
            }

            if (teaserShownForItem === playback.id) {
                // Ignored teasers persist to the end of the item, so nothing to do.
                return;
            }

            if (!shouldShowTeaser(playback) || ui.isOverlayOpen()) {
                return;
            }

            var top = suggestions.length ? suggestions[0] : null;
            if (!top) {
                // No Suggestion fits: say nothing rather than offer nothing.
                return;
            }

            teaserShownForItem = playback.id;
            ui.showTeaser(top);
        }

        /**
         * Playback disappeared. If the item was within seconds of its end it finished, and
         * the End Card is the point of the feature; if the user stopped it halfway, an
         * overlay would be an intrusion, so only the teaser goes away.
         */
        function onPlaybackGone(previous) {
            ui.hideTeaser();
            teaserShownForItem = null;

            if (phase !== PHASE_ACTIVE) {
                return;
            }

            var remaining = previous.remainingSeconds;
            var finished = remaining !== null && remaining <= ENDED_WITHIN_SECONDS;
            if (!finished || endCardForItem === previous.id) {
                return;
            }

            endCardForItem = previous.id;
            openEndCard();
            // The card is open with what the last poll knew; refresh it behind the scenes
            // so that Effective Runtime and Slack reflect the time this item just spent.
            poll().then(function () {
                if (ui.isOverlayOpen() && phase === PHASE_ACTIVE) {
                    openEndCard();
                }
            });
        }

        function subscribe() {
            if (!events || typeof events.on !== 'function') {
                return;
            }

            try {
                events.on(playbackManager, 'playbackstart', function () {
                    if (phase === PHASE_ACTIVE) {
                        leases.reassert();
                    }
                    ui.hideOverlay();
                    poll();
                });

                // Reacting here is too late to stop the auto-advance — that is what the
                // lease is for — but it is the earliest notice that an item is over.
                events.on(playbackManager, 'playbackstop', function () {
                    var previous = lastPlayback;
                    lastPlayback = null;
                    if (previous) {
                        onPlaybackGone(previous);
                    }
                });
            } catch (error) {
                warn('could not subscribe to playback events; falling back to polling', error);
            }
        }

        return {
            start: function () {
                subscribe();
                tickTimer = setInterval(tick, TICK_MS);
                poll();
            },

            destroy: function () {
                destroyed = true;
                if (pollTimer) {
                    clearTimeout(pollTimer);
                }
                if (tickTimer) {
                    clearInterval(tickTimer);
                }
                leases.release();
                ui.destroy();
            }
        };
    }

    // ---------------------------------------------------------------------------------
    // Registration.
    // ---------------------------------------------------------------------------------

    function resolveApiClient(serverConnections) {
        var client = callOn(serverConnections, 'currentApiClient');
        if (client) {
            return client;
        }

        try {
            return window.ApiClient || null;
        } catch (error) {
            return null;
        }
    }

    function resolveUserId(apiClient) {
        var id = callOn(apiClient, 'getCurrentUserId');
        return id ? String(id) : null;
    }

    function registerClientPlugin() {
        var constructed = false;

        /**
         * The factory jellyfin-web's pluginManager awaits. It must resolve to the class,
         * not an instance: `const PluginClass = await pluginDefinition();`
         */
        window.Timewatch = async function () {
            return class TimewatchClientPlugin {
                constructor(dependencies) {
                    var options = dependencies || {};
                    var playbackManager = options.playbackManager;
                    var events = options.events;
                    var inputManager = options.inputManager;
                    var confirm = options.confirm;
                    var ServerConnections = options.ServerConnections;

                    // pluginManager reads these off the instance; a missing one is a
                    // console error in the client's own code, not ours.
                    this.name = 'Timewatch';
                    this.id = 'timewatch';
                    this.type = 'timewatch';

                    constructed = true;

                    try {
                        if (!playbackManager || typeof playbackManager.play !== 'function') {
                            warn('no usable playbackManager was handed to the client plugin; '
                                + 'staying inert so that native playback is untouched');
                            return;
                        }

                        var apiClient = resolveApiClient(ServerConnections);
                        var userId = resolveUserId(apiClient);
                        if (!apiClient || !userId) {
                            warn('no signed-in ApiClient; staying inert until the next load');
                            return;
                        }

                        var controller = createController({
                            playbackManager: playbackManager,
                            events: events,
                            inputManager: inputManager,
                            confirm: confirm,
                            apiClient: apiClient,
                            storage: openStorage(),
                            userId: userId
                        });

                        this.destroy = function () {
                            controller.destroy();
                        };

                        controller.start();
                        debug('client plugin constructed; watching for a Watch Window');
                    } catch (error) {
                        // Degrade to inert, never to a broken client.
                        warn('the client plugin failed to start and will stay inert', error);
                    }
                }
            };
        };

        // If the constructor never fires, the client-plugin seam is not what we think it
        // is. That has to be reported rather than worked around: the alternative —
        // patching minified chunks — is exactly what this design exists to avoid.
        setTimeout(function () {
            if (!constructed) {
                warn('window.Timewatch was never constructed by jellyfin-web. The client '
                    + 'plugin seam did not fire: check that /web/config.json lists '
                    + '"Timewatch" in its plugins array and that this script runs before '
                    + 'loadPlugins(). Timewatch is inert.');
            }
        }, 20000);

        debug('injected script loaded; window.Timewatch registered');
    }

    if (typeof window !== 'undefined' && window.document) {
        registerClientPlugin();
    }

    // Browsers never see this: `module` is undefined there. It is how the node tests under
    // tests/js reach the pure decisions above without a DOM or a Jellyfin.
    if (typeof module !== 'undefined' && module.exports) {
        module.exports = {
            teaserLeadSeconds: teaserLeadSeconds,
            shouldShowTeaser: shouldShowTeaser,
            nextPhase: nextPhase,
            formatDuration: formatDuration,
            fitLabel: fitLabel,
            restoreOverlayFlag: restoreOverlayFlag
        };
    }
})();
