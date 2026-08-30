import { describe, expect, it, vi, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { AppBar } from '../../src/panes/AppBar.js';

function jsonResponse(body: unknown): Response {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'content-type': 'application/json' } });
}

describe('AppBar', () => {
  afterEach(() => vi.restoreAllMocks());

  it('offers no Admin destination while capabilities have not (yet) confirmed it', () => {
    // No fetch mock at all: exercises the real default before any response lands, the same state a
    // failed or unmocked request leaves it in permanently. See useAdminCapabilities.
    render(<AppBar page="rules" />);
    expect(screen.queryByRole('link', { name: 'Admin' })).toBeNull();
  });

  it('offers the Admin destination once capabilities confirm the caller is a grant administrator', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      jsonResponse({ grantAdministration: true, administrator: true, devIdentity: false }),
    );
    render(<AppBar page="rules" />);

    expect(await screen.findByRole('link', { name: 'Admin' })).toBeTruthy();
  });

  it('withholds the Admin destination when the grant source cannot be administered', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      jsonResponse({ grantAdministration: false, administrator: true, devIdentity: true }),
    );
    render(<AppBar page="rules" />);

    await waitFor(() => expect(screen.queryByRole('link', { name: 'Admin' })).toBeNull());
  });

  it('keeps navigation labels as words, so the destination is readable', () => {
    render(<AppBar page="rules" />);
    expect(screen.getByRole('link', { name: 'Rules' })).toBeTruthy();
    expect(screen.getByRole('link', { name: 'Propositions' })).toBeTruthy();
  });

  it('is a labelled navigation landmark rather than a tablist', () => {
    // The switch changes the route and controls no panel, so `tablist`/`tab` promised a
    // relationship that was never there — and the two adjacent surfaces wearing the same role
    // while only one meant it was the part that misled. EditorPane's Builder/DSL tabs are the
    // ones that really are tabs.
    render(<AppBar page="rules" />);

    expect(screen.getByRole('navigation', { name: 'Pages' })).toBeTruthy();
    expect(screen.queryByRole('tablist')).toBeNull();
    expect(screen.queryByRole('tab')).toBeNull();
  });

  it('points each page at its own hash, so the destination is visible and openable in a new tab', () => {
    // The href is the navigation: no click handler intercepts it, so middle-click, ⌘-click and
    // the status-bar preview all work without anything here arranging for them.
    render(<AppBar page="rules" />);

    expect(screen.getByRole('link', { name: 'Rules' }).getAttribute('href')).toBe('#/rules');
    expect(screen.getByRole('link', { name: 'Propositions' }).getAttribute('href'))
      .toBe('#/propositions');
  });

  it('marks the current page with aria-current, the state a link has to say it with', () => {
    render(<AppBar page="propositions" />);

    expect(screen.getByRole('link', { name: 'Propositions' }).getAttribute('aria-current'))
      .toBe('page');
    expect(screen.getByRole('link', { name: 'Rules' }).getAttribute('aria-current')).toBeNull();
  });

  it('hides the nav glyph from the accessible name', () => {
    // If the glyph were exposed, the link would announce as something other than its word.
    const { container } = render(<AppBar page="rules" />);
    const glyphs = container.querySelectorAll('.page-nav svg');
    expect(glyphs.length).toBe(2);
    glyphs.forEach((glyph) => expect(glyph.getAttribute('aria-hidden')).toBe('true'));
  });
});
