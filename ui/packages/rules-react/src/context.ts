import { createContext, createElement, useContext, type ReactNode } from 'react';
import type { RuleEditorStore } from '@motiv/rules-core';

const RuleEditorContext = createContext<RuleEditorStore | null>(null);

/** Provides a {@link RuleEditorStore} to descendant hooks and components. */
export function RuleEditorProvider(props: { store: RuleEditorStore; children: ReactNode }): ReactNode {
  return createElement(RuleEditorContext.Provider, { value: props.store }, props.children);
}

/** Returns the store from the nearest provider; throws when used outside one. */
export function useRuleEditorStore(): RuleEditorStore {
  const store = useContext(RuleEditorContext);
  if (!store) throw new Error('useRuleEditorStore must be used within a <RuleEditorProvider>.');
  return store;
}
