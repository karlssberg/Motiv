import { afterEach } from 'vitest';
import { cleanup } from '@testing-library/react';

// jsdom implements no layout, and with it no ResizeObserver. This stub is deliberately inert —
// it never reports an entry — so components that observe an element still mount, and nothing
// here can manufacture a resize that the real DOM would not have produced.
if (!('ResizeObserver' in globalThis)) {
  globalThis.ResizeObserver = class {
    observe(): void {}
    unobserve(): void {}
    disconnect(): void {}
  } as unknown as typeof ResizeObserver;
}

// jsdom implements no layout, and with it no scrollIntoView. Components that scroll a mark into
// view on focus change still need to call something; this stub is deliberately inert.
if (typeof Element.prototype.scrollIntoView !== 'function') {
  Element.prototype.scrollIntoView = function scrollIntoView(): void {};
}

// jsdom 25 defines HTMLDialogElement but implements neither showModal() nor close(), so a
// component that calls them throws on render. These stubs do the one thing jsdom can honestly
// model — the `open` attribute — and nothing else.
//
// THE LIMITS MATTER. jsdom has no top layer, so there is no focus trap, no inertness, and no
// Escape handling here. A unit test asserting any of those would pass or fail for reasons
// unrelated to what ships. Those three behaviours are proven in Playwright (Task 9) and MUST NOT
// be asserted in a jsdom test.
if (typeof HTMLDialogElement !== 'undefined' && typeof HTMLDialogElement.prototype.showModal !== 'function') {
  HTMLDialogElement.prototype.showModal = function showModal(): void { this.open = true; };
  HTMLDialogElement.prototype.close = function close(): void {
    this.open = false;
    this.dispatchEvent(new Event('close'));
  };
}

afterEach(() => cleanup());
