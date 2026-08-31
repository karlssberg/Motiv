import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ReportBanner } from '../../src/shell/ReportBanner.js';

describe('ReportBanner', () => {
  it('announces itself as an alert, so a report reaches a listener without taking focus', () => {
    render(<ReportBanner>Rules service unavailable (503)</ReportBanner>);

    const alert = screen.getByRole('alert');
    expect(alert.textContent).toContain('Rules service unavailable (503)');
  });

  it('offers no way back when there is nothing to reload', () => {
    render(<ReportBanner>Rules service unavailable (503)</ReportBanner>);

    expect(screen.queryByRole('button')).toBeNull();
  });

  it('runs the reload it was given', async () => {
    const onReload = vi.fn();
    render(<ReportBanner onReload={onReload}>Someone else saved version 9.</ReportBanner>);

    await userEvent.click(screen.getByRole('button', { name: /reload latest/i }));

    expect(onReload).toHaveBeenCalledTimes(1);
  });
});
