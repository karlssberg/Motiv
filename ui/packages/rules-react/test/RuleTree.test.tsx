import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { createElement } from 'react';
import { RuleEditorStore } from '@motiv/rules-core';
import { RuleEditorProvider } from '../src/context.js';
import { RuleTree } from '../src/RuleTree.js';

describe('RuleTree', () => {
  it('renders a treeitem per node with ARIA level from depth', () => {
    const store = new RuleEditorStore({ rule: { and: [{ spec: 'a' }, { not: { spec: 'b' } }] } });

    render(
      createElement(RuleEditorProvider, {
        store,
        children: createElement(RuleTree, {
          children: (item) =>
            createElement('span', { key: item.path, 'data-path': item.path, 'data-level': item.level }, item.path),
        }),
      }),
    );

    // role="tree" wrapper, one treeitem per node (4 nodes: root, and[0], and[1], and[1].not)
    expect(screen.getByRole('tree')).toBeDefined();
    const items = screen.getAllByRole('treeitem');
    expect(items).toHaveLength(4);
    expect(items[0]!.getAttribute('aria-level')).toBe('1');

    // deepest node ($.rule.and[1].not) has level 3
    const deepest = items.find((el) => el.textContent === '$.rule.and[1].not');
    expect(deepest!.getAttribute('aria-level')).toBe('3');
  });
});
