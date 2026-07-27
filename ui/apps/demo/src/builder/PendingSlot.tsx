import { useRef, useState } from 'react';
import { parse, type Catalog, type RuleNode } from '@motiv/rules-core';
import { useInlineDslEditor } from './useInlineDslEditor.js';

/**
 * A row that does not exist yet: an insertion point with a focused editor and nothing behind it.
 *
 * Deliberately not backed by a document node. `schemas/rule.v1.json` has no blank-node kind, so a
 * placeholder would be schema-invalid the moment the JSON pane rendered it or `/evaluate` received
 * it — and it would sit in undo history as a state the user can return to but not evaluate. So the
 * uncommitted node lives here, in React state, exactly as `NodeDsl` keeps an unparseable buffer out
 * of the document.
 *
 * An empty buffer cancels rather than erroring. Pressing Enter on an untouched slot, or clicking
 * away from one, means "never mind" — reporting "expected an expression" for it would be scolding
 * the user for changing their mind.
 */
export function PendingSlot(props: {
  modelType: string;
  catalog: Catalog;
  onCommit: (node: RuleNode) => void;
  onCancel: () => void;
}) {
  const { modelType, catalog, onCommit, onCancel } = props;
  const [error, setError] = useState<string | null>(null);

  const scope = useRef({ catalog, modelType });
  scope.current = { catalog, modelType };

  const { host } = useInlineDslEditor({
    active: true,
    initialText: '',
    scope: () => scope.current,
    onCommit: (buffer) => {
      if (buffer.trim() === '') {
        onCancel();
        return true;
      }
      const result = parse(buffer);
      if (!result.document || result.errors.length > 0) {
        setError(result.errors[0]?.message ?? 'could not parse this expression');
        return false;
      }
      onCommit(result.document.rule);
      return true;
    },
    onCancel,
    // Retires a refused commit's message on the next keystroke. Left standing it would sit
    // beside the field for the whole time you spend typing the fix. The hook owns no error
    // state — that is why this is a callback rather than something it does itself.
    onChange: () => setError(null),
    ariaLabel: 'new expression',
  });

  return (
    <div className="node">
      <div className="node-row node-row-pending">
        <span className="node-chev">＋</span>
        <span className="node-dsl node-dsl-editing">
          <span ref={host} className="node-dsl-host" />
          {error && <span role="alert" className="error node-dsl-error" title={error}>{error}</span>}
        </span>
      </div>
    </div>
  );
}
