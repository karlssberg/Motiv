import { useRef, useState } from 'react';
import { parse, printInline, type Catalog, type RuleNode } from '@motiv/rules-core';
import { useRuleEditorStore } from '@motiv/rules-react';
import { tokenSpans } from './dslTokens.js';
import { useInlineDslEditor } from './useInlineDslEditor.js';

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

  /**
   * The completion scope, read lazily rather than closed over: `useCatalog` resolves
   * asynchronously, so a row focused before the catalog lands would otherwise offer nothing for
   * the life of the editor. `path` and `text` need no such treatment — the row is keyed by its
   * path, so a change remounts it and resets `editing`.
   */
  const scope = useRef({ catalog, modelType });
  scope.current = { catalog, modelType };

  const stop = (): void => {
    setEditing(false);
    setError(null);
  };

  const { host } = useInlineDslEditor({
    active: editing,
    initialText: text,
    scope: () => scope.current,
    onCommit: (buffer) => {
      const result = parse(buffer);
      if (!result.document || result.errors.length > 0) {
        setError(result.errors[0]?.message ?? 'could not parse this expression');
        return false;
      }
      store.replaceNode(path, result.document.rule);
      stop();
      return true;
    },
    onCancel: stop,
    onEdit: () => setError(null),
  });

  if (editing) {
    return (
      <span className="node-dsl node-dsl-editing">
        <span ref={host} className="node-dsl-host" />
        {/* Below the field rather than beside it: a message long enough to name what is missing
            is long enough to crowd out the expression you are typing. Truncated to one line,
            with the full text on the title so nothing is lost. */}
        {error && (
          <span role="alert" className="error node-dsl-error" title={error}>{error}</span>
        )}
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
