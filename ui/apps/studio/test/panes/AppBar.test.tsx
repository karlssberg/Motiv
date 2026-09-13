import { describe, expect, it, vi, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { AppBar } from '../../src/panes/AppBar.js';

function jsonResponse(body: unknown): Response {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'content-type': 'application/json' } });
}

function grantAdmin(): void {
  vi.spyOn(globalThis, 'fetch').mockResolvedValue(
    jsonResponse({ grantAdministration: true, administrator: true, devIdentity: false }),
  );
}

describe('AppBar', () => {
  afterEach(() => vi.restoreAllMocks());

  it('carries what the page puts beside the brand, and its controls', () => {
    render(<AppBar controls={<button type="button">Act</button>}><div data-testid="strip">strip</div></AppBar>);
    const banner = screen.getByRole('banner');
    expect(banner.textContent).toContain('Motiv');
    expect(screen.getByTestId('strip')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Act' })).toBeTruthy();
  });

  it('offers no page navigation at all until there is somewhere else to go', () => {
    // The Rules / Propositions switch is gone: documents open as tabs. With no Admin grant there is
    // nothing to navigate to, so there is no landmark either.
    render(<AppBar />);
    expect(screen.queryByRole('navigation')).toBeNull();
    expect(screen.queryByRole('link', { name: 'Rules' })).toBeNull();
    expect(screen.queryByRole('link', { name: 'Propositions' })).toBeNull();
  });

  it('offers no Admin destination while capabilities have not (yet) confirmed it', () => {
    render(<AppBar />);
    expect(screen.queryByRole('link', { name: 'Admin' })).toBeNull();
  });

  it('offers the Admin destination once capabilities confirm the caller is a grant administrator', async () => {
    grantAdmin();
    render(<AppBar />);
    const link = await screen.findByRole('link', { name: 'Admin' });
    expect(link.getAttribute('href')).toBe('#/admin');
    expect(link.getAttribute('aria-current')).toBeNull();
    expect(screen.getByRole('navigation', { name: 'Pages' })).toBeTruthy();
  });

  it('withholds the Admin destination when the grant source cannot be administered', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      jsonResponse({ grantAdministration: false, administrator: true, devIdentity: true }),
    );
    render(<AppBar />);
    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(screen.queryByRole('link', { name: 'Admin' })).toBeNull();
  });

  it('on the admin page, marks Admin current and links back to the documents', async () => {
    grantAdmin();
    render(<AppBar current="admin" />);
    expect((await screen.findByRole('link', { name: 'Admin' })).getAttribute('aria-current')).toBe('page');
    expect(screen.getByRole('link', { name: 'Documents' }).getAttribute('href')).toBe('#/rules');
  });

  it('hides the nav glyph from the accessible name', async () => {
    grantAdmin();
    render(<AppBar />);
    const link = await screen.findByRole('link', { name: 'Admin' });
    expect(link.querySelector('svg')?.getAttribute('aria-hidden')).toBe('true');
  });
});
