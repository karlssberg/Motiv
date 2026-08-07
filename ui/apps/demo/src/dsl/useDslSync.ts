import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  mergeDecorations, parse, print,
  type ParseResult, type RuleDocument, type RuleEditorStore,
} from '@motiv-rules/core';

/** How the text buffer currently relates to the store's document. */
export type SyncStatus =
  /** The buffer has been committed; the store's document is what the text says. */
  | 'synced'
  /** The buffer has uncommitted edits; a commit is pending. */
  | 'dirty'
  /** The buffer does not parse, so it cannot be committed. */
  | 'error';

/** Milliseconds of quiet before an edited buffer is parsed and committed to the store. */
const COMMIT_DEBOUNCE_MS = 300;

/** A two-way binding between a DSL text buffer and a {@link RuleEditorStore}. */
export interface DslSync {
  /** The current buffer contents. */
  text: string;
  status: SyncStatus;
  /**
   * True when the store changed underneath uncommitted edits. Both versions are then held
   * apart — the pending commit is cancelled — until the user picks one.
   */
  conflict: boolean;
  /** The parse of the current buffer, for highlighting, linting and span lookups. */
  parseResult: ParseResult;
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
 * Drives a DSL text buffer as the source of truth for a rule document. Edits debounce-parse and
 * commit into `store`, so the other panes follow the text; changes made elsewhere reprint into a
 * clean buffer, or raise a conflict against a dirty one.
 *
 * The mutable coordination state (`baseDocument`, `selfCommitting`, `dirty`, the timer) lives in
 * refs rather than state so the store subscription — registered once — always reads current
 * values instead of the values captured when it was created.
 */
export function useDslSync(store: RuleEditorStore): DslSync {
  const [text, setTextState] = useState(() => print(store.getState().document));
  const [status, setStatus] = useState<SyncStatus>('synced');
  const [conflict, setConflict] = useState(false);

  /** The store document this buffer was last reconciled against; anything else is external. */
  const baseDocument = useRef<RuleDocument>(store.getState().document);
  /** Set while this hook is the one writing to the store, so its own notification is ignored. */
  const selfCommitting = useRef(false);
  const dirty = useRef(false);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  const parseResult = useMemo(() => parse(text), [text]);

  const cancelPendingCommit = useCallback(() => {
    if (timer.current === null) return;
    clearTimeout(timer.current);
    timer.current = null;
  }, []);

  const commit = useCallback((source: string) => {
    timer.current = null;
    const parsed = parse(source);
    if (!parsed.document) {
      setStatus('error');
      return;
    }

    // `loadDocument` deliberately clears the store's undo/redo history (see RuleEditorStore).
    // That is correct here: while the DSL text drives the document, undo belongs to the
    // editor's own text history (CodeMirror's `history()` extension), not the tree store —
    // a tree-level undo would silently contradict the buffer. Do not "fix" this to a
    // history-preserving mutation.
    selfCommitting.current = true;
    store.loadDocument(mergeDecorations(parsed.document, store.getState().document));
    // The store clones on load, so the reconciled baseline must be read back rather than
    // reusing the object that was handed in — they are not the same reference.
    baseDocument.current = store.getState().document;
    selfCommitting.current = false;

    dirty.current = false;
    setStatus('synced');
  }, [store]);

  /** Arms the debounced commit of `source`, replacing any commit already in flight. */
  const scheduleCommit = useCallback((source: string) => {
    cancelPendingCommit();
    timer.current = setTimeout(() => commit(source), COMMIT_DEBOUNCE_MS);
  }, [cancelPendingCommit, commit]);

  const setText = useCallback((next: string) => {
    setTextState(next);
    dirty.current = true;
    setStatus('dirty');
    scheduleCommit(next);
  }, [scheduleCommit]);

  useEffect(() => store.subscribe(() => {
    const current = store.getState().document;
    // This hook's own commit: adopt the new baseline without treating it as someone else's edit.
    if (selfCommitting.current) {
      baseDocument.current = current;
      return;
    }
    // A notification that left the document alone (e.g. setErrors) is not a change to reconcile.
    if (current === baseDocument.current) return;

    baseDocument.current = current;
    if (dirty.current) {
      // A pending commit is a decision already in flight. The conflict state exists to hold the
      // two versions apart until the user picks one, so the commit must die here — otherwise it
      // fires mid-banner, overwrites the change that raised the conflict, and leaves "reformat
      // from tree" reprinting the user's own text. `keepEditing` re-arms it.
      cancelPendingCommit();
      setConflict(true);
      return;
    }
    setTextState(print(current));
    setStatus('synced');
  }), [cancelPendingCommit, store]);

  useEffect(() => cancelPendingCommit, [cancelPendingCommit]);

  const format = useCallback(() => {
    if (!parseResult.document) return;
    const printed = print(parseResult.document);
    if (printed !== text) setText(printed);
  }, [parseResult, setText, text]);

  const reformatFromTree = useCallback(() => {
    cancelPendingCommit();
    const current = store.getState().document;
    baseDocument.current = current;
    setTextState(print(current));
    dirty.current = false;
    setConflict(false);
    setStatus('synced');
  }, [cancelPendingCommit, store]);

  const keepEditing = useCallback(() => {
    setConflict(false);
    // The user has chosen their local text, so the commit cancelled when the conflict was raised
    // is armed again — dismissing the banner must not leave the buffer uncommitted forever.
    if (dirty.current) scheduleCommit(text);
  }, [scheduleCommit, text]);

  return { text, status, conflict, parseResult, setText, format, reformatFromTree, keepEditing };
}
