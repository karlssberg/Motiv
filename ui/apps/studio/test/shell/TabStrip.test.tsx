import { describe, it, expect, vi, afterEach } from 'vitest';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { TabStrip } from '../../src/shell/TabStrip.js';
import { Workspace, tabIdOf } from '../../src/shell/workspace.js';

/**
 * jsdom lays nothing out, so the strip's measured width is stubbed: `stripWidth` decides how many
 * chips fit (176px each, after 64px for the New tab and Open buttons and 64px for the "+N" menu),
 * and `innerWidth` decides compact or not.
 */
let stripWidth = 800;
Object.defineProperty(HTMLElement.prototype, 'clientWidth', { configurable: true, get: () => stripWidth });

function renderStrip(tabs: Array<['rule' | 'proposition', string]>, width = 1200, fits = 800) {
  window.innerWidth = width;
  stripWidth = fits;
  const workspace = new Workspace();
  workspace.setListings(
    [{ name: 'can-checkout', modelType: 'customer', metadataType: 'String', isAsync: true, isPolicy: false, version: 4, description: null }],
    [{ name: 'customer.is-active', modelType: 'customer', metadataType: 'String', isAsync: false, origin: 'Overridden', version: 2, description: null, quarantine: [] }],
  );
  for (const [kind, name] of tabs) workspace.open(kind, name);
  const handlers = { onActivate: vi.fn(), onRequestClose: vi.fn(), onNewTab: vi.fn(), onOpen: vi.fn() };
  render(<TabStrip workspace={workspace} {...handlers} />);
  return { workspace, ...handlers };
}

afterEach(() => { window.innerWidth = 1024; });

describe('TabStrip', () => {
  it('draws one chip per open tab, in a tablist, with its kind said', () => {
    renderStrip([['rule', 'can-checkout'], ['proposition', 'customer.is-active']]);
    const list = screen.getByRole('tablist', { name: 'Open documents' });
    const tabs = within(list).getAllByRole('tab');
    expect(tabs).toHaveLength(2);
    expect(tabs[0]!.textContent).toContain('can-checkout');
    expect(tabs[0]!.getAttribute('aria-label')).toBe('Rule can-checkout');
    expect(tabs[1]!.getAttribute('aria-label')).toBe('Proposition customer.is-active');
  });

  it('marks the active tab selected and gives it the one tab stop', () => {
    const { workspace } = renderStrip([['rule', 'a'], ['rule', 'b']]);
    act(() => workspace.activate(tabIdOf('rule', 'a')));
    const [a, b] = screen.getAllByRole('tab');
    expect(a!.getAttribute('aria-selected')).toBe('true');
    expect(a!.tabIndex).toBe(0);
    expect(b!.getAttribute('aria-selected')).toBe('false');
    expect(b!.tabIndex).toBe(-1);
  });

  it('activates on click, and closes from the ×, middle-click and Delete', async () => {
    const { onActivate, onRequestClose } = renderStrip([['rule', 'a'], ['rule', 'b']]);
    const [a, b] = screen.getAllByRole('tab');
    await userEvent.click(b!);
    expect(onActivate).toHaveBeenLastCalledWith(tabIdOf('rule', 'b'));

    await userEvent.click(screen.getByLabelText('Close a'));
    expect(onRequestClose).toHaveBeenLastCalledWith(tabIdOf('rule', 'a'));
    // The × never activates the tab it closes.
    expect(onActivate).toHaveBeenCalledTimes(1);

    fireEvent(b!, new MouseEvent('auxclick', { button: 1, bubbles: true }));
    expect(onRequestClose).toHaveBeenLastCalledWith(tabIdOf('rule', 'b'));

    a!.focus();
    await userEvent.keyboard('{Delete}');
    expect(onRequestClose).toHaveBeenLastCalledWith(tabIdOf('rule', 'a'));
  });

  it('moves between tabs on the arrow keys', async () => {
    const { onActivate } = renderStrip([['rule', 'a'], ['rule', 'b']]);
    screen.getAllByRole('tab')[0]!.focus();
    await userEvent.keyboard('{ArrowRight}');
    expect(onActivate).toHaveBeenLastCalledWith(tabIdOf('rule', 'b'));
    await userEvent.keyboard('{ArrowRight}');
    expect(onActivate).toHaveBeenLastCalledWith(tabIdOf('rule', 'a'));
  });

  it('says a tab has unsaved changes, in the × label and a marker', () => {
    const { workspace } = renderStrip([['rule', 'a']]);
    act(() => workspace.getState().docs[tabIdOf('rule', 'a')]!.store.replaceNode('$.rule', { spec: 'x' }));
    expect(screen.getByLabelText('Close a (unsaved changes)')).toBeTruthy();
    expect(screen.getByRole('tab').parentElement!.className).toContain('dirty');
  });

  it('shows the full name and its metadata on focus, and names the card for the tab', async () => {
    renderStrip([['rule', 'can-checkout']]);
    const tab = screen.getByRole('tab');
    tab.focus();
    const card = await screen.findByRole('tooltip', undefined, { timeout: 1500 });
    expect(tab.getAttribute('aria-describedby')).toBe(card.id);
    expect(card.textContent).toContain('can-checkout');
    expect(card.textContent).toContain('customer');
    expect(card.textContent).toContain('v4');
    expect(card.textContent).toContain('async');
    expect(card.textContent).toContain('No unsaved changes');
    fireEvent.blur(tab);
    expect(screen.queryByRole('tooltip')).toBeNull();
  });

  it('keeps the card while the pointer moves onto it, and drops it when the pointer leaves the card (WCAG 1.4.13)', async () => {
    renderStrip([['rule', 'can-checkout']]);
    const tab = screen.getByRole('tab');
    fireEvent.mouseEnter(tab);
    const card = await screen.findByRole('tooltip', undefined, { timeout: 1500 });
    // Hoverable: leaving the tab *for the card* is not leaving.
    fireEvent.mouseLeave(tab, { relatedTarget: card });
    expect(screen.queryByRole('tooltip')).not.toBeNull();
    fireEvent.mouseLeave(card, { relatedTarget: document.body });
    expect(screen.queryByRole('tooltip')).toBeNull();
  });

  it('reserves room for the two strip buttons before counting chips', () => {
    // 416px: two chips would fit beside the "+N" menu alone, but not beside it *and* the New tab
    // and Open buttons that are always drawn — so only one chip may show, or the strip overflows.
    renderStrip([['rule', 'a'], ['rule', 'b'], ['rule', 'c']], 1200, 416);
    expect(screen.getAllByRole('tab')).toHaveLength(1);
    expect(screen.getByRole('button', { name: '+2 more open documents' })).toBeTruthy();
  });

  it('spills the tabs that do not fit into a menu, keeping the active one visible', async () => {
    // Room for one chip, the two strip buttons and the menu.
    const { workspace, onActivate } = renderStrip([['rule', 'a'], ['rule', 'b'], ['rule', 'c']], 1200, 304);
    act(() => workspace.activate(tabIdOf('rule', 'a')));
    const names = () => screen.getAllByRole('tab').map((tab) => tab.getAttribute('aria-label'));
    expect(names()).toEqual(['Rule a']);
    const more = screen.getByRole('button', { name: '+2 more open documents' });
    await userEvent.click(more);
    const menu = screen.getByRole('menu', { name: 'More open documents' });
    expect(within(menu).getAllByRole('menuitem').map((item) => item.textContent)).toEqual(['Rb', 'Rc']);

    await userEvent.click(within(menu).getByRole('menuitem', { name: /c/ }));
    expect(onActivate).toHaveBeenLastCalledWith(tabIdOf('rule', 'c'));
    act(() => workspace.activate(tabIdOf('rule', 'c')));
    expect(names()).toEqual(['Rule c']);
  });

  it('offers + for a new, empty tab, and Open for the palette', async () => {
    const { onNewTab, onOpen } = renderStrip([['rule', 'a']]);
    await userEvent.click(screen.getByRole('button', { name: 'New tab' }));
    expect(onNewTab).toHaveBeenCalledTimes(1);
    await userEvent.click(screen.getByRole('button', { name: 'Open' }));
    expect(onOpen).toHaveBeenCalledTimes(1);
  });

  it('draws an empty tab as the catalog, closable, with no card to show', async () => {
    const { workspace } = renderStrip([['rule', 'a']]);
    act(() => { workspace.newTab(); });
    const empty = screen.getByRole('tab', { name: 'Catalog' });
    expect(empty.getAttribute('aria-selected')).toBe('true');
    expect(screen.getByLabelText('Close empty tab')).toBeTruthy();
    // No document, nothing to describe: focusing it must not reference a card that never mounts.
    act(() => empty.focus());
    await act(() => new Promise((resolve) => setTimeout(resolve, 400)));
    expect(empty.getAttribute('aria-describedby')).toBeNull();
    expect(screen.queryByRole('tooltip')).toBeNull();
  });

  it('lists an empty tab in the narrow dropdown, and offers a new tab from it', async () => {
    const { workspace, onNewTab } = renderStrip([['rule', 'a']], 500);
    act(() => { workspace.newTab(); });
    const button = screen.getByRole('button', { name: /^Open documents/ });
    expect(button.getAttribute('aria-label')).toContain('Catalog');
    await userEvent.click(button);
    const menu = screen.getByRole('menu', { name: 'Open documents' });
    expect(within(menu).getByRole('menuitem', { name: 'Catalog' })).toBeTruthy();
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'New tab' }));
    expect(onNewTab).toHaveBeenCalledTimes(1);
    // The palette stays one click away beside the dropdown: a phone never sees the chips.
    expect(screen.getByRole('button', { name: 'Open' })).toBeTruthy();
  });

  it('becomes a dropdown on a narrow window, listing every tab with a close each', async () => {
    const { workspace, onActivate, onRequestClose } = renderStrip([['rule', 'a'], ['rule', 'b']], 500);
    act(() => workspace.activate(tabIdOf('rule', 'a')));
    expect(screen.queryByRole('tablist')).toBeNull();
    const button = screen.getByRole('button', { name: /^Open documents/ });
    expect(button.textContent).toContain('a');
    expect(button.textContent).toContain('2');

    await userEvent.click(button);
    const menu = screen.getByRole('menu', { name: 'Open documents' });
    await userEvent.click(within(menu).getByRole('menuitem', { name: 'b' }));
    expect(onActivate).toHaveBeenLastCalledWith(tabIdOf('rule', 'b'));

    await userEvent.click(button);
    await userEvent.click(within(screen.getByRole('menu')).getByLabelText('Close b'));
    expect(onRequestClose).toHaveBeenLastCalledWith(tabIdOf('rule', 'b'));
  });

  it('closes its menu on Escape', async () => {
    renderStrip([['rule', 'a'], ['rule', 'b']], 1200, 240);
    await userEvent.click(screen.getByRole('button', { name: /more open documents/ }));
    expect(screen.getByRole('menu')).toBeTruthy();
    await userEvent.keyboard('{Escape}');
    expect(screen.queryByRole('menu')).toBeNull();
  });
});
