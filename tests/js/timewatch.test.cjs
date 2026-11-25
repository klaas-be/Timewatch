/*
 * The injected script is browser code and most of it cannot be exercised here: there is no
 * DOM, no playbackManager and no Jellyfin. Its decisions can be, though, and they are the
 * parts that would fail silently on a live server — the teaser moment, what a 404 from the
 * Suggestions endpoint means, and how a Suggestion's fit is worded.
 *
 * The script stays a classic browser script; it only hands these pure functions to a
 * CommonJS loader when one is present, which is how this file reaches them.
 */
'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');

const timewatch = require('../../src/Jellyfin.Plugin.Timewatch/Injection/timewatch.js');

test('the teaser fires at the native moment, per the native runtime bands', () => {
    // jellyfin-web: 30s remaining, 35s when the runtime is >= 40 min, 40s when >= 50 min.
    assert.equal(timewatch.teaserLeadSeconds(20 * 60), 30);
    assert.equal(timewatch.teaserLeadSeconds(40 * 60), 35);
    assert.equal(timewatch.teaserLeadSeconds(50 * 60), 40);
    assert.equal(timewatch.teaserLeadSeconds(90 * 60), 40);
});

test('short items get no teaser at all, as natively', () => {
    assert.equal(timewatch.teaserLeadSeconds(9 * 60), null);
    assert.equal(timewatch.teaserLeadSeconds(0), null);
    assert.equal(timewatch.teaserLeadSeconds(undefined), null);
});

test('the teaser shows inside its lead but not in the last seconds of a seek', () => {
    assert.equal(timewatch.shouldShowTeaser({ remainingSeconds: 38, runtimeSeconds: 55 * 60 }), true);
    assert.equal(timewatch.shouldShowTeaser({ remainingSeconds: 41, runtimeSeconds: 55 * 60 }), false);
    assert.equal(timewatch.shouldShowTeaser({ remainingSeconds: 12, runtimeSeconds: 55 * 60 }), false);
    assert.equal(timewatch.shouldShowTeaser({ remainingSeconds: 25, runtimeSeconds: 20 * 60 }), true);
    // A runtime too short for the native overlay gets no teaser at any point.
    assert.equal(timewatch.shouldShowTeaser({ remainingSeconds: 25, runtimeSeconds: 8 * 60 }), false);
});

test('a Watch Window that answers is active, or Spent when it says so', () => {
    assert.equal(timewatch.nextPhase({ previous: 'inert', status: 200, spent: false }), 'active');
    assert.equal(timewatch.nextPhase({ previous: 'active', status: 200, spent: true }), 'spent');
});

test('a 404 is the time being up for a client that was running a Window', () => {
    assert.equal(timewatch.nextPhase({ previous: 'active', status: 404 }), 'spent');
    assert.equal(timewatch.nextPhase({ previous: 'spent', status: 404 }), 'spent');
});

test('a 404 is simply no Watch Window for a client that never had one', () => {
    assert.equal(timewatch.nextPhase({ previous: 'inert', status: 404 }), 'inert');
});

test('an unreachable server changes nothing, so a blip cannot fake the time being up', () => {
    assert.equal(timewatch.nextPhase({ previous: 'active', status: 0 }), 'active');
    assert.equal(timewatch.nextPhase({ previous: 'active', status: 500 }), 'active');
    assert.equal(timewatch.nextPhase({ previous: 'inert', status: 401 }), 'inert');
});

test('fit is worded as Effective Runtime plus what it leaves or overruns', () => {
    assert.equal(
        timewatch.fitLabel({ effectiveRuntimeSeconds: 2580, slackSeconds: 1020 }),
        '43 min · leaves 17 min');
    assert.equal(
        timewatch.fitLabel({ effectiveRuntimeSeconds: 5400, slackSeconds: -600 }),
        '1 h 30 min · overruns by 10 min');
    assert.equal(
        timewatch.fitLabel({ effectiveRuntimeSeconds: 1800, slackSeconds: 20 }),
        '30 min · fits exactly');
});

test('durations read the way a person would say them', () => {
    assert.equal(timewatch.formatDuration(0), '0 min');
    assert.equal(timewatch.formatDuration(45), '1 min');
    assert.equal(timewatch.formatDuration(3600), '1 h');
    assert.equal(timewatch.formatDuration(3660), '1 h 1 min');
});

test('the overlay lease restores exactly what it recorded, including absence', () => {
    const store = new Map([['u1-enableNextVideoInfoOverlay', 'false']]);
    const fake = {
        getItem: (k) => (store.has(k) ? store.get(k) : null),
        setItem: (k, v) => store.set(k, String(v)),
        removeItem: (k) => store.delete(k)
    };

    // A lease taken over a key that was absent must leave it absent again.
    timewatch.restoreOverlayFlag(fake, 'u1', null);
    assert.equal(store.has('u1-enableNextVideoInfoOverlay'), false);

    timewatch.restoreOverlayFlag(fake, 'u1', 'true');
    assert.equal(store.get('u1-enableNextVideoInfoOverlay'), 'true');
});

/*
 * Everything below drives the controller itself, with fakes for all it reads: localStorage,
 * the ApiClient, playbackManager, and — where a test needs it — a document stub. The
 * reload, refill, teaser and polling behaviours live behind that seam and none of them is
 * reachable from the pure decisions alone.
 */

function fakeStorage(initial) {
    const store = new Map(Object.entries(initial || {}));
    return {
        getItem: (k) => (store.has(k) ? store.get(k) : null),
        setItem: (k, v) => store.set(k, String(v)),
        removeItem: (k) => store.delete(k),
        has: (k) => store.has(k)
    };
}

/**
 * An ApiClient whose requests hang until a test answers them, which is what lets a test
 * say "while a request is in flight…" at all. `calls` is every request made, in order.
 */
function fakeClient() {
    const calls = [];
    return {
        calls: calls,
        getUrl: (path, query) => path + '?' + new URLSearchParams(query || {}).toString(),
        ajax: (options) => new Promise((resolve, reject) => {
            calls.push({ url: options.url, type: options.type, resolve: resolve, reject: reject });
        }),
        serverId: () => 's1',
        getCurrentUserId: () => 'u1'
    };
}

/** A playbackManager with one episode on screen, that far from its end. */
function playingEpisode(state) {
    const player = { name: 'fake' };
    return {
        play: () => {},
        getCurrentPlayer: () => player,
        currentItem: () => ({
            Id: state.id,
            Type: 'Episode',
            SeriesId: 'series-1',
            RunTimeTicks: state.runtimeSeconds * 10000000
        }),
        getCurrentTicks: () => (state.runtimeSeconds - state.remainingSeconds) * 10000000,
        paused: () => false
    };
}

/** Enough of a document for the overlays; every node is a plain object. */
function fakeDocument() {
    function create(tag) {
        const node = {
            tagName: tag,
            className: '',
            textContent: '',
            dataset: {},
            children: [],
            parentNode: null,
            handlers: {},
            appendChild: (child) => {
                child.parentNode = node;
                node.children.push(child);
                return child;
            },
            removeChild: (child) => {
                node.children = node.children.filter((other) => other !== child);
                child.parentNode = null;
                return child;
            },
            addEventListener: (name, handler) => {
                node.handlers[name] = (node.handlers[name] || []).concat(handler);
            },
            removeEventListener: () => {}
        };
        return node;
    }

    return {
        body: create('body'),
        head: create('head'),
        createElement: create,
        getElementById: () => null,
        querySelector: () => null,
        addEventListener: () => {},
        removeEventListener: () => {}
    };
}

function textOf(node) {
    return [node.textContent || '']
        .concat(node.children.map(textOf))
        .join(' ')
        .replace(/\s+/g, ' ')
        .trim();
}

function teaserText(document) {
    const teaser = document.body.children.find((node) => node.className === 'timewatch-teaser');
    return teaser ? textOf(teaser) : null;
}

function suggestion(name) {
    return {
        ItemId: 'item-' + name,
        Title: name,
        SeriesName: 'Severance',
        EffectiveRuntimeSeconds: 2580,
        SlackSeconds: 1020
    };
}

/** Lets every already-resolved promise run its handlers. */
function settled() {
    return new Promise((resolve) => setImmediate(resolve));
}

test('a reload during an active Watch Window takes both suppression leases again', () => {
    // The marker is what a reload leaves behind: this client was running a Window when the
    // page went away. Without the leases the native overlay is back on and Jellyfin
    // auto-advances at media end — the exact behaviour the plugin exists to prevent.
    const storage = fakeStorage({
        'timewatch-window-was-running': '1',
        'u1-enableNextVideoInfoOverlay': 'true'
    });
    const playbackManager = {
        play: () => {},
        getCurrentPlayer: () => null,
        _playNextAfterEnded: true
    };

    const controller = timewatch.createController({
        playbackManager: playbackManager,
        events: null,
        apiClient: null,
        storage: storage,
        userId: 'u1'
    });

    assert.equal(storage.getItem('u1-enableNextVideoInfoOverlay'), 'false');
    assert.equal(playbackManager._playNextAfterEnded, false);

    // And it is still a lease: what it took over comes back on release.
    controller.destroy();
    assert.equal(storage.getItem('u1-enableNextVideoInfoOverlay'), 'true');
    assert.equal(playbackManager._playNextAfterEnded, true);
});

test('a refresh resolves with fresh data, never with what the last poll knew', async () => {
    // Awaiting a refresh is how the End Card refills the slot it just freed. A refresh that
    // resolves immediately because another request is in flight hands the caller the
    // suggestions from before the dismiss — the item dismissed is still on the card.
    const client = fakeClient();
    const controller = timewatch.createController({
        playbackManager: { play: () => {}, getCurrentPlayer: () => null },
        events: null,
        apiClient: client,
        storage: fakeStorage({}),
        userId: 'u1'
    });

    try {
        const first = controller.refresh();
        let secondResolved = false;
        const second = controller.refresh();
        second.then(() => { secondResolved = true; });

        await settled();
        assert.equal(client.calls.length, 1, 'the two refreshes share the one request');
        assert.equal(secondResolved, false, 'a refresh must not resolve before an answer');

        client.calls[0].reject({ status: 404 });
        await Promise.all([first, second]);
        assert.equal(secondResolved, true);
    } finally {
        controller.destroy();
    }
});

test('the teaser follows the Suggestion on offer instead of latching onto the first', async (t) => {
    t.mock.timers.enable({ apis: ['setTimeout', 'setInterval'] });
    const document = fakeDocument();
    globalThis.document = document;
    t.after(() => { delete globalThis.document; });

    const client = fakeClient();
    const controller = timewatch.createController({
        playbackManager: playingEpisode({ id: 'now-playing', runtimeSeconds: 3000, remainingSeconds: 35 }),
        events: null,
        apiClient: client,
        storage: fakeStorage({}),
        userId: 'u1'
    });
    t.after(() => controller.destroy());

    controller.start();
    client.calls[0].resolve({ Spent: false, RemainingSeconds: 3600, Suggestions: [suggestion('Good Clean Fun')] });
    await settled();

    t.mock.timers.tick(1000);
    assert.match(teaserText(document), /Good Clean Fun/);

    // The engine stops offering that episode — it was watched elsewhere, or no longer fits.
    // A teaser still naming it would play something the engine would not suggest.
    const refreshed = controller.refresh();
    client.calls[client.calls.length - 1]
        .resolve({ Spent: false, RemainingSeconds: 3000, Suggestions: [suggestion('Defiant Jazz')] });
    await refreshed;

    t.mock.timers.tick(1000);
    assert.match(teaserText(document), /Defiant Jazz/);
});

test('a surprise while handling a response degrades the feature instead of ending polling', async (t) => {
    t.mock.timers.enable({ apis: ['setTimeout', 'setInterval'] });
    const client = fakeClient();
    // No document at all, so putting up the Spent card throws: any such surprise used to
    // skip schedulePoll() and freeze the client in its last phase for the whole session.
    const controller = timewatch.createController({
        playbackManager: { play: () => {}, getCurrentPlayer: () => null },
        events: null,
        apiClient: client,
        storage: fakeStorage({ 'timewatch-window-was-running': '1' }),
        userId: 'u1'
    });
    t.after(() => controller.destroy());

    controller.start();
    client.calls[0].resolve({ Spent: true, RemainingSeconds: 0, Suggestions: [] });
    await settled();

    t.mock.timers.tick(30000);
    const polls = client.calls.filter((call) => call.url.indexOf('Suggestions') >= 0);
    assert.ok(polls.length > 1, 'the next poll was scheduled anyway');
});

test('an episode that ends in a throttled background tab still counts as finished', () => {
    // Browsers throttle a background tab's timers to roughly once a minute while the video
    // keeps playing, so the last snapshot can be a minute behind the truth. Carrying it
    // forward by the wall clock is what makes the End Card open at all in that tab.
    const snapshot = { remainingSeconds: 58, readAt: 1000, paused: false };

    assert.equal(timewatch.remainingAtStop(snapshot, 1000), 58);
    assert.equal(timewatch.remainingAtStop(snapshot, 61000), 0);
    assert.equal(timewatch.remainingAtStop(snapshot, 46000), 13);

    // A paused item makes no progress, so its snapshot is carried forward by nothing: a
    // long pause followed by a stop is somebody giving up, not an episode finishing.
    assert.equal(
        timewatch.remainingAtStop({ remainingSeconds: 58, readAt: 1000, paused: true }, 601000),
        58);

    // An unknown position stays unknown rather than becoming a finished item.
    assert.equal(timewatch.remainingAtStop({ remainingSeconds: null, readAt: 1000 }, 61000), null);
    assert.equal(timewatch.remainingAtStop(null, 61000), null);
});
