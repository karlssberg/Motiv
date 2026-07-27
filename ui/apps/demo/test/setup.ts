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

afterEach(() => cleanup());
