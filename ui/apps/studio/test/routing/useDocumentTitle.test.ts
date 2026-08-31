import { describe, it, expect, afterEach } from 'vitest';
import { renderHook } from '@testing-library/react';
import type { Route } from '../../src/routing/useHashRoute.js';
import { titleFor, useDocumentTitle } from '../../src/routing/useDocumentTitle.js';

/**
 * 2.4.2 Page Titled, which the conformance enumeration is what found.
 *
 * axe's `document-title` rule checks that a title exists, and a single-page application satisfies it
 * with the one `<title>` in `index.html` — so the sweep was green while all three routes shared the
 * name "Motiv Studio". The title is what a screen reader announces on navigation and what the
 * history list shows, so one title for three pages is a real loss, invisible to the scan.
 */

const ORIGINAL = document.title;
afterEach(() => { document.title = ORIGINAL; });

describe('titleFor', () => {
  it('names the page', () => {
    expect(titleFor({ page: 'rules', name: null })).toBe('Rules — Motiv Studio');
    expect(titleFor({ page: 'propositions', name: null })).toBe('Propositions — Motiv Studio');
    expect(titleFor({ page: 'admin', name: null })).toBe('Admin — Motiv Studio');
  });

  it('leads with the selection, which is what the route is actually about', () => {
    // The name first, because a history list and a tab strip both truncate from the right, and the
    // half worth keeping is which proposition — not which application.
    expect(titleFor({ page: 'propositions', name: 'customer.is-active' }))
      .toBe('customer.is-active — Propositions — Motiv Studio');
    expect(titleFor({ page: 'rules', name: 'customer.can-checkout' }))
      .toBe('customer.can-checkout — Rules — Motiv Studio');
  });

  it('ignores a selection that is only whitespace', () => {
    // A hash of `#/rules/%20` parses to a name, and a title that begins with a dash names nothing.
    expect(titleFor({ page: 'rules', name: '   ' })).toBe('Rules — Motiv Studio');
  });
});

describe('useDocumentTitle', () => {
  it('writes the title, and rewrites it when the route changes', () => {
    const initialProps: Route = { page: 'rules', name: null };
    const { rerender } = renderHook((route: Route) => useDocumentTitle(route), { initialProps });

    expect(document.title).toBe('Rules — Motiv Studio');

    rerender({ page: 'propositions', name: 'customer.is-adult' });

    expect(document.title).toBe('customer.is-adult — Propositions — Motiv Studio');
  });
});
