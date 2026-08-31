import { inject, provide, type InjectionKey } from 'vue';
import type { RuleEditorStore } from '@motiv-rules/core';

/**
 * The injection key is module-private: the store is reached through the two functions below, so
 * a consumer cannot inject it under a key of their own and drift from what the composables read.
 */
const RULE_EDITOR_STORE = Symbol('motiv.ruleEditorStore') as InjectionKey<RuleEditorStore>;

/** Provides a {@link RuleEditorStore} to descendant components. Call from `setup`. */
export function provideRuleEditorStore(store: RuleEditorStore): void {
  provide(RULE_EDITOR_STORE, store);
}

/** Returns the store from the nearest provider; throws when used outside one. */
export function useRuleEditorStore(): RuleEditorStore {
  const store = inject(RULE_EDITOR_STORE, null);
  if (!store) throw new Error('useRuleEditorStore must be used under provideRuleEditorStore().');
  return store;
}
