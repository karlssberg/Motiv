import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { RuleEditorStore, type RulesApiClient } from '@motiv/rules-core';
import { RuleEditorProvider } from '@motiv/rules-react';
import { BuilderPane } from '../../src/panes/BuilderPane.js';

const catalog = {
  specs: [{ name: 'is-active', modelType: 'customer', metadataType: 'String', isAsync: false, description: null }],
  collections: [],
};
const client = () => ({ getCatalog: vi.fn().mockResolvedValue(catalog) }) as unknown as RulesApiClient;
const renderWith = (store: RuleEditorStore) =>
  render(<RuleEditorProvider store={store}><BuilderPane client={client()} /></RuleEditorProvider>);

describe('close-all strip', () => {
  const doc = { rule: { and: [{ spec: 'is-active' }, { spec: 'is-active' }] } };

  it('stays hidden while nothing is pinned', async () => {
    renderWith(new RuleEditorStore(doc));
    await screen.findByRole('button', { name: 'details for $.rule' });
    expect(screen.queryByRole('button', { name: 'close all' })).toBeNull();
  });

  it('appears once a node is pinned, and counts the pins', async () => {
    renderWith(new RuleEditorStore(doc));
    fireEvent.click(await screen.findByRole('button', { name: 'pin $.rule.and[0]' }));
    expect(screen.getByRole('button', { name: 'close all' })).toBeDefined();
    expect(screen.getByText('1 pinned')).toBeDefined();
    fireEvent.click(screen.getByRole('button', { name: 'pin $.rule.and[1]' }));
    expect(screen.getByText('2 pinned')).toBeDefined();
  });

  it('close all clears every pin and the open panel', async () => {
    renderWith(new RuleEditorStore(doc));
    fireEvent.click(await screen.findByRole('button', { name: 'pin $.rule.and[0]' }));
    fireEvent.click(screen.getByRole('button', { name: 'details for $.rule.and[1]' }));
    fireEvent.click(screen.getByRole('button', { name: 'close all' }));
    expect(screen.queryByLabelText('name at $.rule.and[0]')).toBeNull();
    expect(screen.queryByLabelText('name at $.rule.and[1]')).toBeNull();
    expect(screen.queryByRole('button', { name: 'close all' })).toBeNull();
  });
});
