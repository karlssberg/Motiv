import { toValue, type MaybeRefOrGetter, type ShallowRef } from 'vue';
import type { RuleEditorStore, RulesApiClient } from '@motiv-rules/core';
import { RuleWorkflowController, type RuleWorkflowState } from '@motiv-rules/core/workflow';
import { observe } from '../observe.js';

/** A {@link RuleWorkflowState} ref plus the actions that drive it — what a component binds to. */
export interface RuleWorkflow {
  readonly state: Readonly<ShallowRef<RuleWorkflowState>>;
  /** Refetches the listing. */
  refresh(): Promise<void>;
  /** Loads a rule into the store and takes its identity; `null` returns to the local draft. */
  load(name: string | null): Promise<void>;
  /** Saves the store's document back under the loaded identity. */
  save(): Promise<void>;
}

/**
 * Binds a {@link RuleWorkflowController} to the scope's lifetime: one controller per
 * (client, store) pair, rebuilt when either changes — a different client is a different server
 * world, and nothing carries over.
 */
export function useRuleWorkflow(
  client: MaybeRefOrGetter<RulesApiClient>,
  store: MaybeRefOrGetter<RuleEditorStore>,
): RuleWorkflow {
  const { state, dispatch } = observe(
    [client, store],
    () => new RuleWorkflowController(toValue(client), toValue(store)),
  );

  return {
    state,
    refresh: () => dispatch((c) => c.refresh()),
    load: (name) => dispatch((c) => c.load(name)),
    save: () => dispatch((c) => c.save()),
  };
}
