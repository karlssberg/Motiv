import { toValue, type MaybeRefOrGetter, type ShallowRef } from 'vue';
import type { EditorState, RuleEditorStore } from '@motiv-rules/core';
import { observe } from './observe.js';

/**
 * Subscribes the calling scope to a {@link RuleEditorStore} and returns its state as a ref.
 *
 * The store is accepted as a value, a ref or a getter: a consumer that never swaps stores passes
 * the store itself and pays nothing for the choice.
 */
export function useRuleEditor(store: MaybeRefOrGetter<RuleEditorStore>): Readonly<ShallowRef<EditorState>> {
  return observe([store], () => toValue(store)).state;
}
