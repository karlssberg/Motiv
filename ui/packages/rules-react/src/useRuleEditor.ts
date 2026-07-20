import { useCallback, useRef, useSyncExternalStore } from 'react';
import type { EditorState, RuleEditorStore } from '@motiv/rules-core';

/**
 * Subscribes a component to a {@link RuleEditorStore} and returns its current state.
 * The store allocates a fresh state object per call, so the snapshot is cached and only
 * replaced when one of its immutable fields changes — required by useSyncExternalStore.
 */
export function useRuleEditor(store: RuleEditorStore): EditorState {
  const cache = useRef<EditorState | null>(null);

  const subscribe = useCallback((onChange: () => void) => store.subscribe(onChange), [store]);

  const getSnapshot = useCallback((): EditorState => {
    const next = store.getState();
    const prev = cache.current;
    if (
      prev &&
      prev.document === next.document &&
      prev.errors === next.errors &&
      prev.canUndo === next.canUndo &&
      prev.canRedo === next.canRedo
    ) {
      return prev;
    }
    cache.current = next;
    return next;
  }, [store]);

  return useSyncExternalStore(subscribe, getSnapshot, getSnapshot);
}
