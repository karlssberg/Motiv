import { toValue, type MaybeRefOrGetter, type ShallowRef } from 'vue';
import { DslSyncController, type DslSyncState, type RuleEditorStore } from '@motiv-rules/core';
import { observe } from './observe.js';

/** A {@link DslSyncState} ref plus the actions that drive it — what a component binds to. */
export interface DslSync {
  readonly state: Readonly<ShallowRef<DslSyncState>>;
  /** Replaces the buffer, marking it dirty and (re)arming the debounced commit. */
  setText(next: string): void;
  /** Reprints the buffer canonically, keeping whatever it currently says. */
  format(): void;
  /** Discards the buffer and reprints from the store — the conflict resolution that yields. */
  reformatFromTree(): void;
  /** Dismisses the conflict banner and re-arms the commit — the conflict resolution that wins. */
  keepEditing(): void;
}

/**
 * Binds a {@link DslSyncController} to the scope's lifetime: one controller per store, following
 * the store while the scope lives, and cancelling any in-flight commit when it ends. All of the
 * sync behaviour is the controller's — this composable only makes its subscription the scope's
 * business.
 *
 * `connect()` returns its own disconnect, which is the shape `observe` takes for teardown, so the
 * controller's follow-the-store lifetime and the subscription's end together and cannot drift.
 */
export function useDslSync(store: MaybeRefOrGetter<RuleEditorStore>): DslSync {
  const { state, dispatch } = observe(
    [store],
    () => new DslSyncController(toValue(store)),
    (controller) => controller.connect(),
  );

  return {
    state,
    setText: (next) => dispatch((c) => c.setText(next)),
    format: () => dispatch((c) => c.format()),
    reformatFromTree: () => dispatch((c) => c.reformatFromTree()),
    keepEditing: () => dispatch((c) => c.keepEditing()),
  };
}
