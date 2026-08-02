import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CommandPalette } from '../../src/shell/CommandPalette.js';

interface Row { id: string; }
// A fourth "customer" row keeps the post-"customer"-filter match set at 3 rows instead of 2, so a
// stale cursor of 2 (left over from two ArrowDowns) is *in bounds* and resolves to matches[2] —
// the wrong row — rather than falling out of bounds to the same matches[0] a correct reset would
// produce. Without this row, "resets the highlight when the query changes" cannot tell a real
// reset from a cursor that merely overflowed to the same fallback answer.
const ROWS: Row[] = [
  { id: 'customer.is-active' },
  { id: 'customer.is-adult' },
  { id: 'orders.is-large' },
  { id: 'customer.is-verified' },
];

const setup = (overrides: Partial<Parameters<typeof CommandPalette<Row>>[0]> = {}) => {
  const onChoose = vi.fn();
  const onClose = vi.fn();
  render(
    <CommandPalette<Row>
      label="Propositions"
      placeholder="Filter…"
      items={ROWS}
      match={(item, query) => item.id.includes(query)}
      renderItem={(item) => item.id}
      onChoose={onChoose}
      onClose={onClose}
      {...overrides}
    />,
  );
  return { onChoose, onClose };
};

describe('CommandPalette', () => {
  it('opens with the search input focused, so typing needs no click', () => {
    setup();
    expect(document.activeElement).toBe(screen.getByRole('combobox'));
  });

  it('filters to matching rows as the query is typed', async () => {
    setup();
    await userEvent.type(screen.getByRole('combobox'), 'adult');
    const options = screen.getAllByRole('option');
    expect(options).toHaveLength(1);
    expect(options[0]!.textContent).toBe('customer.is-adult');
  });

  it('moves the highlight with the arrow keys without moving focus off the input', async () => {
    // aria-activedescendant is what lets the highlight move while the caret stays put; without
    // it every arrow key would be a round trip out of the search box and back.
    setup();
    const input = screen.getByRole('combobox');
    await userEvent.keyboard('{ArrowDown}');
    expect(document.activeElement).toBe(input);
    expect(input.getAttribute('aria-activedescendant'))
      .toBe(screen.getAllByRole('option')[1]!.id);
  });

  it('chooses the highlighted row on Enter', async () => {
    const { onChoose } = setup();
    await userEvent.keyboard('{ArrowDown}{Enter}');
    expect(onChoose).toHaveBeenCalledWith(ROWS[1]);
  });

  it('chooses the row that was clicked, not the one highlighted', async () => {
    // A mouse user never touched the arrow keys, so choosing the highlight would select
    // something they never pointed at.
    const { onChoose } = setup();
    await userEvent.keyboard('{ArrowDown}');
    await userEvent.click(screen.getByText('orders.is-large'));
    expect(onChoose).toHaveBeenCalledWith(ROWS[2]);
    expect(onChoose).toHaveBeenCalledTimes(1);
  });

  it('resets the highlight when the query changes', async () => {
    // The row under the highlight is not the row that was under it before the list changed.
    const { onChoose } = setup();
    await userEvent.keyboard('{ArrowDown}{ArrowDown}');
    await userEvent.type(screen.getByRole('combobox'), 'customer');
    await userEvent.keyboard('{Enter}');
    expect(onChoose).toHaveBeenCalledWith(ROWS[0]);
  });

  it('browses instead of listing when the query is empty', async () => {
    setup({ renderBrowse: () => <p>browse view</p> });
    expect(screen.getByText('browse view')).toBeTruthy();
    await userEvent.type(screen.getByRole('combobox'), 'orders');
    expect(screen.queryByText('browse view')).toBeNull();
    expect(screen.getAllByRole('option')).toHaveLength(1);
  });

  it('hands the footer the highlighted item', async () => {
    setup({ footer: (highlighted) => <span>target: {highlighted?.id ?? 'none'}</span> });
    await userEvent.type(screen.getByRole('combobox'), 'orders');
    expect(screen.getByText(/target: orders.is-large/)).toBeTruthy();
  });

  it('does nothing on Enter when nothing matched', async () => {
    const { onChoose } = setup();
    await userEvent.type(screen.getByRole('combobox'), 'nothing-matches-this');
    await userEvent.keyboard('{Enter}');
    expect(onChoose).not.toHaveBeenCalled();
  });
});
