import { describe, it, expect } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { selectScenario, useSelectedScenario } from '../../src/panes/scenarioSelection.js';

describe('scenarioSelection', () => {
  it('publishes the open scenario per rule', () => {
    const { result } = renderHook(() => useSelectedScenario('can-checkout'));
    expect(result.current).toBeNull();
    act(() => selectScenario('can-checkout', { name: 'VIP', model: '{"age":40}' }));
    expect(result.current).toEqual({ name: 'VIP', model: '{"age":40}' });
    act(() => selectScenario('other', { name: 'x', model: '{}' }));
    expect(result.current).toEqual({ name: 'VIP', model: '{"age":40}' });
    act(() => selectScenario('can-checkout', null));
    expect(result.current).toBeNull();
  });
});
