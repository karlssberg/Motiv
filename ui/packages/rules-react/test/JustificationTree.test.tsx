import { describe, it, expect } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { createElement } from 'react';
import type { ExplanationNode } from '@motiv/rules-core';
import { JustificationTree } from '../src/JustificationTree.js';

const explanation: ExplanationNode = {
  assertions: ['AND'],
  underlying: [
    { assertions: ['is positive'], underlying: [] },
    { assertions: ['is even'], underlying: [{ assertions: ['divisible by 2'], underlying: [] }] },
  ],
};

describe('JustificationTree', () => {
  it('renders every row and collapses a subtree on toggle', () => {
    render(
      createElement(JustificationTree, {
        explanation,
        children: ({ row, toggle }) =>
          createElement(
            'button',
            { key: row.id, 'data-id': row.id, 'aria-level': row.depth + 1, onClick: () => toggle(row.id) },
            row.assertions.join(', '),
          ),
      }),
    );

    // 4 rows initially: AND, is positive, is even, divisible by 2
    expect(screen.getAllByRole('treeitem')).toHaveLength(4);

    // collapse the "is even" subtree (id '0.1') → its child 'divisible by 2' disappears
    fireEvent.click(screen.getByText('is even'));
    expect(screen.getAllByRole('treeitem')).toHaveLength(3);
    expect(screen.queryByText('divisible by 2')).toBeNull();
  });
});
