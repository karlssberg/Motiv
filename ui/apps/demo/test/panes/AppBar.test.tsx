import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AppBar } from '../../src/panes/AppBar.js';

describe('AppBar', () => {
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
