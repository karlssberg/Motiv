import type { RuleDocument } from './document.js';
import type { RuleEditorStore } from './editor.js';
import { mergeDecorations } from './dsl/decorations.js';
import { parse } from './dsl/parser.js';
import { print } from './dsl/printer.js';
import type { ParseResult } from './dsl/types.js';

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

/** The observable state of a {@link DslSyncController}. */
export interface DslSyncState {
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
}

/**
 * A two-way binding between a DSL text buffer and a {@link RuleEditorStore}, driving the text
 * as the source of truth for the rule document. Edits debounce-parse and commit into the store,
 * so the other views follow the text; changes made elsewhere reprint into a clean buffer, or
 * raise a conflict against a dirty one.
 *
 * Framework-free by design: it exposes the same `subscribe`/`getState` shape as
 * `RuleEditorStore`, and a UI binding (e.g. `@motiv-rules/react`'s `useDslSync`) adapts it to
 * its framework's subscription primitive.
 *
 * Following the store is explicit: nothing is reconciled until {@link connect} is called, and
 * disconnecting cancels any in-flight commit — which is what lets a UI binding tie the
 * store subscription to its own mount/unmount lifecycle.
 */
export class DslSyncController {
  readonly #store: RuleEditorStore;
  #detach: (() => void) | null = null;
  readonly #listeners = new Set<() => void>();

  #text: string;
  #status: SyncStatus = 'synced';
  #conflict = false;

  /** The store document this buffer was last reconciled against; anything else is external. */
  #baseDocument: RuleDocument;
  /** Set while this controller is the one writing to the store, so its own notification is ignored. */
  #selfCommitting = false;
  #dirty = false;
  #timer: ReturnType<typeof setTimeout> | null = null;

  /** The parse of `#text`, computed once per text. */
  #parsedText: string;
  #parseResult: ParseResult;
  /** The state object handed out until something changes, so unchanged reads stay identical. */
  #snapshot: DslSyncState | null = null;

  constructor(store: RuleEditorStore) {
    this.#store = store;
    this.#text = print(store.getState().document);
    this.#baseDocument = store.getState().document;
    this.#parsedText = this.#text;
    this.#parseResult = parse(this.#text);
  }

  /**
   * Starts following the store, so external document changes reprint or raise conflicts.
   * Returns the disconnect function, which also cancels any pending commit; connecting again
   * after a disconnect resumes following.
   */
  connect(): () => void {
    if (this.#detach) throw new Error('DslSyncController is already connected.');
    const unsubscribe = this.#store.subscribe(() => this.#onStoreChanged());
    const detach = () => {
      if (this.#detach !== detach) return;
      this.#detach = null;
      unsubscribe();
      this.#cancelPendingCommit();
    };
    this.#detach = detach;
    return detach;
  }

  getState(): DslSyncState {
    if (this.#parsedText !== this.#text) {
      this.#parsedText = this.#text;
      this.#parseResult = parse(this.#text);
    }
    this.#snapshot ??= {
      text: this.#text,
      status: this.#status,
      conflict: this.#conflict,
      parseResult: this.#parseResult,
    };
    return this.#snapshot;
  }

  subscribe(listener: () => void): () => void {
    this.#listeners.add(listener);
    return () => this.#listeners.delete(listener);
  }

  /** Replaces the buffer, marking it dirty and (re)arming the debounced commit. */
  setText(next: string): void {
    this.#text = next;
    this.#dirty = true;
    this.#status = 'dirty';
    this.#scheduleCommit(next);
    this.#notify();
  }

  /** Reprints the buffer canonically, keeping whatever it currently says. */
  format(): void {
    const { parseResult } = this.getState();
    if (!parseResult.document) return;
    const printed = print(parseResult.document);
    if (printed !== this.#text) this.setText(printed);
  }

  /** Discards the buffer and reprints from the store — the conflict resolution that yields. */
  reformatFromTree(): void {
    this.#cancelPendingCommit();
    const current = this.#store.getState().document;
    this.#baseDocument = current;
    this.#text = print(current);
    this.#dirty = false;
    this.#conflict = false;
    this.#status = 'synced';
    this.#notify();
  }

  /** Dismisses the conflict banner and re-arms the commit — the conflict resolution that wins. */
  keepEditing(): void {
    this.#conflict = false;
    // The user has chosen their local text, so the commit cancelled when the conflict was raised
    // is armed again — dismissing the banner must not leave the buffer uncommitted forever.
    if (this.#dirty) this.#scheduleCommit(this.#text);
    this.#notify();
  }

  #notify(): void {
    this.#snapshot = null;
    for (const listener of this.#listeners) listener();
  }

  #cancelPendingCommit(): void {
    if (this.#timer === null) return;
    clearTimeout(this.#timer);
    this.#timer = null;
  }

  /**
   * Arms the debounced commit of `source`, replacing any commit already in flight. Disconnected,
   * this is a no-op: a controller nothing is following must not write into the shared store —
   * a stale `setText` landing after a UI binding unmounted would otherwise still commit 300ms
   * later. The buffer itself keeps the edit (and stays `dirty`), so reconnecting and re-arming
   * (`keepEditing`, or a further edit) picks it back up.
   */
  #scheduleCommit(source: string): void {
    if (this.#detach === null) return;
    this.#cancelPendingCommit();
    this.#timer = setTimeout(() => this.#commit(source), COMMIT_DEBOUNCE_MS);
  }

  #commit(source: string): void {
    this.#timer = null;
    const parsed = parse(source);
    if (!parsed.document) {
      this.#status = 'error';
      this.#notify();
      return;
    }

    // `loadDocument` deliberately clears the store's undo/redo history (see RuleEditorStore).
    // That is correct here: while the DSL text drives the document, undo belongs to the text
    // editor's own history, not the tree store — a tree-level undo would silently contradict
    // the buffer. Do not "fix" this to a history-preserving mutation.
    //
    // The store notifies its subscribers synchronously and unguarded, so a *different*
    // subscriber throwing propagates out of `loadDocument`. The guard must reset regardless:
    // left latched, every later external change would be silently adopted as this controller's
    // own commit, and the sync would be broken for good with nothing announcing it.
    this.#selfCommitting = true;
    try {
      this.#store.loadDocument(mergeDecorations(parsed.document, this.#store.getState().document));
      // The store clones on load, so the reconciled baseline must be read back rather than
      // reusing the object that was handed in — they are not the same reference.
      this.#baseDocument = this.#store.getState().document;
    } finally {
      this.#selfCommitting = false;
    }

    this.#dirty = false;
    this.#status = 'synced';
    this.#notify();
  }

  #onStoreChanged(): void {
    const current = this.#store.getState().document;
    // This controller's own commit: adopt the new baseline without treating it as someone else's edit.
    if (this.#selfCommitting) {
      this.#baseDocument = current;
      return;
    }
    // A notification that left the document alone (e.g. setErrors) is not a change to reconcile.
    if (current === this.#baseDocument) return;

    this.#baseDocument = current;
    if (this.#dirty) {
      // A pending commit is a decision already in flight. The conflict state exists to hold the
      // two versions apart until the user picks one, so the commit must die here — otherwise it
      // fires mid-banner, overwrites the change that raised the conflict, and leaves "reformat
      // from tree" reprinting the user's own text. `keepEditing` re-arms it.
      this.#cancelPendingCommit();
      this.#conflict = true;
      this.#notify();
      return;
    }
    this.#text = print(current);
    this.#status = 'synced';
    this.#notify();
  }
}
