import { useRef, useState } from 'react';
import { parse, printInline, tokenSpans, type Catalog, type RuleNode } from '@motiv-rules/core';
import { useRuleEditorStore } from '@motiv-rules/react';

import { useInlineDslEditor, type OpeningPoint } from './useInlineDslEditor.js';

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

  /**
   * Where the pointer that opened this row landed. A ref rather than state because nothing renders
   * from it — it exists only to carry a coordinate from the handler that has one to the editor
   * that can use it.
   */
  const openedAt = useRef<OpeningPoint | null>(null);

  /**
   * The two ways in differ only in whether they have a point to report, so they share everything
   * else. `null` is not merely "no point" but an erasure: a row opened by Tab would otherwise
   * inherit wherever it was last clicked.
   */
  const start = (at: OpeningPoint | null): void => {
    openedAt.current = at;
    setEditing(true);
  };

  const stop = (): void => {
    setEditing(false);
    setError(null);
  };

  const { host } = useInlineDslEditor({
    active: editing,
    initialText: text,
    opening: () => openedAt.current,
    scope: () => scope.current,
    // The hook's `trigger` argument is dropped deliberately, not overlooked. Unlike `PendingSlot`,
    // this row edits an existing node rather than an empty slot, so a refused buffer leaves the
    // editor open over text the document already has — there is no phantom row here that a stuck
    // blur could strand, and both triggers refuse alike.
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
    onChange: () => setError(null),
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
  //
  // The two handlers are the two ways in, and they are deliberately exclusive rather than
  // redundant. `mousedown` — not `click` — because focusing the button is `mousedown`'s default
  // action, so a click already opened the row through `onFocus` long before any `click` could
  // fire; suppressing that default is what lets the pointer path keep the coordinates `onFocus`
  // never had. The editor takes focus itself once mounted, so nothing is lost by not focusing the
  // button we are about to replace.
  return (
    <button
      type="button"
      className="node-dsl"
      aria-label={`edit expression at ${path}`}
      onMouseDown={(event) => {
        // Secondary buttons keep their usual meaning — a right-click here should open a context
        // menu, not an editor, and certainly not have its default suppressed.
        if (event.button !== 0) return;
        event.preventDefault();
        start({ x: event.clientX, y: event.clientY });
      }}
      onFocus={() => start(null)}
    >
      {tokenSpans(text).map((span) => (
        <span key={span.key} className={`tok-${span.kind}`}>{span.value}</span>
      ))}
    </button>
  );
}
