import { computed, toValue, type ComputedRef, type MaybeRefOrGetter } from 'vue';
import { errorsForNode, getNode, type RuleError, type RuleNode } from '@motiv-rules/core';
import { useRuleEditorStore } from './context.js';
import { useRuleEditor } from './useRuleEditor.js';

/** The node at a path plus the errors anchored on it. */
export interface RuleNodeView {
  node: RuleNode | undefined;
  errors: RuleError[];
}

/** Returns the node at a path (from the nearest provider's store) and its errors, reactively. */
export function useRuleNode(path: MaybeRefOrGetter<string>): ComputedRef<RuleNodeView> {
  const state = useRuleEditor(useRuleEditorStore());
  return computed(() => ({
    node: getNode(state.value.document, toValue(path)),
    errors: errorsForNode(state.value.errors, toValue(path)),
  }));
}
