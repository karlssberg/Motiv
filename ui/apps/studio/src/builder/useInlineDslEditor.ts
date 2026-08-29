import { useEffect, useRef, type MutableRefObject } from 'react';
import { autocompletion, completionKeymap } from '@codemirror/autocomplete';
import { defaultKeymap, history, historyKeymap } from '@codemirror/commands';
import { EditorState } from '@codemirror/state';
import { EditorView, keymap } from '@codemirror/view';
import type { Catalog } from '@motiv-rules/core';
import { createMotivCompletion } from '../dsl/completion.js';
import { motiv } from '../dsl/motivLanguage.js';
import { motivEditorTheme } from '../dsl/theme.js';

/** Keeps a row to one line: a pasted newline would silently grow the row out of the tree. */
const singleLine = EditorState.transactionFilter.of((tr) => (tr.newDoc.lines > 1 ? [] : tr));

/** Which of the two commit paths fired. */
export type CommitTrigger = 'enter' | 'blur';

/** Viewport coordinates a pointer landed on, to be resolved against the mounted editor. */
export type OpeningPoint = { x: number; y: number };

/**
 * What the editor selects when it opens, which follows from how it was opened.
 *
 * A pointer names a character, so the caret goes under it — the same thing every other text field
 * does, and the difference between correcting one spec name and retyping the whole line. Every
 * other way in (a Tab, a slot that opens itself) names only the row, so the buffer is selected and
 * the next keystroke replaces it: the only useful default when the user cannot have meant a
 * particular character.
 */
function openingSelection(view: EditorView, at: OpeningPoint | null): { anchor: number; head: number } {
  if (!at) return { anchor: 0, head: view.state.doc.length };
  // `posAtCoords` reports `null` only for coordinates outside the editor's own box, and a click
  // that opened this editor is inside it by construction — the fallback covers the degenerate
  // case (a row measured mid-layout) by landing at the end rather than reinstating a select-all,
  // which is the behaviour the caller asked not to have.
  const position = view.posAtCoords(at) ?? view.state.doc.length;
  return { anchor: position, head: position };
}

/**
 * Mounts and tears down a one-line CodeMirror editor for a DSL expression: single-line
 * filter, the `motiv()` language, model-scoped completion, Enter to commit, Escape to cancel,
 * and commit-on-blur.
 *
 * Owns the editor's `host` element ref and its `attached` lifecycle guard. Owns no error
 * state — the caller renders the message, since different callers place it differently.
 */
export function useInlineDslEditor(options: {
  active: boolean;
  initialText: string;
  scope: () => { catalog: Catalog; modelType: string };
  /**
   * `trigger` names which of the two commit paths fired — Enter or a blur. The hook itself
   * treats them identically (both route through the same guarded `commit`), but a caller often
   * cannot: refusing an unparseable buffer on Enter leaves the editor open for the user to fix,
   * who is still mid-edit and would be poorly served by having their typing discarded. The same
   * refusal on blur instead leaves an unfocused, unreachable row behind — the user has already
   * moved on, so a caller that models "nothing here yet" (like `PendingSlot`) may prefer to treat
   * an unparseable blur as abandonment rather than a refusal.
   */
  onCommit: (text: string, trigger: CommitTrigger) => boolean;
  onCancel: () => void;
  /**
   * Where the pointer that opened this editor landed, or `null` when it was opened some other way.
   * Omitted entirely by a caller that has no pointer to report.
   *
   * A getter for the same reason `scope` is one, though for the opposite reason it needs to be
   * read late rather than early: an opening is an event, not a state, so this is read exactly once
   * — at the moment `active` turns true. Reading the caller's ref *here* keeps that guarantee in
   * this file, rather than making it contingent on which render the caller happened to sample it in.
   */
  opening?: () => OpeningPoint | null;
  /**
   * Fired on every doc change. The hook owns no error state, so it cannot clear a caller's
   * error itself — this lets the caller retire a refused commit's message on the next
   * keystroke, the way it always has.
   */
  onChange?: () => void;
  /** Accessible name for the editable region. Applied to `.cm-content`, not the host — CodeMirror
   *  puts `role="textbox"` there, and ARIA does not inherit a name from an ancestor that has its
   *  own role, so a label on the host would name nothing. */
  ariaLabel?: string;
}): { host: MutableRefObject<HTMLSpanElement | null> } {
  const host = useRef<HTMLSpanElement | null>(null);

  /**
   * Cleared before the view is torn down, so the blur that teardown provokes cannot write back.
   * Destroying a focused CodeMirror fires `blur`, and by then the row may be gone — switching
   * editing surfaces, or a parent re-render dropping this node — leaving the caller's commit to
   * address state that no longer exists.
   */
  const attached = useRef(false);

  useEffect(() => {
    const parent = host.current;
    if (!options.active || !parent) return;

    attached.current = true;

    const commit = (buffer: string, trigger: CommitTrigger): boolean => {
      if (!attached.current) return false;
      // Disarm before delegating, not after. A successful commit makes the caller unmount this
      // editor, and the blur that DOM removal provokes would otherwise re-enter here and commit
      // the same buffer a second time. Effect cleanup cannot close this window: it is passive,
      // so it runs after the removal — hence after the blur. Disarming up front is also
      // independent of whether React flushes the caller's state update synchronously.
      attached.current = false;
      const accepted = options.onCommit(buffer, trigger);
      // A refused buffer leaves the editor open and still usable, so re-arm for the next attempt.
      if (!accepted) attached.current = true;
      return accepted;
    };

    // Escape bypassed `commit` entirely, calling `options.onCancel` directly — which never
    // touched the guard. Cancelling also unmounts this editor (the same as a successful commit
    // does), so the same teardown blur can re-enter `commit` with the guard still armed and the
    // typed buffer still sitting in the doc, silently inserting what the user just cancelled.
    // Routing cancellation through this guard closes that window the same way `commit` does.
    const cancel = (): void => {
      if (!attached.current) return;
      attached.current = false;
      options.onCancel();
    };

    // Completion offers the specs of this row's own model type — the same filter the spec picker
    // it replaces applied, and one the DSL pane does not apply at all. One row has one scope, so
    // a *collapsed* quantifier's body is still offered its parent's specs; expanded, the body is
    // its own row and gets the element scope.
    const scoped = (): Catalog => {
      const { catalog, modelType } = options.scope();
      return {
        specs: catalog.specs.filter((s) => s.modelType === modelType),
        collections: catalog.collections,
      };
    };

    const view = new EditorView({
      parent,
      state: EditorState.create({
        doc: options.initialText,
        extensions: [
          singleLine,
          history(),
          motiv(),
          motivEditorTheme,
          autocompletion({ override: [createMotivCompletion(scoped)] }),
          ...(options.ariaLabel ? [EditorView.contentAttributes.of({ 'aria-label': options.ariaLabel })] : []),
          // Ahead of the default bindings, which would otherwise claim Enter for a newline.
          keymap.of([
            {
              key: 'Enter',
              run: (editor) => {
                commit(editor.state.doc.toString(), 'enter');
                // Consumed either way. Reporting a refused commit as unhandled passes the
                // keystroke to whatever binds Enter next — the default newline, which the
                // single-line filter then swallows, so the key appears to do nothing at all.
                //
                // Deferring to the completion popup here would be redundant: `autocompletion`
                // registers its own bindings at the highest precedence, so an open popup has
                // already claimed Enter before this runs.
                return true;
              },
            },
            // A fully-typed buffer that also matches a live completion can take two presses to
            // cancel: `autocompletion()` installs its own higher-precedence keymap, so an open
            // completion popup claims the first Escape to dismiss itself, and only the second
            // reaches this binding.
            { key: 'Escape', run: () => { cancel(); return true; } },
          ]),
          keymap.of([...defaultKeymap, ...historyKeymap, ...completionKeymap]),
          // A refused commit's message describes the buffer as it was when refused, so the next
          // keystroke retires it. Left standing it would sit beside the field for the whole time
          // you spend typing the fix — which, for a half-typed group, is the whole expression.
          EditorView.updateListener.of((update) => {
            if (update.docChanged) options.onChange?.();
          }),
          EditorView.domEventHandlers({
            blur: (_event, editor) => { commit(editor.state.doc.toString(), 'blur'); return false; },
          }),
        ],
      }),
    });
    view.focus();
    view.dispatch({ selection: openingSelection(view, options.opening?.() ?? null) });

    return () => {
      attached.current = false;
      view.destroy();
    };
    // Rebuilds only on a non-editing → editing transition, not on every render. While `active`
    // stays `true` the closures above stay pinned to the options captured when it became `true` —
    // safe because `PendingSlot` holds `active` constant for its whole life, and `NodeDsl`'s rows
    // are keyed by path, so a meaningful change (a different row, a different pending slot) remounts
    // this hook entirely rather than re-running this effect with stale props.
  }, [options.active]);

  return { host };
}
