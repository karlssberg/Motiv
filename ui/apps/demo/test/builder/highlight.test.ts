import { describe, it, expect } from 'vitest';
import {
  EMPTY_HIGHLIGHT, focusedPath, setHovered, setSelected,
} from '../../src/builder/highlight.js';

describe('highlight model', () => {
  it('starts with nothing marked', () => {
    expect(EMPTY_HIGHLIGHT).toEqual({ hoveredPath: null, selectedPath: null, focus: null });
    expect(focusedPath(EMPTY_HIGHLIGHT)).toBeNull();
  });

  it('hovering takes focus', () => {
    const model = setHovered(EMPTY_HIGHLIGHT, '$.rule.and[0]');
    expect(model.hoveredPath).toBe('$.rule.and[0]');
    expect(focusedPath(model)).toBe('$.rule.and[0]');
  });

  it('selecting takes focus and does not clear the hover', () => {
    const model = setSelected(setHovered(EMPTY_HIGHLIGHT, '$.rule.and[0]'), '$.rule.and[1]');
    expect(model.hoveredPath).toBe('$.rule.and[0]');
    expect(model.selectedPath).toBe('$.rule.and[1]');
    expect(focusedPath(model)).toBe('$.rule.and[1]');
  });

  it('hovering after selecting takes focus back, selection intact', () => {
    const model = setHovered(setSelected(EMPTY_HIGHLIGHT, '$.rule.and[1]'), '$.rule.and[0]');
    expect(model.selectedPath).toBe('$.rule.and[1]');
    expect(focusedPath(model)).toBe('$.rule.and[0]');
  });

  it('leaving the tree hands focus back to the selection, after a real hover', () => {
    // Builds the transition the name describes: focus must be 'hover' at the moment of
    // leaving, so that handing it back to the selection is an observable change rather
    // than a no-op. Without the second setHovered, focus is already 'selection' and a
    // setHovered that ignored the null branch entirely would still pass.
    const hovered = setHovered(setSelected(setHovered(EMPTY_HIGHLIGHT, '$.rule.and[0]'), '$.rule.and[1]'), '$.rule.and[0]');
    expect(focusedPath(hovered)).toBe('$.rule.and[0]');

    const left = setHovered(hovered, null);

    expect(left.hoveredPath).toBeNull();
    expect(left.selectedPath).toBe('$.rule.and[1]');
    expect(left.focus).toBe('selection');
    expect(focusedPath(left)).toBe('$.rule.and[1]');
  });

  it('leaving with nothing selected leaves nothing focused', () => {
    const left = setHovered(setHovered(EMPTY_HIGHLIGHT, '$.rule'), null);

    // `focus` is asserted directly: with both paths null, focusedPath returns null
    // whichever side focus names, so it cannot discriminate the branch on its own.
    expect(left.focus).toBe('selection');
    expect(focusedPath(left)).toBeNull();
  });

  it('deselecting hands focus to the hover when there is one', () => {
    const model = setSelected(setHovered(setSelected(EMPTY_HIGHLIGHT, '$.rule'), '$.rule.and[0]'), null);
    expect(focusedPath(model)).toBe('$.rule.and[0]');
  });
});
