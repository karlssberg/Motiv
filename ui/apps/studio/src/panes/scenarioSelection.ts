import { useSyncExternalStore } from 'react';

export interface SelectedScenario { name: string; model: string }

const selected = new Map<string, SelectedScenario>();
const listeners = new Set<() => void>();

/** The scenario whose details are open in the table, per rule — what the inspector reads a leaf against. */
export function selectScenario(ruleName: string, scenario: SelectedScenario | null): void {
  if (scenario) selected.set(ruleName, scenario); else selected.delete(ruleName);
  for (const listener of listeners) listener();
}

export function useSelectedScenario(ruleName: string): SelectedScenario | null {
  return useSyncExternalStore(
    (listener) => { listeners.add(listener); return () => listeners.delete(listener); },
    () => selected.get(ruleName) ?? null,
  );
}
