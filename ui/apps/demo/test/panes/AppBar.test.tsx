import { describe, expect, it, vi, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AppBar } from '../../src/panes/AppBar.js';

function jsonResponse(body: unknown): Response {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'content-type': 'application/json' } });
}

describe('AppBar', () => {
  afterEach(() => vi.restoreAllMocks());

  it('offers no Admin tab while capabilities have not (yet) confirmed it', () => {
    // No fetch mock at all: exercises the real default before any response lands, the same state a
    // failed or unmocked request leaves it in permanently. See useAdminCapabilities.
    render(<AppBar page="rules" onNavigate={() => {}} />);
    expect(screen.queryByRole('tab', { name: 'Admin' })).toBeNull();
  });

  it('offers the Admin tab once capabilities confirm the caller is a grant administrator', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      jsonResponse({ grantAdministration: true, administrator: true, devIdentity: false }),
    );
    render(<AppBar page="rules" onNavigate={() => {}} />);

    expect(await screen.findByRole('tab', { name: 'Admin' })).toBeTruthy();
  });

  it('withholds the Admin tab when the grant source cannot be administered', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      jsonResponse({ grantAdministration: false, administrator: true, devIdentity: true }),
    );
    render(<AppBar page="rules" onNavigate={() => {}} />);

    await waitFor(() => expect(screen.queryByRole('tab', { name: 'Admin' })).toBeNull());
  });

  it('keeps navigation labels as words, so the destination is readable', () => {
    render(<AppBar page="rules" onNavigate={() => {}} />);
    expect(screen.getByRole('tab', { name: 'Rules' })).toBeTruthy();
    expect(screen.getByRole('tab', { name: 'Propositions' })).toBeTruthy();
  });

  it('marks the current page as selected', () => {
    render(<AppBar page="propositions" onNavigate={() => {}} />);
    expect(screen.getByRole('tab', { name: 'Propositions' }).getAttribute('aria-selected')).toBe('true');
    expect(screen.getByRole('tab', { name: 'Rules' }).getAttribute('aria-selected')).toBe('false');
  });

  it('navigates on click', async () => {
    const onNavigate = vi.fn();
    render(<AppBar page="rules" onNavigate={onNavigate} />);
    await userEvent.click(screen.getByRole('tab', { name: 'Propositions' }));
    expect(onNavigate).toHaveBeenCalledWith('propositions');
  });

  it('hides the nav glyph from the accessible name', () => {
    // If the glyph were exposed, the tab would announce as something other than its word.
    const { container } = render(<AppBar page="rules" onNavigate={() => {}} />);
    const glyphs = container.querySelectorAll('.page-tabs svg');
    expect(glyphs.length).toBe(2);
    glyphs.forEach((glyph) => expect(glyph.getAttribute('aria-hidden')).toBe('true'));
  });
});
