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

  /**
   * The completion scope, read lazily rather than closed over: `useCatalog` resolves
   * asynchronously, so a row focused before the catalog lands would otherwise offer nothing for
   * the life of the editor. `path` and `text` need no such treatment — the row is keyed by its
   * path, so a change remounts it and resets `editing`.
   */
  const scope = useRef({ catalog, modelType });
  scope.current = { catalog, modelType };

  /**
   * Cleared before the view is torn down, so the blur that teardown provokes cannot write back.
   * Destroying a focused CodeMirror fires `blur`, and by then the row may be gone — switching
   * editing surfaces, or a parent re-render dropping this node — leaving `replaceNode` to
   * address a path that no longer exists.
   */
  const attached = useRef(false);

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
    store.replaceNode(path, result.document.rule);
    stop();
    return true;
  };

  useEffect(() => {
    const parent = host.current;
    if (!editing || !parent) return;

    attached.current = true;

    // Completion offers the specs of this row's own model type — the same filter the spec picker
    // it replaces applied, and one the DSL pane does not apply at all. One row has one scope, so
    // a *collapsed* quantifier's body is still offered its parent's specs; expanded, the body is
    // its own row and gets the element scope.
    const scoped = (): Catalog => ({
      specs: scope.current.catalog.specs.filter((s) => s.modelType === scope.current.modelType),
      collections: scope.current.catalog.collections,
    });

    const view = new EditorView({
      parent,
      state: EditorState.create({
        doc: text,
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

  // One label, on one element. An inner labelled span would give the same row two accessible
  // names differing only by prefix, which any substring-matching query reads as ambiguous.
  return (
    <button
      type="button"
      className="node-dsl"
      aria-label={`edit expression at ${path}`}
      onFocus={() => setEditing(true)}
      onClick={() => setEditing(true)}
    >
      {tokenSpans(text).map((span) => (
        <span key={span.key} className={`tok-${span.kind}`}>{span.value}</span>
      ))}
    </button>
  );
}
