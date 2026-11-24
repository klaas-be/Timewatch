/*
 * Timewatch's injected client script — PLACEHOLDER.
 *
 * Ticket 06 wires the endpoint that serves this file; ticket 09 replaces its contents with
 * the real client plugin: the teaser, the End Card and the two suppression leases. Until
 * then this exists so that the endpoint serves something, and so that the injected
 * <script src> resolves instead of 404ing on a freshly built plugin.
 *
 * It must stay inert. It registers nothing on window, so jellyfin-web's pluginManager does
 * not find a "Timewatch" factory and simply skips the entry our config.json transformation
 * added — which is the degrade-to-inert behaviour the spec asks for anyway.
 */
(function () {
    'use strict';

    console.debug('[Timewatch] placeholder script loaded; the End Card arrives with ticket 09.');
})();
