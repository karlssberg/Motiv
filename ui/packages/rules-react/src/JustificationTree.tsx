import {
  flattenExplanation, toExplanationView,
  type ExplanationNode, type ExplanationRow,
} from '@motiv/rules-core';
import { useMemo, useState, type ReactNode } from 'react';

/** A row surfaced to a {@link JustificationTree} render prop, plus a collapse toggle. */
export interface JustificationRow {
  row: ExplanationRow;
  toggle: (id: string) => void;
}

/**
 * Headless explanation tree: derives collapsible rows from an explanation and wraps each in an
 * ARIA treeitem, delegating visible markup to the render-prop `children`. Collapse state is
 * owned internally.
 */
export function JustificationTree(props: {
  explanation: ExplanationNode;
  children: (row: JustificationRow) => ReactNode;
}): ReactNode {
  const view = useMemo(() => toExplanationView(props.explanation), [props.explanation]);
  const [collapsed, setCollapsed] = useState<ReadonlySet<string>>(() => new Set());

  const toggle = (id: string): void =>
    setCollapsed((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });

  const rows = flattenExplanation(view, collapsed);

  return (
    <div role="tree">
      {rows.map((row) => (
        <div
          key={row.id}
          role="treeitem"
          aria-level={row.depth + 1}
          aria-expanded={row.hasChildren ? !row.collapsed : undefined}
        >
          {props.children({ row, toggle })}
        </div>
      ))}
    </div>
  );
}
