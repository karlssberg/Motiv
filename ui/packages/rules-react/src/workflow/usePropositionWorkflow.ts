import { useCallback, useEffect, useMemo, useRef, useState, useSyncExternalStore } from 'react';
import type {
  PropositionCreateRequest, PropositionListEntry, RuleEditorStore, RulesApiClient,
} from '@motiv-rules/core';
import {
  PropositionWorkflowController, type PropositionWorkflowState,
} from '@motiv-rules/core/workflow';

/** Options for {@link usePropositionWorkflow}. */
export interface UsePropositionWorkflowOptions {
  /** Where the selection should go after an act that moves it — typically a route setter. */
  onSelect?: (name: string | null) => void;
}

/**
 * A {@link PropositionWorkflowState} snapshot plus the actions that drive it — what a component
 * binds to.
 */
export interface PropositionWorkflow extends PropositionWorkflowState {
  /** Refetches the listing, reporting a failed reload in the failure banner. */
  refreshEntries: () => Promise<void>;
  /** Selects a proposition, fetching its document and blast radius; `null` clears. */
  select: (name: string | null) => Promise<void>;
  /** Refetches behind the current selection — for a revert, or conflict recovery. */
  reload: () => Promise<void>;
  /** Saves the store's document back under the selected identity. */
  save: () => Promise<void>;
  /** Deletes an authored proposition, or reverts an overridden one to its compiled spec. */
  remove: (entry: PropositionListEntry) => Promise<void>;
  /** Authors a new proposition, answering the failure to show — `null` on success. */
  create: (request: PropositionCreateRequest) => Promise<string | null>;
}

/**
 * Binds a {@link PropositionWorkflowController} to the component lifecycle: one controller per
 * (client, store) pair. All of the workflow behaviour lives in the controller — this hook only
 * adapts its `subscribe`/`getState` shape to React's subscription primitive, and keeps the
 * `onSelect` handover pointed at the latest callback the component rendered with.
 */
export function usePropositionWorkflow(
  client: RulesApiClient,
  store: RuleEditorStore,
  options: UsePropositionWorkflowOptions = {},
): PropositionWorkflow {
  // The latest-callback indirection: the controller is built once per (client, store), but the
  // consumer's onSelect is typically an inline closure that changes identity every render — the
  // handover must reach the one the component holds now, not a stale closure captured at
  // construction.
  //
  // Updated in an effect rather than during render: React treats refs as commit-phase state, and
  // a render-phase write would install a callback from a render that concurrent React may yet
  // discard. The cost is a narrow window after a prop change in which an in-flight completion
  // still reaches the previous callback — the safer trade, since that callback did render.
  const onSelectRef = useRef(options.onSelect);
  useEffect(() => {
    onSelectRef.current = options.onSelect;
  }, [options.onSelect]);

  // The controller holds workflow state (the selection, an unreported failure), so it lives in
  // state, not `useMemo` — React documents the memo cache as discardable. A client or store swap
  // rebinds during render (the documented adjust-state-on-prop-change pattern): a different
  // client is a different server world, and nothing carries over.
  const makeBinding = () => ({
    client, store,
    controller: new PropositionWorkflowController(client, store, {
      onSelect: (name) => onSelectRef.current?.(name),
    }),
  });
  const [binding, setBinding] = useState(makeBinding);
  let active = binding;
  if (binding.client !== client || binding.store !== store) {
    active = makeBinding();
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
  // across state changes — an effect can depend on one (e.g. select-on-route-change) without
  // re-firing every time the state it changes comes back around.
  const actions = useMemo(() => ({
    refreshEntries: () => controller.refreshEntries(),
    select: (name: string | null) => controller.select(name),
    reload: () => controller.reload(),
    save: () => controller.save(),
    remove: (entry: PropositionListEntry) => controller.remove(entry),
    create: (request: PropositionCreateRequest) => controller.create(request),
  }), [controller]);

  return useMemo(() => ({ ...state, ...actions }), [state, actions]);
}
