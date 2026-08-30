import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { PropositionListEntry } from '@motiv-rules/core';
import { PropositionExplorer } from '../../src/explorer/PropositionExplorer.js';

function entry(overrides: Partial<PropositionListEntry> & { name: string }): PropositionListEntry {
  return {
    modelType: 'customer', metadataType: 'String', isAsync: false,
    origin: 'Authored', version: 1, description: null, quarantine: [],
    ...overrides,
  };
}

const ENTRIES = [
  entry({ name: 'customer.eligibility.is-active', origin: 'Compiled', version: 0 }),
  entry({ name: 'customer.eligibility.is-adult' }),
  entry({ name: 'customer.risk.is-fraudulent' }),
  entry({ name: 'order.is-large', modelType: 'order' }),
];

function renderExplorer(overrides: Partial<Parameters<typeof PropositionExplorer>[0]> = {}) {
  const actions = {
    onSelect: vi.fn(), onDerive: vi.fn(), onOverride: vi.fn(), onNew: vi.fn(), onDelete: vi.fn(),
    onClose: vi.fn(),
  };
  render(
    <PropositionExplorer entries={ENTRIES} selected={null} actions={actions} {...overrides} />,
  );
  return actions;
}

/** Types into the palette's search box, which leaves the browse view for the flat match list. */
async function search(needle: string): Promise<void> {
  await userEvent.type(screen.getByRole('combobox'), needle);
}

/** What an unavailable action says for itself — the reason its `aria-describedby` points at. */
function reasonFor(name: RegExp): string {
  const button = screen.getByRole('button', { name });
  expect(button.getAttribute('aria-disabled')).toBe('true');
  return document.getElementById(button.getAttribute('aria-describedby')!)?.textContent ?? '';
}

describe('PropositionExplorer', () => {
  it('renders every proposition as a leaf', () => {
    renderExplorer();

    expect(screen.getByRole('treeitem', { name: /is-active/ })).toBeTruthy();
    expect(screen.getByRole('treeitem', { name: /is-large/ })).toBeTruthy();
  });

  it('groups leaves under their namespace', () => {
    renderExplorer();

    const customer = screen.getByRole('treeitem', { name: /^customer/ });
    const order = screen.getByRole('treeitem', { name: /^order/ });

    // That both rows exist says nothing about grouping — the payoff is the *nesting*, so the
    // owned `role="group"` and the rows inside it are what is asserted. `getAllByRole` because
    // `customer` holds a group per namespace level; the first in document order is its own.
    const customerGroup = within(customer).getAllByRole('group')[0]!;
    expect(within(customerGroup).getByRole('treeitem', { name: /^risk/ })).toBeTruthy();
    expect(within(order).getAllByRole('group')[0]).toBeTruthy();
    expect(within(order).getByRole('treeitem', { name: /is-large/ })).toBeTruthy();
  });

  it('flattens to matching rows as you type, matching the full dotted path', async () => {
    renderExplorer();

    await search('risk.is-fraud');

    // Hierarchy is noise in a result list, so the tree goes and one row per match takes its place
    // — and the query is matched against the whole dotted path, not the leaf segment.
    const rows = screen.getAllByRole('option');
    expect(rows).toHaveLength(1);
    expect(rows[0]!.textContent).toContain('is-fraudulent');
    expect(screen.queryByRole('treeitem')).toBeNull();
  });

  it('shows the namespace a match came from, so two leaves of a name stay apart', async () => {
    renderExplorer();

    await search('is-fraudulent');

    expect(screen.getByRole('option').textContent).toContain('customer.risk.');
  });

  it('reports how many propositions match', async () => {
    renderExplorer();

    await search('eligibility');

    expect(screen.getByText(/2 of 4/)).toBeTruthy();
  });

  it('chooses a matched row and closes on the way out', async () => {
    const actions = renderExplorer();

    await search('is-fraudulent');
    await userEvent.keyboard('{Enter}');

    expect(actions.onSelect).toHaveBeenCalledWith('customer.risk.is-fraudulent');
    // Choosing is the palette's whole purpose: leaving it open would leave the user to dismiss a
    // modal they are finished with.
    expect(actions.onClose).toHaveBeenCalled();
  });

  it('narrows to one model when a chip is toggled', async () => {
    renderExplorer();

    await userEvent.click(screen.getByRole('button', { name: 'order' }));

    expect(screen.queryByRole('treeitem', { name: /is-large/ })).toBeTruthy();
    expect(screen.queryByRole('treeitem', { name: /is-adult/ })).toBeNull();
    // The count is the only thing that says how much was narrowed away, and it is what the tree
    // beside it cannot show — one of four survives here, so a count reporting the unfiltered total
    // reads as "nothing was filtered" and is wrong by three.
    expect(screen.getByText('1 of 4')).toBeTruthy();
  });

  it('selects a proposition when its leaf is clicked, and closes', async () => {
    const actions = renderExplorer();

    await userEvent.click(screen.getByRole('treeitem', { name: /is-fraudulent/ }));

    expect(actions.onSelect).toHaveBeenCalledWith('customer.risk.is-fraudulent');
    expect(actions.onClose).toHaveBeenCalled();
  });

  it('does not select a namespace that holds no proposition', async () => {
    const actions = renderExplorer();

    await userEvent.click(screen.getByRole('treeitem', { name: /^customer/ }));

    expect(actions.onSelect).not.toHaveBeenCalled();
  });

  it('marks the selected leaf', () => {
    renderExplorer({ selected: 'customer.risk.is-fraudulent' });

    expect(screen.getByRole('treeitem', { name: /is-fraudulent/ }).getAttribute('aria-selected'))
      .toBe('true');
  });

  it('badges an origin on each leaf', () => {
    renderExplorer();

    expect(screen.getByRole('treeitem', { name: /is-active/ }).textContent).toContain('compiled');
    expect(screen.getByRole('treeitem', { name: /is-adult/ }).textContent).toContain('authored');
  });

  it('shows the model type as a pill', () => {
    renderExplorer();

    expect(screen.getByRole('treeitem', { name: /is-large/ }).textContent).toContain('order');
  });

  it('marks a quarantined proposition and shows why', () => {
    renderExplorer({
      entries: [entry({
        name: 'customer.broken',
        quarantine: [{ path: '$', code: 'UnknownSpec', message: 'unknown spec' }],
      })],
    });

    const leaf = screen.getByRole('treeitem', { name: /broken/ });
    expect(leaf.textContent).toContain('quarantined');
    expect(leaf.getAttribute('title')).toContain('unknown spec');
  });

  it('keeps quarantine distinct from origin', () => {
    // Quarantine is orthogonal, not a fourth origin — both marks must show
    renderExplorer({
      entries: [entry({
        name: 'customer.eligibility.is-active',
        origin: 'Overridden',
        quarantine: [{ path: '$', code: 'UnknownSpec', message: 'gone' }],
      })],
    });

    const leaf = screen.getByRole('treeitem', { name: /is-active/ });
    expect(leaf.textContent).toContain('overridden');
    expect(leaf.textContent).toContain('quarantined');
  });

  it('derives from a leaf', async () => {
    const actions = renderExplorer({ selected: 'customer.risk.is-fraudulent' });

    await userEvent.click(screen.getByRole('button', { name: /derive/i }));

    expect(actions.onDerive).toHaveBeenCalledWith('customer.risk.is-fraudulent');
  });

  it('starts a new proposition', async () => {
    const actions = renderExplorer();

    await userEvent.click(screen.getByRole('button', { name: /^new/i }));

    expect(actions.onNew).toHaveBeenCalled();
  });

  it('says so when nothing matches, naming the query that found nothing', async () => {
    renderExplorer();

    await search('zzz');

    expect(screen.getByText(/no propositions match/i).textContent).toContain('zzz');
  });

  it('does not blame an empty query for an empty catalog', () => {
    // "No propositions match ''" is what a listing shows before it has arrived, and what a
    // genuinely empty catalog shows forever. Neither is a failed search.
    renderExplorer({ entries: [] });

    expect(screen.queryByText(/no propositions match/i)).toBeNull();
    expect(screen.getByText(/no propositions yet/i)).toBeTruthy();
  });

  it('does not let a click on an unselectable middle row bubble into a selectable ancestor', async () => {
    // customer (dual role: namespace AND proposition) > customer.risk (bare namespace, no entry
    // of its own) > customer.risk.is-fraudulent (leaf). Clicking the *middle* row must not select
    // the ancestor — an unhandled click on a bare namespace must not bubble into whichever
    // ancestor happens to have a handler.
    const actions = renderExplorer({
      entries: [
        entry({ name: 'customer', modelType: 'customer' }),
        entry({ name: 'customer.risk.is-fraudulent' }),
      ],
    });

    await userEvent.click(screen.getByRole('treeitem', { name: /^risk/ }));

    expect(actions.onSelect).not.toHaveBeenCalled();
  });

  it('selects only the leaf when the leaf sits directly under a selectable ancestor', async () => {
    // customer (dual role: namespace AND proposition) > customer.is-fraudulent (leaf), with no
    // bare namespace between them to absorb the bubble. Every treeitem in the chain carries a
    // handler, so a click allowed to bubble is handled again by the ancestor — and the ancestor
    // runs *last*, so it would win and quietly replace the selection the user made.
    const actions = renderExplorer({
      entries: [
        entry({ name: 'customer', modelType: 'customer' }),
        entry({ name: 'customer.is-fraudulent' }),
      ],
    });

    await userEvent.click(screen.getByRole('treeitem', { name: /is-fraudulent/ }));

    expect(actions.onSelect).toHaveBeenCalledTimes(1);
    expect(actions.onSelect).toHaveBeenCalledWith('customer.is-fraudulent');
  });

  it('composes the accessible name from segment, origin and quarantine state', () => {
    // aria-label overrides content in ARIA name computation, so origin/quarantine — the badges
    // that are this task's whole deliverable — must be composed into the name explicitly, or an
    // assistive-tech user gets none of it.
    renderExplorer({
      entries: [entry({
        name: 'customer.eligibility.is-active',
        origin: 'Overridden',
        quarantine: [{ path: '$', code: 'UnknownSpec', message: 'gone' }],
      })],
    });

    const leaf = screen.getByRole('treeitem', { name: /is-active/ });
    expect(leaf.getAttribute('aria-label')).toContain('overridden');
    expect(leaf.getAttribute('aria-label')).toContain('quarantined');
  });

  it('activates a focused leaf via Enter', () => {
    const actions = renderExplorer();
    const leaf = screen.getByRole('treeitem', { name: /is-fraudulent/ });

    leaf.focus();
    fireEvent.keyDown(leaf, { key: 'Enter' });

    expect(actions.onSelect).toHaveBeenCalledWith('customer.risk.is-fraudulent');
  });

  it('activates a focused leaf via Space, and prevents the page from scrolling', () => {
    const actions = renderExplorer();
    const leaf = screen.getByRole('treeitem', { name: /is-fraudulent/ });
    leaf.focus();

    // fireEvent returns false exactly when preventDefault() was called on the dispatched event.
    const notPrevented = fireEvent.keyDown(leaf, { key: ' ' });

    expect(actions.onSelect).toHaveBeenCalledWith('customer.risk.is-fraudulent');
    expect(notPrevented).toBe(false);
  });

  it('leaves aria-selected off a bare namespace entirely', () => {
    // In ARIA `aria-selected="false"` means *selectable, not currently selected* — the opposite of
    // what a bare namespace is. Omission is what says "not selectable", exactly as the absent
    // tabindex one line down already does.
    renderExplorer();

    const namespaceNode = screen.getByRole('treeitem', { name: /^customer/ });
    expect(namespaceNode.getAttribute('aria-selected')).toBeNull();
  });

  it('keeps every row but the tree\u2019s one stop out of the tab order', () => {
    // Out of the *tab* order, not out of reach: `-1` is what makes a row the arrow keys\u2019 to
    // move to and not the Tab key\u2019s. A bare namespace is included, unlike `aria-selected` one
    // line up \u2014 a tree navigates its structure even where it cannot select.
    renderExplorer({ selected: 'customer.risk.is-fraudulent' });

    expect(screen.getByRole('treeitem', { name: /^customer/ }).getAttribute('tabindex')).toBe('-1');
    expect(screen.getByRole('treeitem', { name: /is-active/ }).getAttribute('tabindex')).toBe('-1');
    expect(screen.getByRole('treeitem', { name: /is-fraudulent/ }).getAttribute('tabindex'))
      .toBe('0');
  });

  it('offers Override for a compiled entry whose model has a sibling spec', () => {
    renderExplorer({ selected: 'customer.eligibility.is-active' });

    expect(screen.getByRole('button', { name: /^override/i }).getAttribute('aria-disabled'))
      .toBeNull();
  });

  it('will not override a compiled entry that is the only spec of its model, and says why', () => {
    // Override composes from another spec over the same model, and UI propositions are
    // composition-only — so this one has nothing to build an override from. The button stays
    // reachable and carries that reason rather than vanishing.
    renderExplorer({
      entries: [entry({ name: 'order.is-large', modelType: 'order', origin: 'Compiled', version: 0 })],
      selected: 'order.is-large',
    });

    expect(reasonFor(/^override/i)).toContain('order');
  });

  it('will not override an already-authored entry, and says why', async () => {
    // Override mints the overlay for a name served *only* by a compiled spec. Run against an
    // authored one it would POST at a name that already has an overlay, which can only come back
    // `nameTaken`.
    renderExplorer({ selected: 'customer.eligibility.is-adult' });

    expect(reasonFor(/^override/i)).toMatch(/compiled/i);
  });

  it('will not override an entry that is already overridden', async () => {
    renderExplorer({
      entries: [
        entry({ name: 'customer.eligibility.is-active', origin: 'Overridden', version: 2 }),
        entry({ name: 'customer.eligibility.is-adult' }),
      ],
      selected: 'customer.eligibility.is-active',
    });

    expect(reasonFor(/^override/i)).toMatch(/compiled/i);
  });

  it('does nothing when an unavailable Override is clicked anyway', async () => {
    // `aria-disabled` is a claim to assistive technology, not an enforcement — the click still
    // arrives, so the handler has to refuse it.
    const actions = renderExplorer({ selected: 'customer.eligibility.is-adult' });

    await userEvent.click(screen.getByRole('button', { name: /^override/i }));

    expect(actions.onOverride).not.toHaveBeenCalled();
  });

  it('calls onOverride with the selected name', async () => {
    const actions = renderExplorer({ selected: 'customer.eligibility.is-active' });

    await userEvent.click(screen.getByRole('button', { name: /^override/i }));

    expect(actions.onOverride).toHaveBeenCalledWith('customer.eligibility.is-active');
  });

  it('offers no Delete at all for a compiled entry', () => {
    // Every other unavailable action explains itself instead of disappearing. This one genuinely
    // has no target: there is no authored document under a compiled name to delete or revert.
    renderExplorer({ selected: 'customer.eligibility.is-active' });

    expect(screen.queryByRole('button', { name: /delete|revert/i })).toBeNull();
  });

  it('calls a delete on an overridden entry a revert', () => {
    // The same DELETE either way, but what it does to the name differs: an override reverts to the
    // compiled spec it shadowed, while an authored proposition goes for good.
    renderExplorer({
      entries: [entry({ name: 'customer.eligibility.is-active', origin: 'Overridden', version: 2 })],
      selected: 'customer.eligibility.is-active',
    });

    expect(screen.getByRole('button', { name: 'Revert to compiled' })).toBeTruthy();
  });

  it('deletes the entry it is aimed at', async () => {
    const actions = renderExplorer({ selected: 'customer.eligibility.is-adult' });

    await userEvent.click(screen.getByRole('button', { name: /^delete$/i }));

    expect(actions.onDelete).toHaveBeenCalledWith(
      expect.objectContaining({ name: 'customer.eligibility.is-adult' }));
  });

  it('aims the actions at the highlighted row once a query is typed', async () => {
    // The selection is what the user chose last; the highlight is what they are pointing at now.
    // A footer that went on describing the selection would sit under a row it says nothing about.
    const actions = renderExplorer({ selected: 'customer.eligibility.is-adult' });

    await search('is-fraudulent');
    await userEvent.click(screen.getByRole('button', { name: /derive/i }));

    expect(actions.onDerive).toHaveBeenCalledWith('customer.risk.is-fraudulent');
  });

  it('says what the actions are missing when nothing is selected and nothing highlighted', () => {
    renderExplorer();

    expect(reasonFor(/derive/i)).toMatch(/pick a proposition/i);
    expect(reasonFor(/^override/i)).toMatch(/pick a proposition/i);
    expect(reasonFor(/^delete$/i)).toMatch(/pick a proposition/i);
  });
});

/**
 * The palette's namespace browser as a tree that means it: one tab stop, arrow keys to move,
 * Home/End to the ends, and type-ahead — the WAI-ARIA pattern `role="tree"` promises.
 *
 * The tree is never collapsed, so "visible order" is the whole tree in document order:
 * customer › eligibility › is-active › is-adult › risk › is-fraudulent, then order › is-large.
 */
describe('PropositionExplorer namespace navigation', () => {
  /** Press a key on whatever holds focus, as a keyboard user does. */
  function press(key: string, init: Record<string, unknown> = {}): boolean {
    return fireEvent.keyDown(document.activeElement!, { key, ...init });
  }

  function item(name: RegExp): HTMLElement {
    return screen.getByRole('treeitem', { name });
  }

  function focused(): Element | null {
    return document.activeElement;
  }

  it('is a single tab stop rather than a column of them', () => {
    // A tree is one stop in the tab sequence; the arrow keys do the rest. Every row being
    // separately tabbable is precisely what the role says it is not.
    renderExplorer();

    const stops = screen.getAllByRole('treeitem')
      .filter((row) => row.getAttribute('tabindex') === '0');

    expect(stops).toHaveLength(1);
    expect(screen.getAllByRole('treeitem').every((row) => row.hasAttribute('tabindex'))).toBe(true);
  });

  it('puts that stop on the selected proposition, so returning lands where you left', () => {
    renderExplorer({ selected: 'customer.risk.is-fraudulent' });

    expect(item(/is-fraudulent/).getAttribute('tabindex')).toBe('0');
  });

  it('puts it on the first row when nothing is selected', () => {
    renderExplorer();

    expect(item(/^customer/).getAttribute('tabindex')).toBe('0');
  });

  it('is entered at that stop, whatever the tab sequence held before it', async () => {
    renderExplorer({ selected: 'customer.risk.is-fraudulent' });

    // The model chips sit between the search box and the tree, so this walks up to the tree rather
    // than assuming how many stops precede it — what matters is which row it lands on.
    const tree = screen.getByRole('tree');
    for (let stop = 0; stop < 10 && !tree.contains(focused()); stop += 1) await userEvent.tab();

    expect(focused()).toBe(item(/is-fraudulent/));
  });

  it('takes one more Tab to cross, however many rows it holds', async () => {
    renderExplorer({ selected: 'customer.risk.is-fraudulent' });
    item(/is-fraudulent/).focus();

    await userEvent.tab();

    // Eight rows in this tree, and the next stop is already outside it: the inside belongs to the
    // arrow keys. Before, crossing the palette meant a Tab per proposition.
    expect(screen.getByRole('tree').contains(focused())).toBe(false);
  });

  it('moves to the next row on ArrowDown, bare namespaces included', () => {
    renderExplorer();
    item(/^customer/).focus();

    press('ArrowDown');
    expect(focused()).toBe(item(/^eligibility/));

    press('ArrowDown');
    expect(focused()).toBe(item(/is-active/));
  });

  it('moves to the previous row on ArrowUp', () => {
    renderExplorer();
    item(/is-active/).focus();

    press('ArrowUp');

    expect(focused()).toBe(item(/^eligibility/));
  });

  it('crosses out of a subtree, because the movement follows the rows on screen', () => {
    // is-fraudulent is the last row under customer; the next row down is `order`, a sibling of
    // customer itself. Walking the rendered order rather than the siblings is what makes ArrowDown
    // mean "the next row" instead of "the next row at this level".
    renderExplorer();
    item(/is-fraudulent/).focus();

    press('ArrowDown');

    expect(focused()).toBe(item(/^order/));
  });

  it('stops at the ends rather than wrapping', () => {
    // Wrapping would mean ArrowDown can never tell you that you are at the bottom.
    renderExplorer();
    item(/^customer/).focus();

    press('ArrowUp');
    expect(focused()).toBe(item(/^customer/));

    item(/is-large/).focus();
    press('ArrowDown');
    expect(focused()).toBe(item(/is-large/));
  });

  it('goes to the first and last rows on Home and End', () => {
    renderExplorer();
    item(/is-active/).focus();

    press('End');
    expect(focused()).toBe(item(/is-large/));

    press('Home');
    expect(focused()).toBe(item(/^customer/));
  });

  it('enters a subtree on ArrowRight and returns to the parent on ArrowLeft', () => {
    renderExplorer();
    item(/^customer/).focus();

    press('ArrowRight');
    expect(focused()).toBe(item(/^eligibility/));

    press('ArrowLeft');
    expect(focused()).toBe(item(/^customer/));
  });

  it('holds still where there is nowhere to go: no parent above, no child within', () => {
    renderExplorer();
    item(/^customer/).focus();

    press('ArrowLeft');
    expect(focused()).toBe(item(/^customer/));

    item(/is-large/).focus();
    press('ArrowRight');
    expect(focused()).toBe(item(/is-large/));
  });

  it('keeps the arrow keys from scrolling the palette out from under the row', () => {
    renderExplorer();
    item(/^customer/).focus();

    // fireEvent returns false exactly when preventDefault() was called.
    expect(press('ArrowDown')).toBe(false);
    expect(press('Home')).toBe(false);
  });

  it('moves the tab stop with the focus, so leaving and re-entering returns to the same row', () => {
    renderExplorer();
    item(/^customer/).focus();

    press('ArrowDown');

    expect(item(/^eligibility/).getAttribute('tabindex')).toBe('0');
    expect(item(/^customer/).getAttribute('tabindex')).toBe('-1');
  });

  it('jumps to the next row whose name starts with what was typed', () => {
    renderExplorer();
    item(/^customer/).focus();

    press('o');

    expect(focused()).toBe(item(/^order/));
  });

  it('accumulates the typed characters, so two rows sharing a first letter stay apart', () => {
    // `is-active` and `is-adult` are siblings: one character can only ever reach the first.
    renderExplorer();
    item(/^eligibility/).focus();

    press('i');
    expect(focused()).toBe(item(/is-active/));

    press('s');
    press('-');
    press('a');
    press('d');
    expect(focused()).toBe(item(/is-adult/));
  });

  it('wraps round the end of the tree when searching', () => {
    renderExplorer();
    item(/is-large/).focus();

    press('c');

    expect(focused()).toBe(item(/^customer/));
  });

  it('stays put when nothing starts with what was typed', () => {
    renderExplorer();
    item(/^customer/).focus();

    press('z');

    expect(focused()).toBe(item(/^customer/));
  });

  it('leaves a chorded keystroke alone, so the shell shortcuts still reach the window', () => {
    // ⌘K is how the palette opens; a type-ahead that swallowed the K would take the shortcut with
    // it, and Ctrl/Alt chords are the platform's rather than this tree's.
    renderExplorer();
    item(/^customer/).focus();

    expect(press('k', { metaKey: true })).toBe(true);
    expect(focused()).toBe(item(/^customer/));
  });

  it('does not activate a bare namespace, whichever key asks', () => {
    const actions = renderExplorer();
    item(/^customer/).focus();

    press('Enter');
    press(' ');

    expect(actions.onSelect).not.toHaveBeenCalled();
  });
});
