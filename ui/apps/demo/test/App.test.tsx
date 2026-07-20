import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { RuleEditorStore, RulesApiClient } from '@motiv/rules-core';
import { App } from '../src/App.js';

function testClient(): RulesApiClient {
  return {
    getCatalog: vi.fn().mockResolvedValue([]),
    validate: vi.fn().mockResolvedValue({ errors: [] }),
    evaluate: vi.fn(),
  } as unknown as RulesApiClient;
}

describe('App', () => {
  it('renders the three panes', () => {
    render(<App client={testClient()} store={new RuleEditorStore({ rule: { spec: 'is-active' } })} />);
    expect(screen.getByRole('region', { name: 'Builder' })).toBeDefined();
    expect(screen.getByRole('region', { name: 'Document' })).toBeDefined();
    expect(screen.getByRole('region', { name: 'Evaluate' })).toBeDefined();
  });
});
