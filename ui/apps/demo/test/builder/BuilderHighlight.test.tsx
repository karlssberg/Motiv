import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { RuleEditorStore, type RulesApiClient } from '@motiv/rules-core';
import { RuleEditorProvider } from '@motiv/rules-react';
import { BuilderPane } from '../../src/panes/BuilderPane.js';

const catalog = { specs: [], collections: [] };
const client = () => ({ getCatalog: vi.fn().mockResolvedValue(catalog) }) as unknown as RulesApiClient;
const renderWith = (store: RuleEditorStore) =>
  render(<RuleEditorProvider store={store}><BuilderPane client={client()} /></RuleEditorProvider>);

const twoOperands = () => new RuleEditorStore({ rule: { and: [{ spec: 'a' }, { spec: 'b' }] } });
/** The `.node-row` owning a given path, found via the row's own DSL button. */
const rowFor = async (path: string): Promise<HTMLElement> => {
  const dsl = await screen.findByRole('button', { name: `edit expression at ${path}` });
  return dsl.closest('.node-row') as HTMLElement;
};
/** The strip text carrying a given mark, rejoined — a mark can span several segments. */
const marked = (container: HTMLElement, selector: string): string =>
  [...container.querySelectorAll(selector)].map((el) => el.textContent).join('');

// Hover is fired as mouseOver/mouseOut, not mouseEnter/mouseLeave: React synthesises the
// enter/leave pair from the bubbling events it delegates at the root, so dispatching the
// non-bubbling native events directly would leave the handlers under test uncalled.

describe('builder highlight wiring', () => {
  it('renders the DSL strip for the whole rule', async () => {
    renderWith(twoOperands());
    await rowFor('$.rule.and[0]');
    expect(screen.getByLabelText('rule expression').textContent).toBe('a & b');
  });

  it('marks the hovered row span in the strip', async () => {
    const { container } = renderWith(twoOperands());

    fireEvent.mouseOver(await rowFor('$.rule.and[1]'));

    expect(marked(container, '.dsl-strip-hover')).toBe('b');
  });

  it('clears the hover mark on leaving the row', async () => {
    const { container } = renderWith(twoOperands());
    const row = await rowFor('$.rule.and[1]');

    fireEvent.mouseOver(row);
    fireEvent.mouseOut(row);

    expect(container.querySelectorAll('.dsl-strip-hover')).toHaveLength(0);
  });

  it('selecting a row underlines its span and marks the row', async () => {
    const { container } = renderWith(twoOperands());

    fireEvent.click(await screen.findByRole('button', { name: 'select $.rule.and[0]' }));

    expect(marked(container, '.dsl-strip-selected')).toBe('a');
    expect(container.querySelector('.node-row.selected')).not.toBeNull();
  });

  it('keeps the selection mark while hovering a different row', async () => {
    const { container } = renderWith(twoOperands());

    fireEvent.click(await screen.findByRole('button', { name: 'select $.rule.and[0]' }));
    fireEvent.mouseOver(await rowFor('$.rule.and[1]'));

    expect(marked(container, '.dsl-strip-selected')).toBe('a');
    expect(marked(container, '.dsl-strip-hover')).toBe('b');
  });
});
