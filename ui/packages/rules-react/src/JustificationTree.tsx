import {
  toExplanationView,
  type ExplanationNode, type ExplanationRow, type ExplanationView,
} from '@motiv-rules/core';
import { useId, useMemo, useState, type ReactNode } from 'react';

/** A row surfaced to a {@link JustificationTree} render prop, plus a collapse toggle. */
export interface JustificationRow {
  row: ExplanationRow;
  toggle: (id: string) => void;
  /**
   * The id of the group holding this row's causes, or `null` when there is no such group — a leaf
   * has none, and a collapsed row's is unmounted. A consumer drawing a disclosure control points
   * `aria-controls` at it, and drops the attribute when it is `null`: an IDREF naming an element
   * that is not in the document is an invalid relationship rather than a harmless one.
   */
  groupId: string | null;
}

/**
 * Headless explanation tree: renders an explanation as nested labelled groups, delegating all
 * visible markup to the render-prop `children`. Collapse state is owned internally.
 *
 * **Nested groups, not `role="tree"`** — the same structure the rule builder uses, for the same
 * reason (ticket 18). What this renders is a causal hierarchy whose every level is described by
 * text Motiv itself generated, and that text is what carries the structure to a reader who cannot
 * see the indentation; so each group is named by the assertion it explains, and entering one
 * announces what is being explained. The shape it replaces was a flat run of sibling `treeitem`s
 * distinguished only by `aria-level` — a nesting claimed in an attribute that the DOM did not have,
 * and the shape ARIA requires `aria-posinset`/`aria-setsize` for and it did not carry either.
 *
 * This is the lone place accessibility is inherited from a package rather than authored in the app:
 * the packages are headless everywhere else, so an adopter's own UI gets no markup from the SDK.
 */
export function JustificationTree(props: {
  explanation: ExplanationNode;
  /** The accessible name of the explanation as a whole. */
  label?: string;
  children: (row: JustificationRow) => ReactNode;
}): ReactNode {
  const view = useMemo(() => toExplanationView(props.explanation), [props.explanation]);
  const [collapsed, setCollapsed] = useState<ReadonlySet<string>>(() => new Set());
  const treeId = useId();

  const toggle = (id: string): void =>
    setCollapsed((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });

  /** The id of the group holding `node`'s causes. Scoped by `useId`, so two trees cannot collide. */
  const groupIdOf = (node: ExplanationView): string => `${treeId}-causes-${node.id}`;

  const renderNode = (node: ExplanationView): ReactNode => {
    const isCollapsed = collapsed.has(node.id);
    const hasChildren = node.children.length > 0;
    const mounted = hasChildren && !isCollapsed;
    const row: ExplanationRow = {
      id: node.id,
      depth: node.depth,
      assertions: node.assertions,
      hasChildren,
      collapsed: isCollapsed,
    };

    return (
      <div key={node.id}>
        {props.children({ row, toggle, groupId: mounted ? groupIdOf(node) : null })}
        {mounted && (
          <div
            role="group"
            id={groupIdOf(node)}
            // Omitted rather than emptied when the node carries no assertions, which the
            // `string[]` contract permits: an empty `aria-label` claims a name where there is
            // none, and assistive technologies disagree about what to do with that — some say
            // nothing, some fall back to the content — so the same group would read differently
            // in different readers. An unnamed group is at least unambiguously unnamed.
            aria-label={node.assertions.join(', ') || undefined}
          >
            {node.children.map(renderNode)}
          </div>
        )}
      </div>
    );
  };

  // `??` would only catch null and undefined, and a caller's `label` is a string: `""` and a
  // whitespace-only string both reach the DOM as an empty accessible name. Blank means absent.
  const label = props.label?.trim() ? props.label : 'justification';

  return (
    <div role="group" aria-label={label}>
      {renderNode(view)}
    </div>
  );
}
