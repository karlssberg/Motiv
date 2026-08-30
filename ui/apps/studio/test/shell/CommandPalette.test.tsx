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
  it('commits autoFocus onto the search input, which is not the same as opening focused', () => {
    // Named for what it proves and no more. React's `autoFocus` is not the HTML attribute — it is
    // an imperative `focus()` during commit, and it renders no attribute to assert on, so this is
    // the only trace of it. In a browser `showModal()` then runs the dialog focusing steps *after*
    // that commit and decides the caret for itself, which is how the real focus-order bug — every
    // modal opening on Close — survived a green run of this very test.
    //
    // Not a hole: what actually lands the caret here is the close button being rendered last.
    // `Modal.test.tsx › puts the close control last` pins that structure, and
    // `propositions.spec.ts › the palette traps focus…` measures the outcome in a browser.
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

  it('holds no highlight while browsing, where no row is on screen to be highlighted', async () => {
    // The browse view renders instead of the list, so there is no row under the highlight to see,
    // to choose, or to hand a footer. Reporting matches[0] anyway would make Enter choose a row
    // nobody was shown, and point aria-activedescendant at an id that is not in the document.
    const { onChoose } = setup({
      renderBrowse: () => <p>browse view</p>,
      footer: (highlighted) => <span>target: {highlighted?.id ?? 'none'}</span>,
    });

    expect(screen.getByText('target: none')).toBeTruthy();
    expect(screen.getByRole('combobox').getAttribute('aria-activedescendant')).toBeNull();
    await userEvent.keyboard('{Enter}');
    expect(onChoose).not.toHaveBeenCalled();
  });

  it('names no list while browsing, where there is no list in the document to name', async () => {
    // The browse view renders *instead of* the listbox, so `aria-controls` pointed the combobox at
    // an id nothing in the document carried — an invalid IDREF, and in the palette's default state:
    // every open of the Propositions palette started there. The comment justifying the empty <ul>
    // that stays mounted turns on exactly this, so the browse branch was contradicting it.
    setup({ renderBrowse: () => <p>browse view</p> });
    const input = screen.getByRole('combobox');

    expect(input.getAttribute('aria-controls')).toBeNull();

    await userEvent.type(input, 'orders');

    const controls = input.getAttribute('aria-controls');
    expect(controls).not.toBeNull();
    expect(document.getElementById(controls!)).toBe(screen.getByRole('listbox'));
  });

  it('renders the empty state with the query that found nothing', async () => {
    // "Nothing matched" is a statement about the query, so the query is what the caller is handed
    // to say it with.
    setup({ renderEmpty: (query) => <p>nothing like “{query}”</p> });

    await userEvent.type(screen.getByRole('combobox'), 'zzz');

    expect(screen.getByText('nothing like “zzz”')).toBeTruthy();
  });

  it('does nothing on Enter when nothing matched', async () => {
    const { onChoose } = setup();
    await userEvent.type(screen.getByRole('combobox'), 'nothing-matches-this');
    await userEvent.keyboard('{Enter}');
    expect(onChoose).not.toHaveBeenCalled();
  });
});

/**
 * Ticket 18 names the palette a known announcement trap. The visible `N of M` count is the only
 * feedback that typing narrowed anything, and a plain `<span>` that changes is not announced at
 * all — so a screen-reader user typing into the box hears nothing back from a list they cannot
 * see, whether it narrowed to one row or to none.
 */
describe('CommandPalette announcements', () => {
  it('announces how many rows a query left, rather than only showing it', async () => {
    setup();
    const status = screen.getByRole('status');
    expect(status.textContent).toBe('4 of 4');

    await userEvent.type(screen.getByRole('combobox'), 'orders');
    expect(screen.getByRole('status').textContent).toBe('1 of 4');
  });

  it('says so when a query matched nothing, which the empty list cannot', async () => {
    setup();
    await userEvent.type(screen.getByRole('combobox'), 'nothing-matches-this');
    expect(screen.getByRole('status').textContent).toBe('0 of 4');
  });

  it('announces nothing while browsing, where there is no result set to report', () => {
    setup({ renderBrowse: () => <p>browse</p> });
    expect(screen.queryByRole('status')).toBeNull();
  });
});
