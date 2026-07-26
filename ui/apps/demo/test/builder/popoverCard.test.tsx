import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';
import { RuleEditorStore, type RulesApiClient } from '@motiv/rules-core';
import { RuleEditorProvider } from '@motiv/rules-react';
import { BuilderPane } from '../../src/panes/BuilderPane.js';

const catalog = {
  specs: [
    { name: 'is-active', modelType: 'customer', metadataType: 'String', isAsync: false, description: null },
    { name: 'is-adult', modelType: 'customer', metadataType: 'String', isAsync: false, description: null },
  ],
  collections: [],
};
const client = () => ({ getCatalog: vi.fn().mockResolvedValue(catalog) }) as unknown as RulesApiClient;
const AND = { rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }] } };

/**
 * jsdom has no layout, so every box measures as zero and a card would always be placed at the
 * viewport's origin. Anchoring the row at a row that *moves* is what makes the placement
 * observable at all.
 */
const original = Element.prototype.getBoundingClientRect;
let anchorTop = 200;
const stubLayout = (): void => {
  Element.prototype.getBoundingClientRect = function measured(this: Element) {
    const isTrigger = this instanceof HTMLElement && this.getAttribute('role') === 'combobox';
    const top = isTrigger ? anchorTop : 0;
    return { top, bottom: top + 20, left: 40, right: 140, width: 100, height: 20, x: 40, y: top, toJSON: () => ({}) };
  };
};

afterEach(() => { Element.prototype.getBoundingClientRect = original; });

describe('row popovers', () => {
  it('follows its row when the pane beneath it scrolls', async () => {
    stubLayout();
    render(<RuleEditorProvider store={new RuleEditorStore(AND)}><BuilderPane client={client()} /></RuleEditorProvider>);
    fireEvent.click(await screen.findByRole('combobox', { name: /^operator at/ }));

    const placedAt = () => (screen.getByRole('listbox') as HTMLElement).style.top;
    const before = placedAt();
    expect(before).toBe('226px'); // the anchor's bottom, plus the gap

    // The card is fixed to the viewport, so a scroll that moves its row has to move it too —
    // otherwise it hangs in place pointing at whatever slid underneath.
    anchorTop = 60;
    act(() => { window.dispatchEvent(new Event('scroll')); });
    expect(placedAt()).toBe('86px');

    anchorTop = 200;
    act(() => { window.dispatchEvent(new Event('resize')); });
    expect(placedAt()).toBe(before);
  });
});
