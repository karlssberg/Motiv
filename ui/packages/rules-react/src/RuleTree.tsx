import { errorsForNode, listPaths, type RuleError, type RuleNode } from '@motiv/rules-core';
import type { ReactNode } from 'react';
import { useRuleEditorStore } from './context.js';
import { useRuleEditor } from './useRuleEditor.js';

/** One node surfaced to a {@link RuleTree} render prop. */
export interface RuleTreeItem {
  path: string;
  node: RuleNode;
  /** 1-based ARIA level (root = 1). */
  level: number;
  errors: RuleError[];
}

const ROOT = '$.rule';

function depthOf(path: string): number {
  if (path === ROOT) return 0;
  return path.slice(ROOT.length).split('.').filter(Boolean).length;
}

/**
 * Headless rule tree: flattens the current document (pre-order) and wraps each node in an
 * ARIA treeitem, delegating all visible markup to the render-prop `children`.
 */
export function RuleTree(props: { children: (item: RuleTreeItem) => ReactNode }): ReactNode {
  const store = useRuleEditorStore();
  const state = useRuleEditor(store);

  return (
    <div role="tree">
      {listPaths(state.document).map(({ path, node }) => (
        <div key={path} role="treeitem" aria-level={depthOf(path) + 1}>
          {props.children({ path, node, level: depthOf(path) + 1, errors: errorsForNode(state.errors, path) })}
        </div>
      ))}
    </div>
  );
}
