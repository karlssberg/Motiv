import {
  defineComponent, h, shallowRef, useId,
  type PropType, type SlotsType, type VNode,
} from 'vue';
import {
  toExplanationView,
  type ExplanationNode, type ExplanationRow, type ExplanationView,
} from '@motiv-rules/core';

/** A row surfaced to the default slot, plus a collapse toggle. */
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
 * The Vue counterpart of `@motiv-rules/react`'s `JustificationTree` — the one place in either
 * adapter where accessibility is inherited from a package rather than authored in the app.
 *
 * It is here because the tier table's price for a non-React runtime is otherwise wrong by exactly
 * this component: the bindings above are a mechanical port of a subscription, but a Vue adopter
 * who stops there loses the only a11y the packages carry and has to re-derive the structure from
 * the documentation. What that structure is — nested labelled groups rather than `role="tree"`,
 * each named by the assertion it explains, `aria-controls` dropped rather than dangling — is the
 * decision (ticket 18), and porting it costs the markup, not the decision.
 *
 * Everything visible is delegated to the default slot; the projection itself is
 * `toExplanationView` in core, shared by both adapters.
 */
export const JustificationTree = defineComponent({
  name: 'JustificationTree',
  props: {
    explanation: { type: Object as PropType<ExplanationNode>, required: true },
    /** The accessible name of the explanation as a whole. */
    label: { type: String, default: undefined },
  },
  slots: Object as SlotsType<{ default: JustificationRow }>,
  setup(props, { slots }) {
    const collapsed = shallowRef<ReadonlySet<string>>(new Set());
    const treeId = useId();

    const toggle = (id: string): void => {
      const next = new Set(collapsed.value);
      if (!next.delete(id)) next.add(id);
      collapsed.value = next;
    };

    /** The id of the group holding `node`'s causes. Scoped by `useId`, so two trees cannot collide. */
    const groupIdOf = (node: ExplanationView): string => `${treeId}-causes-${node.id}`;

    const renderNode = (node: ExplanationView): VNode => {
      const isCollapsed = collapsed.value.has(node.id);
      const hasChildren = node.children.length > 0;
      const mounted = hasChildren && !isCollapsed;
      const row: ExplanationRow = {
        id: node.id,
        depth: node.depth,
        assertions: node.assertions,
        hasChildren,
        collapsed: isCollapsed,
      };

      return h('div', { key: node.id }, [
        slots.default?.({ row, toggle, groupId: mounted ? groupIdOf(node) : null }),
        mounted
          ? h('div', {
            role: 'group',
            id: groupIdOf(node),
            // Omitted rather than emptied when the node carries no assertions, which the
            // `string[]` contract permits: an empty `aria-label` claims a name where there is
            // none, and assistive technologies disagree about what to do with that.
            'aria-label': node.assertions.join(', ') || undefined,
          }, node.children.map(renderNode))
          : null,
      ]);
    };

    return () => {
      // `??` would only catch null and undefined, and a caller's `label` is a string: `""` and a
      // whitespace-only string both reach the DOM as an empty accessible name. Blank means absent.
      const label = props.label?.trim() ? props.label : 'justification';
      return h('div', { role: 'group', 'aria-label': label }, [
        renderNode(toExplanationView(props.explanation)),
      ]);
    };
  },
});
