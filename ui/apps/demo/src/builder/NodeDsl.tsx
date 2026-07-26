import { useEffect, useRef, useState } from 'react';
import { autocompletion, completionKeymap } from '@codemirror/autocomplete';
import { defaultKeymap, history, historyKeymap } from '@codemirror/commands';
import { EditorState } from '@codemirror/state';
import { EditorView, keymap } from '@codemirror/view';
import { parse, printInline, type Catalog, type RuleNode } from '@motiv/rules-core';
import { useRuleEditorStore } from '@motiv/rules-react';
import { createMotivCompletion } from '../dsl/completion.js';
import { motiv } from '../dsl/motivLanguage.js';
import { motivEditorTheme } from '../dsl/theme.js';
import { tokenSpans } from './dslTokens.js';

/** Keeps a row to one line: a pasted newline would silently grow the row out of the tree. */
const singleLine = EditorState.transactionFilter.of((tr) => (tr.newDoc.lines > 1 ? [] : tr));

/**
 * A node rendered as one line of DSL — what a leaf always shows, and what a parent shows once
 * its subtree is collapsed — and, on focus, edited as text.
 *
 * The read state is static highlighted spans, so a tree of any size costs no editors. Focus
 * swaps in a CodeMirror instance; because only one element can hold focus, exactly one is ever
 * mounted without any central bookkeeping.
 *
 * A commit parses the buffer and splices the result in through `replaceNode`. An unparseable
 * buffer is refused and the text is left as typed — the invalid state lives only in the editor,
 * never in the document, exactly as the DSL pane's uncommitted buffer does.
 */
export function NodeDsl(props: { path: string; node: RuleNode; modelType: string; catalog: Catalog }) {
  const { path, node, modelType, catalog } = props;
  const store = useRuleEditorStore();
  const text = printInline(node);

  const [editing, setEditing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const host = useRef<HTMLSpanElement | null>(null);
  /** The latest render's values, for the once-built extensions to read. */
  const live = useRef({ path, text, catalog, modelType });
  live.current = { path, text, catalog, modelType };

  /**
   * Cleared before the view is torn down, so the blur that teardown provokes cannot write back.
   * Destroying a focused CodeMirror fires `blur`, and by then the row may be gone — switching
   * editing surfaces, or a parent re-render dropping this node — leaving `replaceNode` to
   * address a path that no longer exists.
   */
  const attached = useRef(true);

  const stop = (): void => {
    setEditing(false);
    setError(null);
  };

  const commit = (buffer: string): boolean => {
    if (!attached.current) return false;
    const result = parse(buffer);
    if (!result.document || result.errors.length > 0) {
      setError(result.errors[0]?.message ?? 'could not parse this expression');
      return false;
    }
    store.replaceNode(live.current.path, result.document.rule);
    stop();
    return true;
  };

  useEffect(() => {
    const parent = host.current;
    if (!editing || !parent) return;

    attached.current = true;

    // Completion is scoped to this row's model type, so a quantifier body offers only the
    // element's specs. The picker this replaced filtered the same way; the DSL pane does not.
    const scoped = (): Catalog => ({
      specs: live.current.catalog.specs.filter((spec) => spec.modelType === live.current.modelType),
      collections: live.current.catalog.collections,
    });

    const view = new EditorView({
      parent,
      state: EditorState.create({
        doc: live.current.text,
        extensions: [
          singleLine,
          history(),
          motiv(),
          motivEditorTheme,
          autocompletion({ override: [createMotivCompletion(scoped)] }),
          // Ahead of the default bindings, which would otherwise claim Enter for a newline.
          keymap.of([
            { key: 'Enter', run: (editor) => commit(editor.state.doc.toString()) },
            { key: 'Escape', run: () => { stop(); return true; } },
          ]),
          keymap.of([...defaultKeymap, ...historyKeymap, ...completionKeymap]),
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
  }, [editing]);

  if (editing) {
    return (
      <span className="node-dsl node-dsl-editing">
        <span ref={host} className="node-dsl-host" />
        {error && <span role="alert" className="error node-dsl-error">{error}</span>}
      </span>
    );
  }

  return (
    <button
      type="button"
      className="node-dsl"
      aria-label={`edit expression at ${path}`}
      onFocus={() => setEditing(true)}
      onClick={() => setEditing(true)}
    >
      <span aria-label={`expression at ${path}`}>
        {tokenSpans(text).map((span) => (
          <span key={span.key} className={`tok-${span.kind}`}>{span.value}</span>
        ))}
      </span>
    </button>
  );
}
