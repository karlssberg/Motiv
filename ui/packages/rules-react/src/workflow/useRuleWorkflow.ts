import { useCallback, useMemo, useState, useSyncExternalStore } from 'react';
import type { RuleEditorStore, RulesApiClient } from '@motiv-rules/core';
import { RuleWorkflowController, type RuleWorkflowState } from '@motiv-rules/core/workflow';

/** A {@link RuleWorkflowState} snapshot plus the actions that drive it — what a component binds to. */
export interface RuleWorkflow extends RuleWorkflowState {
  /** Refetches the listing. */
  refresh: () => Promise<void>;
  /** Loads a rule into the store and takes its identity; `null` returns to the local draft. */
  load: (name: string | null) => Promise<void>;
  /** Saves the store's document back under the loaded identity. */
  save: () => Promise<void>;
}

/**
 * Binds a {@link RuleWorkflowController} to the component lifecycle: one controller per
 * (client, store) pair. All of the save-loop behaviour lives in the controller — this hook only
 * adapts its `subscribe`/`getState` shape to React's subscription primitive.
 */
export function useRuleWorkflow(client: RulesApiClient, store: RuleEditorStore): RuleWorkflow {
  // The controller holds workflow state (the loaded identity, an unresolved conflict), so it
  // lives in state, not `useMemo` — React documents the memo cache as discardable. A client or
  // store swap rebinds during render (the documented adjust-state-on-prop-change pattern): a
  // different client is a different server world, and nothing carries over.
  const [binding, setBinding] = useState(() => ({
    client, store, controller: new RuleWorkflowController(client, store),
  }));
  let active = binding;
  if (binding.client !== client || binding.store !== store) {
    active = { client, store, controller: new RuleWorkflowController(client, store) };
    setBinding(active);
  }
  const { controller } = active;

  const subscribe = useCallback(
    (onChange: () => void) => controller.subscribe(onChange),
    [controller],
  );
  const getSnapshot = useCallback(() => controller.getState(), [controller]);
  const state = useSyncExternalStore(subscribe, getSnapshot, getSnapshot);

  // The actions are memoised per controller, not per snapshot, so their identities are stable
  // across state changes — an effect can depend on one (e.g. refresh-on-mount) without re-firing
  // every time the state it changes comes back around.
  const actions = useMemo(() => ({
    refresh: () => controller.refresh(),
    load: (name: string | null) => controller.load(name),
    save: () => controller.save(),
  }), [controller]);

  return useMemo(() => ({ ...state, ...actions }), [state, actions]);
}
