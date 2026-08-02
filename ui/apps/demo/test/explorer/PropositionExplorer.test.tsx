import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { PropositionListEntry } from '@motiv/rules-core';
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

  it('keeps a bare namespace out of the tab order', () => {
    renderExplorer();

    const namespaceNode = screen.getByRole('treeitem', { name: /^customer/ });
    expect(namespaceNode.getAttribute('tabindex')).toBeNull();
  });

  it('puts a selectable leaf in the tab order', () => {
    renderExplorer();

    const leaf = screen.getByRole('treeitem', { name: /is-fraudulent/ });
    expect(leaf.getAttribute('tabindex')).toBe('0');
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
