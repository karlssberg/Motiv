import { errorsForNode, getNode, type RuleError, type RuleNode } from '@motiv/rules-core';
import { useRuleEditorStore } from './context.js';
import { useRuleEditor } from './useRuleEditor.js';

/** The node at a path plus the errors anchored on it. */
export interface RuleNodeView {
  node: RuleNode | undefined;
  errors: RuleError[];
}

/** Returns the node at a path (from the nearest provider's store) and its errors, reactively. */
export function useRuleNode(path: string): RuleNodeView {
  const store = useRuleEditorStore();
  const state = useRuleEditor(store);
  return {
    node: getNode(state.document, path),
    errors: errorsForNode(state.errors, path),
  };
}
