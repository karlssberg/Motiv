import { describe, it, expect } from 'vitest';
import { defineComponent, h } from 'vue';
import type { ExplanationNode } from '@motiv-rules/core';
import { JustificationTree, type JustificationRow } from '../src/JustificationTree.js';
import { mount } from './mount.js';

const explanation: ExplanationNode = {
  assertions: ['can checkout'],
  underlying: [
    { assertions: ['is active'], underlying: [] },
    { assertions: ['is adult'], underlying: [] },
  ],
};

/** Mounts the tree with a row renderer that draws the disclosure a consumer would draw. */
function renderTree(props: { explanation: ExplanationNode; label?: string }) {
  return mount(defineComponent({
    setup: () => () => h(JustificationTree, props, {
      default: ({ row, toggle, groupId }: JustificationRow) => h('button', {
        'data-id': row.id,
        ...(groupId ? { 'aria-controls': groupId } : {}),
        ...(row.hasChildren ? { 'aria-expanded': String(!row.collapsed) } : {}),
        onClick: () => toggle(row.id),
      }, row.assertions.join(', ')),
    }),
  }));
}

describe('JustificationTree', () => {
  it('names the whole explanation, and each group by the assertion it explains', () => {
    const { el, unmount } = renderTree({ explanation, label: 'why the order was declined' });

    const groups = [...el.querySelectorAll('[role="group"]')];
    expect(groups.map((group) => group.getAttribute('aria-label')))
      .toEqual(['why the order was declined', 'can checkout']);
    unmount();
  });

  it('falls back to a name when the label is blank', () => {
    const { el, unmount } = renderTree({ explanation, label: '   ' });

    expect(el.querySelector('[role="group"]')?.getAttribute('aria-label')).toBe('justification');
    unmount();
  });

  it('points aria-controls at the group it opens, and drops it when collapsed', async () => {
    const { el, unmount } = renderTree({ explanation });

    const root = el.querySelector<HTMLButtonElement>('button[data-id]')!;
    const groupId = root.getAttribute('aria-controls');
    expect(groupId).toBeTruthy();
    expect(el.querySelector(`[id="${groupId!}"]`)).not.toBeNull();
    expect(root.getAttribute('aria-expanded')).toBe('true');

    root.click();
    await Promise.resolve();

    const collapsed = el.querySelector<HTMLButtonElement>('button[data-id]')!;
    // The group is unmounted, so the IDREF goes with it: a reference to an absent element is an
    // invalid relationship, not a harmless one.
    expect(collapsed.getAttribute('aria-controls')).toBeNull();
    expect(collapsed.getAttribute('aria-expanded')).toBe('false');
    expect(el.querySelectorAll('button[data-id]')).toHaveLength(1);
    unmount();
  });

  it('leaves a group holding no assertions unnamed rather than emptily named', () => {
    const { el, unmount } = renderTree({
      explanation: { assertions: [], underlying: [{ assertions: ['is adult'], underlying: [] }] },
    });

    const nested = [...el.querySelectorAll('[role="group"]')][1]!;
    expect(nested.hasAttribute('aria-label')).toBe(false);
    unmount();
  });

  it('gives two trees on one page distinct group ids', () => {
    const { el, unmount } = mount(defineComponent({
      setup: () => () => h('div', [
        h(JustificationTree, { explanation }, { default: ({ groupId }: JustificationRow) => h('span', groupId ?? '') }),
        h(JustificationTree, { explanation }, { default: ({ groupId }: JustificationRow) => h('span', groupId ?? '') }),
      ]),
    }));

    const ids = [...el.querySelectorAll('[role="group"][id]')].map((group) => group.id);
    expect(new Set(ids).size).toBe(ids.length);
    unmount();
  });
});
