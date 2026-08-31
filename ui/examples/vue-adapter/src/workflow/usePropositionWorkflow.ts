import { toValue, type MaybeRefOrGetter, type ShallowRef } from 'vue';
import type {
  PropositionCreateRequest, PropositionListEntry, RuleEditorStore, RulesApiClient,
} from '@motiv-rules/core';
import {
  PropositionWorkflowController, type PropositionWorkflowState,
} from '@motiv-rules/core/workflow';
import { observe } from '../observe.js';

/** Options for {@link usePropositionWorkflow}. */
export interface UsePropositionWorkflowOptions {
  /** Where the selection should go after an act that moves it — typically a route setter. */
  onSelect?: (name: string | null) => void;
}

/**
 * A {@link PropositionWorkflowState} ref plus the actions that drive it — what a component binds
 * to.
 */
export interface PropositionWorkflow {
  readonly state: Readonly<ShallowRef<PropositionWorkflowState>>;
  /** Refetches the listing, reporting a failed reload in the failure banner. */
  refreshEntries(): Promise<void>;
  /** Selects a proposition, fetching its document and blast radius; `null` clears. */
  select(name: string | null): Promise<void>;
  /** Refetches behind the current selection — for a revert, or conflict recovery. */
  reload(): Promise<void>;
  /** Saves the store's document back under the selected identity. */
  save(): Promise<void>;
  /** Deletes an authored proposition, or reverts an overridden one to its compiled spec. */
  remove(entry: PropositionListEntry): Promise<void>;
  /** Authors a new proposition, answering the failure to show — `null` on success. */
  create(request: PropositionCreateRequest): Promise<string | null>;
}

/**
 * Binds a {@link PropositionWorkflowController} to the scope's lifetime: one controller per
 * (client, store) pair.
 *
 * `options` is read through `toValue` at handover time rather than captured at construction, so a
 * consumer whose `onSelect` is rebuilt on every render still gets the callback it holds now. That
 * is the same problem `@motiv-rules/react` solves with a ref written from an effect; here the
 * options object is simply read late, and the window in which an in-flight completion reaches a
 * superseded callback does not exist.
 */
export function usePropositionWorkflow(
  client: MaybeRefOrGetter<RulesApiClient>,
  store: MaybeRefOrGetter<RuleEditorStore>,
  options: MaybeRefOrGetter<UsePropositionWorkflowOptions> = {},
): PropositionWorkflow {
  const { state, dispatch } = observe(
    [client, store],
    () => new PropositionWorkflowController(toValue(client), toValue(store), {
      onSelect: (name) => toValue(options).onSelect?.(name),
    }),
  );

  return {
    state,
    refreshEntries: () => dispatch((c) => c.refreshEntries()),
    select: (name) => dispatch((c) => c.select(name)),
    reload: () => dispatch((c) => c.reload()),
    save: () => dispatch((c) => c.save()),
    remove: (entry) => dispatch((c) => c.remove(entry)),
    create: (request) => dispatch((c) => c.create(request)),
  };
}
