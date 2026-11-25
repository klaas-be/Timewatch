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
