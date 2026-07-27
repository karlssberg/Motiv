import { useEffect, useRef, type MutableRefObject } from 'react';
import { autocompletion, completionKeymap } from '@codemirror/autocomplete';
import { defaultKeymap, history, historyKeymap } from '@codemirror/commands';
import { EditorState } from '@codemirror/state';
import { EditorView, keymap } from '@codemirror/view';
import type { Catalog } from '@motiv/rules-core';
import { createMotivCompletion } from '../dsl/completion.js';
import { motiv } from '../dsl/motivLanguage.js';
import { motivEditorTheme } from '../dsl/theme.js';

/** Keeps a row to one line: a pasted newline would silently grow the row out of the tree. */
const singleLine = EditorState.transactionFilter.of((tr) => (tr.newDoc.lines > 1 ? [] : tr));

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
  onCommit: (text: string) => boolean;
  onCancel: () => void;
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

    const commit = (buffer: string): boolean => {
      if (!attached.current) return false;
      // Disarm before delegating, not after. A successful commit makes the caller unmount this
      // editor, and the blur that DOM removal provokes would otherwise re-enter here and commit
      // the same buffer a second time. Effect cleanup cannot close this window: it is passive,
      // so it runs after the removal — hence after the blur. Disarming up front is also
      // independent of whether React flushes the caller's state update synchronously.
      attached.current = false;
      const accepted = options.onCommit(buffer);
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
                commit(editor.state.doc.toString());
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
            blur: (_event, editor) => { commit(editor.state.doc.toString()); return false; },
          }),
        ],
      }),
    });
    view.focus();
    view.dispatch({ selection: { anchor: 0, head: view.state.doc.length } });

    return () => {
      attached.current = false;
      view.destroy();
    };
  }, [options.active]);

  return { host };
}
