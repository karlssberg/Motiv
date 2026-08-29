import { describe, it, expect } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { createElement, Fragment } from 'react';
import type { ExplanationNode } from '@motiv-rules/core';
import { JustificationTree } from '../src/JustificationTree.js';

const explanation: ExplanationNode = {
  assertions: ['AND'],
  underlying: [
    { assertions: ['is positive'], underlying: [] },
    { assertions: ['is even'], underlying: [{ assertions: ['divisible by 2'], underlying: [] }] },
  ],
};

/** Renders each row as a button, so a row is both findable and clickable in one element. */
const renderTree = (props: Partial<Parameters<typeof JustificationTree>[0]> = {}) =>
  render(
    createElement(JustificationTree, {
      explanation,
      children: ({ row, toggle, groupId }) =>
        createElement(
          Fragment,
          null,
          createElement(
            'button',
            { 'data-id': row.id, 'data-group': groupId ?? '', onClick: () => toggle(row.id) },
            row.assertions.join(', '),
          ),
        ),
      ...props,
    }),
  );

describe('JustificationTree', () => {
  it('renders every row and collapses a subtree on toggle', () => {
    renderTree();
    expect(screen.getAllByRole('button')).toHaveLength(4);

    fireEvent.click(screen.getByText('is even'));
    expect(screen.getAllByRole('button')).toHaveLength(3);
    expect(screen.queryByText('divisible by 2')).toBeNull();
  });

  /**
   * Ticket 18: the same structure-plus-text treatment the builder gets. A flat run of sibling
   * `treeitem`s claims a nesting the DOM does not have, and the assertions — which are Motiv's own
   * generated text — are what actually carry the causal structure to a reader who cannot see the
   * indentation.
   */
  it('nests a labelled group under each cause, named by the assertion it explains', () => {
    renderTree();

    expect(screen.queryAllByRole('treeitem')).toHaveLength(0);
    const root = screen.getByRole('group', { name: 'AND' });
    const nested = screen.getByRole('group', { name: 'is even' });
    expect(root.contains(nested)).toBe(true);
    expect(nested.textContent).toBe('divisible by 2');
  });

  it('names the whole explanation, since a group with no name says nothing about itself', () => {
    renderTree({ label: 'why this rule was satisfied' });
    expect(screen.getByRole('group', { name: 'why this rule was satisfied' })).toBeDefined();
  });

  it('hands a row the id of the group it discloses, so a consumer can point a control at it', () => {
    renderTree();

    const parent = screen.getByText('is even');
    const groupId = parent.getAttribute('data-group');
    expect(groupId).not.toBe('');
    expect(document.getElementById(groupId!)).toBe(screen.getByRole('group', { name: 'is even' }));
  });

  it('offers no group id where there is no group — a leaf, or a collapsed row', () => {
    renderTree();
    expect(screen.getByText('is positive').getAttribute('data-group')).toBe('');

    fireEvent.click(screen.getByText('is even'));
    expect(screen.getByText('is even').getAttribute('data-group')).toBe('');
  });
});
