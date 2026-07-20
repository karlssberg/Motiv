import type { ExplanationNode } from './contracts.js';

/** A causal-explanation node prepared for rendering. */
export interface ExplanationView {
  id: string;
  depth: number;
  assertions: string[];
  children: ExplanationView[];
}

/** One visible row of a (partially collapsed) explanation tree. */
export interface ExplanationRow {
  id: string;
  depth: number;
  assertions: string[];
  hasChildren: boolean;
  collapsed: boolean;
}

/** Converts a raw explanation tree into a view model with stable ids and depth. */
export function toExplanationView(root: ExplanationNode): ExplanationView {
  const build = (node: ExplanationNode, id: string, depth: number): ExplanationView => ({
    id,
    depth,
    assertions: node.assertions,
    children: node.underlying.map((child, i) => build(child, `${id}.${i}`, depth + 1)),
  });
  return build(root, '0', 0);
}

/** Flattens the view into visible rows, hiding descendants of collapsed ids. */
export function flattenExplanation(
  view: ExplanationView,
  collapsedIds: ReadonlySet<string>,
): ExplanationRow[] {
  const rows: ExplanationRow[] = [];
  const visit = (node: ExplanationView): void => {
    const collapsed = collapsedIds.has(node.id);
    rows.push({
      id: node.id,
      depth: node.depth,
      assertions: node.assertions,
      hasChildren: node.children.length > 0,
      collapsed,
    });
    if (!collapsed) node.children.forEach(visit);
  };
  visit(view);
  return rows;
}
