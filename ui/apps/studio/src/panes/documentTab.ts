import { useEffect, useRef, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import type { DocKind, Workspace } from '../shell/workspace.js';

/**
 * The plumbing a rule tab and a proposition tab share word for word. Only what is the same act on
 * both lives here: where the document's actions are drawn, how the tab hands its save to the shell,
 * and how a save reaches the workspace. Everything either kind does differently — its workflow, its
 * panes, its strip — stays in its own document.
 */

/** How a document tab hands its save to the shell, and takes it back: `null` withdraws it. */
export type SaverSink = (save: (() => Promise<boolean>) | null) => void;

/** The actions in the editor's header, or portalled into the bar while the strip is compact. */
export function placeActions(host: HTMLElement | null | undefined, actions: ReactNode): ReactNode {
  return host ? createPortal(actions, host) : actions;
}

/**
 * Hands the shell this tab's save, so the unsaved-changes question's *Save & close* can run it for
 * a tab that is not the active one, and takes it back on unmount.
 */
export function useSaverRegistration(onSaver: SaverSink | undefined, save: () => Promise<boolean>): void {
  useEffect(() => {
    onSaver?.(save);
    return () => onSaver?.(null);
  }, [onSaver, save]);
}

/**
 * A version that moved *after* the load is a save: the workspace learns it, so every other tab that
 * references this document can see it move. `onNoted` runs on each such save, for a tab with its own
 * book-keeping to re-baseline — pass a stable callback, since it is a dependency of the effect.
 */
export function useNoteSaves(
  workspace: Workspace,
  kind: DocKind,
  loaded: { name: string; version: number } | null | undefined,
  onNoted?: () => void,
): void {
  const seenVersion = useRef<number | null>(null);
  useEffect(() => {
    if (!loaded) { seenVersion.current = null; return; }
    if (seenVersion.current !== null && loaded.version !== seenVersion.current) {
      workspace.noteSaved(kind, loaded.name, loaded.version);
      onNoted?.();
    }
    seenVersion.current = loaded.version;
  }, [loaded, workspace, kind, onNoted]);
}
