import { describe, it, expect, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {
  RuleEditorStore, RulesApiError, type PropositionListEntry, type RuleDocument,
} from '@motiv-rules/core';
import { RuleEditorProvider } from '@motiv-rules/react';
import { PropositionsPage } from '../../src/panes/PropositionsPage.js';

function entry(overrides: Partial<PropositionListEntry> & { name: string }): PropositionListEntry {
  return {
    modelType: 'customer', metadataType: 'String', isAsync: false,
    origin: 'Authored', version: 1, description: null, quarantine: [],
    ...overrides,
  };
}

/** What GET returns for a name served only by a compiled spec: no document, and version 0. */
const COMPILED = {
  document: null, version: 0, origin: 'Compiled', hasCompiledDefault: true,
};

/** A client stubbed just far enough for the page: only the calls it actually makes. */
function stubClient(overrides: Record<string, unknown> = {}) {
  return {
    listPropositions: vi.fn().mockResolvedValue([
      entry({ name: 'customer.is-active', origin: 'Compiled', version: 0 }),
      entry({ name: 'customer.derived' }),
      entry({ name: 'customer.overridden', origin: 'Overridden', version: 2 }),
    ]),
    getProposition: vi.fn().mockResolvedValue({
      document: { rule: { spec: 'customer.is-active' } }, version: 1,
      origin: 'Authored', hasCompiledDefault: false,
    }),
    getDependents: vi.fn().mockResolvedValue([]),
    createProposition: vi.fn().mockResolvedValue({ outcome: 'saved', version: 1 }),
    putProposition: vi.fn().mockResolvedValue({ outcome: 'saved', version: 2 }),
    deleteProposition: vi.fn().mockResolvedValue({ outcome: 'saved', version: 0 }),
    getCatalog: vi.fn().mockResolvedValue({ specs: [], collections: [], metadataTypes: {}, modelTypes: {} }),
    validate: vi.fn().mockResolvedValue({ errors: [] }),
    ...overrides,
  };
}

/** The values a `<select>` currently offers, in the order it offers them. */
function optionsOf(label: string | RegExp): string[] {
  return [...(screen.getByLabelText(label) as HTMLSelectElement).options].map((option) => option.value);
}

/**
 * Opens the explorer. It is a command palette behind the toolbar now rather than a standing rail,
 * so every test that reaches into it — the tree, the model chips, New/Derive/Override/Delete —
 * opens it first.
 */
async function openExplorer(): Promise<void> {
  await userEvent.click(screen.getByRole('button', { name: 'Open' }));
}

function renderPage(
  client: ReturnType<typeof stubClient> = stubClient(),
  selected: string | null = null,
  document: RuleDocument = { rule: { spec: 'customer.is-active' } },
) {
  const onSelect = vi.fn();
  const store = new RuleEditorStore(document);
  const page = (name: string | null) => (
    <RuleEditorProvider store={store}>
      <PropositionsPage
        client={client as never}
        page="propositions"
        selected={name}
        onSelect={onSelect}
      />
    </RuleEditorProvider>
  );
  const { rerender } = render(page(selected));
  return { onSelect, select: (name: string | null) => rerender(page(name)) };
}

describe('PropositionsPage', () => {
  it('lists propositions in the explorer on mount', async () => {
    const client = stubClient();
    renderPage(client);

    await openExplorer();

    expect(await screen.findByRole('treeitem', { name: /is-active/ })).toBeTruthy();
    expect(client.listPropositions).toHaveBeenCalled();
  });

  it('loads the selected proposition document', async () => {
    const client = stubClient();
    renderPage(client, 'customer.derived');

    await waitFor(() => expect(client.getProposition).toHaveBeenCalledWith('customer.derived'));
  });

  it('shows the selected name as breadcrumb segments', async () => {
    const client = stubClient();
    renderPage(client, 'customer.derived');

    // The dotted name renders as a trail, which is the payoff of namespacing by name.
    // Scoped to the banner: "customer" also appears as a model pill in the explorer, so an
    // unscoped findByText would match several nodes and throw.
    const bar = await screen.findByRole('banner');
    await waitFor(() => expect(bar.querySelector('.breadcrumb-current')?.textContent).toBe('derived'));
    expect([...bar.querySelectorAll('.breadcrumb-item')].map((node) => node.textContent))
      .toContain('customer');
  });

  it('fetches the blast radius for the selection', async () => {
    const client = stubClient({
      getDependents: vi.fn().mockResolvedValue([{ name: 'can-checkout', kind: 'rule' }]),
    });
    renderPage(client, 'customer.derived');

    expect(await screen.findByText(/can-checkout/)).toBeTruthy();
  });

  it('says how many things an edit would affect', async () => {
    const client = stubClient({
      getDependents: vi.fn().mockResolvedValue([
        { name: 'can-checkout', kind: 'rule' },
        { name: 'customer.other', kind: 'proposition' },
      ]),
    });
    renderPage(client, 'customer.derived');

    expect(await screen.findByText(/1 rule and 1 proposition/i)).toBeTruthy();
    // The same count rides on the Save button, so the blast radius is legible from the control
    // that would cause it without having to read the strip. Every other test stubs the dependents
    // empty and matches `/^save$/i`, which is exactly the label this suffix replaces.
    expect(await screen.findByRole('button', { name: 'Save (2)' })).toBeTruthy();
  });

  it('saves the edited document with the loaded version', async () => {
    const client = stubClient();
    renderPage(client, 'customer.derived');
    await waitFor(() => expect(client.getProposition).toHaveBeenCalled());

    await userEvent.click(screen.getByRole('button', { name: /^save$/i }));

    await waitFor(() => expect(client.putProposition)
      .toHaveBeenCalledWith('customer.derived', expect.anything(), 1));
  });

  it('explains why Save is unavailable for a name only served by a compiled spec', async () => {
    const client = stubClient({ getProposition: vi.fn().mockResolvedValue(COMPILED) });
    renderPage(client, 'customer.is-active');

    // Gated on the load becoming *observable* — the badge renders only once `loaded` is truthy —
    // rather than on the button being unavailable at some tick. `waitFor` resolves on the first
    // tick where its condition holds, and `loaded === null` already makes it unavailable at tick
    // zero, so waiting for that alone would pass without the version guard ever being consulted.
    await screen.findByText('v0');

    // Version 0 is the contract's "purely compiled": there is no overlay document for a PUT to
    // update, and `baseVersion` is required to be positive, so saving could only ever fail.
    // Override is the affordance that authors one — and saying so is the point: `aria-disabled`
    // keeps the button reachable, so the reason it carries can actually be read.
    const save = screen.getByRole('button', { name: 'Save' });
    expect(save.getAttribute('aria-disabled')).toBe('true');
    const reason = save.getAttribute('aria-describedby');
    expect(document.getElementById(reason!)?.textContent).toMatch(/compiled/i);
  });

  it('surfaces a thrown listing failure rather than rendering an empty catalog', async () => {
    const client = stubClient({
      listPropositions: vi.fn().mockRejectedValue(new RulesApiError(500, 'listing exploded')),
    });
    renderPage(client);

    expect((await screen.findByRole('alert')).textContent).toContain('listing exploded');
  });

  it('surfaces a thrown load failure instead of leaving the previous name in the breadcrumb', async () => {
    // `describeFailure` covers every *typed* outcome; a 404 or a 500 arrives as a thrown
    // RulesApiError and escapes it entirely. Left unhandled, a deep link to a name that is gone
    // leaves the breadcrumb naming whatever was loaded before — the page then says one thing and
    // the address bar another.
    const client = stubClient({
      getProposition: vi.fn()
        .mockResolvedValueOnce({
          document: { rule: { spec: 'customer.is-active' } }, version: 1,
          origin: 'Authored', hasCompiledDefault: false,
        })
        .mockRejectedValue(new RulesApiError(404, 'No proposition named customer.gone.')),
    });
    const { select } = renderPage(client, 'customer.derived');
    await screen.findByText('v1');

    select('customer.gone');

    expect((await screen.findByRole('alert')).textContent).toContain('No proposition named');
    expect(document.querySelector('.breadcrumb-current')).toBeNull();
  });

  it('surfaces a thrown save failure rather than leaving Save silently dead', async () => {
    // The `finally` re-enables the button and nothing else happens: without this the user clicks
    // Save, watches it flicker, and is told nothing at all.
    const client = stubClient({
      putProposition: vi.fn().mockRejectedValue(new RulesApiError(500, 'save exploded')),
    });
    renderPage(client, 'customer.derived');
    await screen.findByText('v1');

    await userEvent.click(screen.getByRole('button', { name: /^save$/i }));

    expect((await screen.findByRole('alert')).textContent).toContain('save exploded');
    // `aria-disabled`, not `disabled`: the attribute the toolbar actually uses. Asserting the one
    // it never sets would pass no matter what state the failed save left the button in.
    expect(screen.getByRole('button', { name: /^save$/i }).getAttribute('aria-disabled')).toBeNull();
  });

  it('surfaces a thrown delete failure', async () => {
    const client = stubClient({
      deleteProposition: vi.fn().mockRejectedValue(new RulesApiError(500, 'delete exploded')),
    });
    const { onSelect } = renderPage(client, 'customer.derived');
    await screen.findByText('v1');

    await openExplorer();
    await userEvent.click(await screen.findByRole('button', { name: /^delete$/i }));

    expect((await screen.findByRole('alert')).textContent).toContain('delete exploded');
    // A delete that threw removed nothing, so the selection must stand.
    expect(onSelect).not.toHaveBeenCalledWith(null);
  });

  it('surfaces a thrown create failure in the dialog that raised it', async () => {
    const client = stubClient({
      createProposition: vi.fn().mockRejectedValue(new RulesApiError(500, 'create exploded')),
    });
    renderPage(client);
    await openExplorer();
    await screen.findByRole('treeitem', { name: /is-active/ });

    await userEvent.click(screen.getByRole('button', { name: /^new$/i }));
    await userEvent.type(screen.getByLabelText('Name'), 'customer.fresh');
    await userEvent.click(screen.getByRole('button', { name: /create/i }));

    // The dialog stays open with its own error: the form is where the failed input still lives.
    expect((await screen.findByRole('alert')).textContent).toContain('create exploded');
    expect(screen.getByRole('dialog')).toBeTruthy();
  });

  it('does not let a save continuation overwrite a newer selection', async () => {
    // `saving` disables Save but nothing disables the tree, so a click in the explorer lands while
    // the PUT is still in flight. The save's continuation must not then rewrite the breadcrumb and
    // the Save target back to the proposition it was about.
    let settle = (): void => {};
    const client = stubClient({
      putProposition: vi.fn().mockReturnValue(new Promise<{ outcome: string; version: number }>(
        (resolve) => { settle = () => resolve({ outcome: 'saved', version: 9 }); })),
    });
    const { select } = renderPage(client, 'customer.derived');
    await screen.findByText('v1');

    await userEvent.click(screen.getByRole('button', { name: /^save$/i }));
    select('customer.overridden');
    await screen.findByText('v1'); // the second load lands, still version 1 from the stub
    settle();

    await waitFor(() => expect(document.querySelector('.breadcrumb-current')?.textContent)
      .toBe('overridden'));
    // Version 9 belonged to the *other* proposition's save; showing it here would be a lie about
    // what a subsequent Save is going to send.
    expect(screen.queryByText('v9')).toBeNull();
  });

  it('does not let a save continuation raise a banner about a proposition no longer shown', async () => {
    let settle = (): void => {};
    const client = stubClient({
      putProposition: vi.fn().mockReturnValue(new Promise<{ outcome: string; currentVersion: number }>(
        (resolve) => { settle = () => resolve({ outcome: 'conflict', currentVersion: 5 }); })),
    });
    const { select } = renderPage(client, 'customer.derived');
    await screen.findByText('v1');

    await userEvent.click(screen.getByRole('button', { name: /^save$/i }));
    select('customer.overridden');
    settle();

    // A conflict banner is a claim about the proposition that was saved. Raised over a different
    // selection it reads as a claim about that one, and is false.
    await waitFor(() => expect(client.putProposition).toHaveBeenCalled());
    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('does not let a delete continuation navigate away from a newer selection', async () => {
    // Nothing disables the tree while the DELETE is in flight, so the user can move on before it
    // lands. Dropping the selection then would drag them off the proposition they just opened, on
    // behalf of one they are no longer looking at.
    let settle = (): void => {};
    const client = stubClient({
      deleteProposition: vi.fn().mockReturnValue(new Promise<{ outcome: string; version: number }>(
        (resolve) => { settle = () => resolve({ outcome: 'saved', version: 0 }); })),
    });
    const { onSelect, select } = renderPage(client, 'customer.derived');
    await screen.findByText('v1');

    await openExplorer();
    await userEvent.click(await screen.findByRole('button', { name: /^delete$/i }));
    select('customer.overridden');
    settle();

    await waitFor(() => expect(client.deleteProposition).toHaveBeenCalled());
    expect(onSelect).not.toHaveBeenCalled();
  });

  it('surfaces a conflict when the version was stale', async () => {
    const client = stubClient({
      putProposition: vi.fn().mockResolvedValue({ outcome: 'conflict', currentVersion: 5 }),
    });
    renderPage(client, 'customer.derived');
    await waitFor(() => expect(client.getProposition).toHaveBeenCalled());

    await userEvent.click(screen.getByRole('button', { name: /^save$/i }));

    expect(await screen.findByRole('alert')).toBeTruthy();
    expect(screen.getByRole('alert').textContent).toContain('5');
  });

  it('drops the previous selection’s blast radius and failure while the next one loads', async () => {
    const client = stubClient({
      getDependents: vi.fn().mockResolvedValue([{ name: 'can-checkout', kind: 'rule' }]),
      putProposition: vi.fn().mockResolvedValue({ outcome: 'conflict', currentVersion: 5 }),
    });
    const { select } = renderPage(client, 'customer.derived');
    await screen.findByText(/can-checkout/);
    await userEvent.click(screen.getByRole('button', { name: /^save/i }));
    await screen.findByRole('alert');

    // Both the strip and the banner are claims about the proposition that was selected. Carried
    // across a change of selection they would be claims about the new one, and false.
    select('customer.overridden');

    expect(screen.queryByRole('alert')).toBeNull();
    expect(screen.queryByText(/can-checkout/)).toBeNull();
  });

  it('names the rule an edit would break', async () => {
    const client = stubClient({
      putProposition: vi.fn().mockResolvedValue({
        outcome: 'invalid',
        errors: [],
        brokenDependents: [{
          name: 'can-checkout', kind: 'rule',
          errors: [{ path: '$', code: 'AsyncSpecInSyncLoad', message: 'would not bind' }],
        }],
      }),
    });
    renderPage(client, 'customer.derived');
    await waitFor(() => expect(client.getProposition).toHaveBeenCalled());

    await userEvent.click(screen.getByRole('button', { name: /^save$/i }));

    const alert = await screen.findByRole('alert');
    expect(alert.textContent).toContain('can-checkout');
    expect(alert.textContent).toContain('would not bind');
  });

  it('creates a proposition from the new dialog', async () => {
    const client = stubClient();
    // The store is shared with the Rules page, and on a freshly-opened Propositions page it still
    // holds that page's draft. A create that copied it would depend on which page was visited
    // first, so the fixture is deliberately a document no source in the listing could produce.
    renderPage(client, null, { rule: { spec: 'customer.has-orders' } });
    await openExplorer();
    await screen.findByRole('treeitem', { name: /is-active/ });

    await userEvent.click(screen.getByRole('button', { name: /^new$/i }));
    await userEvent.type(screen.getByLabelText('Name'), 'customer.fresh');
    await userEvent.click(screen.getByRole('button', { name: /create/i }));

    // No source picked, so the create starts from the first one offered — not from the draft.
    await waitFor(() => expect(client.createProposition).toHaveBeenCalledWith(expect.objectContaining({
      name: 'customer.fresh',
      modelType: 'customer',
      document: { rule: { spec: 'customer.derived' } },
    })));
  });

  it('starts a new proposition from whichever source is picked', async () => {
    const client = stubClient();
    renderPage(client, null, { rule: { spec: 'customer.has-orders' } });
    await openExplorer();
    await screen.findByRole('treeitem', { name: /is-active/ });

    await userEvent.click(screen.getByRole('button', { name: /^new$/i }));
    await userEvent.type(screen.getByLabelText('Name'), 'customer.fresh');
    await userEvent.selectOptions(screen.getByLabelText(/starts from/i), 'customer.overridden');
    await userEvent.click(screen.getByRole('button', { name: /create/i }));

    await waitFor(() => expect(client.createProposition).toHaveBeenCalledWith(expect.objectContaining({
      document: { rule: { spec: 'customer.overridden' } },
    })));
  });

  it('offers only the sources belonging to the model type in force', async () => {
    const client = stubClient({
      listPropositions: vi.fn().mockResolvedValue([
        entry({ name: 'customer.derived' }),
        entry({ name: 'customer.overridden', origin: 'Overridden', version: 2 }),
        entry({ name: 'order.is-paid', modelType: 'order' }),
      ]),
    });
    renderPage(client);
    await openExplorer();
    await screen.findByRole('treeitem', { name: /derived/ });

    await userEvent.click(screen.getByRole('button', { name: /^new$/i }));
    await userEvent.type(screen.getByLabelText('Name'), 'order.fresh');
    expect(optionsOf(/starts from/i)).toEqual(['customer.derived', 'customer.overridden']);

    // Deliberately a source that is neither the default nor available under the model chosen next:
    // the picker has to follow the model select, and a choice of another model would not bind, so
    // it is replaced rather than left standing.
    await userEvent.selectOptions(screen.getByLabelText(/starts from/i), 'customer.overridden');
    await userEvent.selectOptions(screen.getByLabelText('Model type'), 'order');

    expect(optionsOf(/starts from/i)).toEqual(['order.is-paid']);
    await userEvent.click(screen.getByRole('button', { name: /create/i }));
    await waitFor(() => expect(client.createProposition).toHaveBeenCalledWith(expect.objectContaining({
      modelType: 'order',
      document: { rule: { spec: 'order.is-paid' } },
    })));
  });

  it('says why it cannot create when there is nothing to start from', async () => {
    const client = stubClient({ listPropositions: vi.fn().mockResolvedValue([]) });
    renderPage(client);
    await waitFor(() => expect(client.listPropositions).toHaveBeenCalled());

    await openExplorer();
    await userEvent.click(screen.getByRole('button', { name: /^new$/i }));
    await userEvent.type(screen.getByLabelText('Name'), 'customer.fresh');

    // A UI-authored proposition is composition-only, so with nothing registered to compose over
    // there is nothing to create. The button being dead is not an explanation, so it is said.
    // `aria-disabled`, not `disabled`: the latter drops the button from the tab order in every
    // major browser, leaving a keyboard screen-reader user unable to reach it and so unable to
    // hear the `aria-describedby` explanation this test also checks for.
    const create = screen.getByRole('button', { name: /create/i });
    expect(create.getAttribute('aria-disabled')).toBe('true');
    expect(create.hasAttribute('disabled')).toBe(false);
    expect(screen.getByText(/nothing to start from/i)).toBeTruthy();
  });

  it('does not create when the Create button is aria-disabled', async () => {
    const client = stubClient({ listPropositions: vi.fn().mockResolvedValue([]) });
    renderPage(client);
    await waitFor(() => expect(client.listPropositions).toHaveBeenCalled());

    await openExplorer();
    await userEvent.click(screen.getByRole('button', { name: /^new$/i }));
    await userEvent.type(screen.getByLabelText('Name'), 'customer.fresh');

    // `aria-disabled` alone does not stop a click from reaching the handler — the guard has to be
    // in the handler itself, exactly like `Toolbar`'s early return.
    await userEvent.click(screen.getByRole('button', { name: /create/i }));

    expect(client.createProposition).not.toHaveBeenCalled();
  });

  it('seeds the dialog from the derived-from node', async () => {
    const client = stubClient();
    // Not the first source alphabetically, so preselecting it cannot be confused with simply
    // falling back to the head of the list.
    renderPage(client, 'customer.overridden');
    await waitFor(() => expect(client.getProposition).toHaveBeenCalled());

    await openExplorer();
    await userEvent.click(await screen.findByRole('button', { name: /derive/i }));

    // Prefilled to the source's namespace, so derivation lands beside what it came from
    expect((screen.getByLabelText('Name') as HTMLInputElement).value).toBe('customer.');
    // …and the source it derives from is the one already picked out for it.
    expect((screen.getByLabelText(/starts from/i) as HTMLSelectElement).value)
      .toBe('customer.overridden');
  });

  it('creates a derived proposition whose document references its source', async () => {
    const client = stubClient();
    renderPage(client, 'customer.overridden');
    await waitFor(() => expect(client.getProposition).toHaveBeenCalled());

    await openExplorer();
    await userEvent.click(await screen.findByRole('button', { name: /derive/i }));
    await userEvent.type(screen.getByLabelText('Name'), 'onward');
    await userEvent.click(screen.getByRole('button', { name: /create/i }));

    await waitFor(() => expect(client.createProposition).toHaveBeenCalledWith(expect.objectContaining({
      name: 'customer.onward',
      document: { rule: { spec: 'customer.overridden' } },
    })));
  });

  it('seeds the dialog with the compiled name when overriding', async () => {
    const client = stubClient({ getProposition: vi.fn().mockResolvedValue(COMPILED) });
    renderPage(client, 'customer.is-active');
    await waitFor(() => expect(client.getProposition).toHaveBeenCalled());

    await openExplorer();
    await userEvent.click(await screen.findByRole('button', { name: /^override$/i }));

    // An override *takes* the compiled spec's name — it does not derive a new one from it.
    expect((screen.getByLabelText('Name') as HTMLInputElement).value).toBe('customer.is-active');
    expect(screen.getByRole('dialog').getAttribute('aria-label')).toContain('customer.is-active');
  });

  it('never offers the name being overridden as its own source', async () => {
    const client = stubClient({ getProposition: vi.fn().mockResolvedValue(COMPILED) });
    renderPage(client, 'customer.is-active');
    await waitFor(() => expect(client.getProposition).toHaveBeenCalled());

    await openExplorer();
    await userEvent.click(await screen.findByRole('button', { name: /^override$/i }));

    // An override is authored under the very name it overrides, so a reference back to that name
    // is a cycle straight onto itself. The name being defined is excluded, whatever it is.
    expect(optionsOf(/starts from/i)).toEqual(['customer.derived', 'customer.overridden']);
  });

  it('creates the override as a reference to its chosen source', async () => {
    const client = stubClient({ getProposition: vi.fn().mockResolvedValue(COMPILED) });
    // Deliberately *not* something the picker can produce: the create must come from the chosen
    // source, not from whatever the shared editor draft happens to hold.
    renderPage(client, 'customer.is-active', { rule: { spec: 'customer.has-orders' } });
    await waitFor(() => expect(client.getProposition).toHaveBeenCalled());

    await openExplorer();
    await userEvent.click(await screen.findByRole('button', { name: /^override$/i }));
    await userEvent.selectOptions(screen.getByLabelText(/starts from/i), 'customer.overridden');
    await userEvent.click(screen.getByRole('button', { name: /create/i }));

    await waitFor(() => expect(client.createProposition).toHaveBeenCalledWith(expect.objectContaining({
      name: 'customer.is-active',
      document: { rule: { spec: 'customer.overridden' } },
    })));
  });

  it('keeps the whole form off the dialog element itself, where a click would dismiss it', async () => {
    // `Modal` reports a click whose target *is* the dialog as a backdrop click — the backdrop
    // belongs to the element, so there is no other node to compare against. That is only sound
    // while the element has no self-area to click on, and this form's 16px frame and 12px row gaps
    // used to be exactly that: padding box and flex gaps hit-test as the element, so a click on the
    // frame or in any band between rows dismissed the dialog and destroyed everything typed.
    //
    // The rhythm lives on an inner wrapper now, which covers the element edge to edge — true only
    // while that wrapper is the dialog's one in-flow child. jsdom computes no layout, so the
    // *consequence* is unprovable here and `propositions.spec.ts › a click inside the authoring
    // dialog keeps what was typed` measures it in a browser. The *cause* is this one structure.
    const client = stubClient();
    renderPage(client);
    await openExplorer();
    await screen.findByRole('treeitem', { name: /is-active/ });
    await userEvent.click(screen.getByRole('button', { name: /^new$/i }));

    const dialog = screen.getByRole('dialog');
    // Close floats over the corner rather than sitting in the flow, so it is not part of the count.
    const close = screen.getByRole('button', { name: /close/i });
    const inFlow = [...dialog.children].filter((child) => child !== close);

    expect(inFlow.map((child) => child.className)).toEqual(['dialog-form']);
    // …and the wrapper is the form, not an empty box the fields sit outside of.
    expect(inFlow[0]!.contains(screen.getByLabelText('Name'))).toBe(true);
    expect(inFlow[0]!.contains(screen.getByRole('button', { name: /create/i }))).toBe(true);
  });

  it('does not offer a quarantined authored proposition as a source', async () => {
    // A quarantined *authored* proposition is not in the effective set at all, so a reference to
    // it would not resolve. A quarantined *override* is different: the compiled default still
    // serves the name, so referencing it does resolve — which is why the filter turns on origin
    // rather than on quarantine alone.
    const client = stubClient({
      listPropositions: vi.fn().mockResolvedValue([
        entry({ name: 'customer.sound' }),
        entry({
          name: 'customer.broken',
          quarantine: [{ path: '$', code: 'UnknownSpec', message: 'unknown spec' }],
        }),
        entry({
          name: 'customer.shadowed', origin: 'Overridden', version: 2,
          quarantine: [{ path: '$', code: 'UnknownSpec', message: 'unknown spec' }],
        }),
      ]),
    });
    renderPage(client);
    await openExplorer();
    await screen.findByRole('treeitem', { name: /sound/ });

    await userEvent.click(screen.getByRole('button', { name: /^new$/i }));

    expect(optionsOf(/starts from/i)).toEqual(['customer.shadowed', 'customer.sound']);
  });

  it('reports a name already taken', async () => {
    const client = stubClient({
      createProposition: vi.fn().mockResolvedValue({ outcome: 'nameTaken' }),
    });
    renderPage(client);
    await openExplorer();
    await screen.findByRole('treeitem', { name: /is-active/ });

    await userEvent.click(screen.getByRole('button', { name: /^new$/i }));
    await userEvent.type(screen.getByLabelText('Name'), 'customer.derived');
    await userEvent.click(screen.getByRole('button', { name: /create/i }));

    expect(await screen.findByText(/already/i)).toBeTruthy();
  });

  it('reports the referrers blocking a delete', async () => {
    const client = stubClient({
      deleteProposition: vi.fn().mockResolvedValue({ outcome: 'referenced', referrers: ['customer.other'] }),
    });
    renderPage(client, 'customer.derived');
    await waitFor(() => expect(client.getProposition).toHaveBeenCalled());

    await openExplorer();
    await userEvent.click(await screen.findByRole('button', { name: /^delete$/i }));

    expect((await screen.findByRole('alert')).textContent).toContain('customer.other');
  });

  it('drops the selection when a delete removed the proposition outright', async () => {
    const client = stubClient();
    const { onSelect } = renderPage(client, 'customer.derived');
    await waitFor(() => expect(client.getProposition).toHaveBeenCalled());

    await openExplorer();
    await userEvent.click(await screen.findByRole('button', { name: /^delete$/i }));

    // DELETE answers the same `{ version: 0 }` either way, so the authored-vs-overridden
    // distinction has to be read off the entry before the call, not inferred from the response.
    await waitFor(() => expect(onSelect).toHaveBeenCalledWith(null));
  });

  it('reloads the now-compiled proposition when a delete reverted an override', async () => {
    const client = stubClient();
    const { onSelect } = renderPage(client, 'customer.overridden');
    await waitFor(() => expect(client.getProposition).toHaveBeenCalledTimes(1));

    await openExplorer();
    await userEvent.click(await screen.findByRole('button', { name: /revert/i }));

    // The name survives a revert — it is served by the compiled spec now — so it stays selected,
    // and the document behind it has changed, so it is fetched again.
    await waitFor(() => expect(client.getProposition).toHaveBeenCalledTimes(2));
    expect(onSelect).toHaveBeenCalledWith('customer.overridden');
    expect(onSelect).not.toHaveBeenCalledWith(null);
  });

  it('opens the explorer from the toolbar', async () => {
    renderPage();

    expect(screen.queryByRole('dialog', { name: 'Propositions' })).toBeNull();
    await openExplorer();

    expect(screen.getByRole('dialog', { name: 'Propositions' })).toBeTruthy();
  });

  it('opens the explorer with ⌘K', async () => {
    renderPage();

    await userEvent.keyboard('{Meta>}k{/Meta}');

    expect(screen.getByRole('dialog', { name: 'Propositions' })).toBeTruthy();
  });

  it('leaves ⌘K inert while the authoring dialog is open, rather than stacking a palette over it', async () => {
    // The shortcut is bound on `window` so it works wherever focus is, and a keydown inside a
    // modal <dialog> still bubbles there — so without a guard ⌘K mounts a second dialog on top of
    // the first. `openDialog` already refuses to do that in the other direction, and this is the
    // same rule from the other side: choosing a row in the stacked palette navigates the page
    // underneath, discarding a form the user is part-way through filling in.
    renderPage();
    await openExplorer();
    await userEvent.click(screen.getByRole('button', { name: 'New' }));
    await screen.findByRole('dialog', { name: 'New proposition' });

    await userEvent.keyboard('{Meta>}k{/Meta}');

    expect(screen.queryByRole('dialog', { name: 'Propositions' })).toBeNull();
    expect(screen.getByRole('dialog', { name: 'New proposition' })).toBeTruthy();
  });

  it('claims ⌘K from the browser rather than letting both fire', () => {
    // jsdom binds nothing to ⌘K, so the browser default this exists to suppress cannot be
    // observed here. What can is the thing a real browser actually reads — that the handler marked
    // the event default-prevented — and `fireEvent` returns false exactly when it did.
    renderPage();

    const notPrevented = fireEvent.keyDown(window, { key: 'k', metaKey: true });

    expect(notPrevented).toBe(false);
  });

  it('opens the explorer fresh, discarding the previous query', async () => {
    // The palette unmounts on close, so this only holds while the query lives inside it. Move
    // that state up to the page and this breaks.
    renderPage();
    await openExplorer();
    await userEvent.type(screen.getByRole('combobox'), 'derived');
    await userEvent.click(screen.getByRole('button', { name: /close/i }));

    await openExplorer();

    expect(screen.getByRole('combobox')).toHaveProperty('value', '');
  });

  it('closes the explorer once a proposition is chosen', async () => {
    const { onSelect } = renderPage();
    await openExplorer();

    await userEvent.type(screen.getByRole('combobox'), 'derived');
    await userEvent.keyboard('{Enter}');

    expect(onSelect).toHaveBeenCalledWith('customer.derived');
    expect(screen.queryByRole('dialog', { name: 'Propositions' })).toBeNull();
  });

  it('opens the document viewer from the toolbar', async () => {
    renderPage();

    await userEvent.click(screen.getByRole('button', { name: 'JSON' }));

    expect(screen.getByRole('dialog', { name: /document/i })).toBeTruthy();
  });

  it('refreshes the listing after a successful create', async () => {
    const client = stubClient();
    renderPage(client);
    await openExplorer();
    await screen.findByRole('treeitem', { name: /is-active/ });
    const before = client.listPropositions.mock.calls.length;

    await userEvent.click(screen.getByRole('button', { name: /^new$/i }));
    await userEvent.type(screen.getByLabelText('Name'), 'customer.fresh');
    await userEvent.click(screen.getByRole('button', { name: /create/i }));

    await waitFor(() =>
      expect(client.listPropositions.mock.calls.length).toBeGreaterThan(before));
  });
});
