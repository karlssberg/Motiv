import { useCallback, useEffect, useMemo, useState, useSyncExternalStore } from 'react';
import { DslSyncController, type DslSyncState, type RuleEditorStore } from '@motiv-rules/core';

/** A {@link DslSyncState} snapshot plus the actions that drive it — what a component binds to. */
export interface DslSync extends DslSyncState {
  /** Replaces the buffer, marking it dirty and (re)arming the debounced commit. */
  setText: (next: string) => void;
  /** Reprints the buffer canonically, keeping whatever it currently says. */
  format: () => void;
  /** Discards the buffer and reprints from the store — the conflict resolution that yields. */
  reformatFromTree: () => void;
  /** Dismisses the conflict banner and re-arms the commit — the conflict resolution that wins. */
  keepEditing: () => void;
}

/**
 * Binds a {@link DslSyncController} to the component lifecycle: one controller per store,
 * following the store while mounted, and cancelling any in-flight commit on unmount. All of
 * the sync behaviour lives in the controller — this hook only adapts its `subscribe`/`getState`
 * shape to React's subscription primitive.
 */
export function useDslSync(store: RuleEditorStore): DslSync {
  // The controller holds the user's uncommitted buffer, so it lives in state, not `useMemo` —
  // React documents the memo cache as discardable, and a rebuilt controller would reprint from
  // the store, evaporating dirty text mid-edit. A store swap rebinds during render (the
  // documented adjust-state-on-prop-change pattern); the superseded controller was never
  // connected past its own effect cleanup, so it holds no timer and follows nothing.
  const [binding, setBinding] = useState(() => ({ store, controller: new DslSyncController(store) }));
  let active = binding;
  if (binding.store !== store) {
    active = { store, controller: new DslSyncController(store) };
    setBinding(active);
  }
  const { controller } = active;

  useEffect(() => controller.connect(), [controller]);

  const subscribe = useCallback(
    (onChange: () => void) => controller.subscribe(onChange),
    [controller],
  );
  const getSnapshot = useCallback(() => controller.getState(), [controller]);
  const state = useSyncExternalStore(subscribe, getSnapshot, getSnapshot);

  return useMemo(() => ({
    ...state,
    setText: (next: string) => controller.setText(next),
    format: () => controller.format(),
    reformatFromTree: () => controller.reformatFromTree(),
    keepEditing: () => controller.keepEditing(),
  }), [controller, state]);
}
