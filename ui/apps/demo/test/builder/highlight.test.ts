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

  it('leaving the tree hands focus back to the selection', () => {
    const model = setHovered(setSelected(EMPTY_HIGHLIGHT, '$.rule.and[1]'), null);
    expect(model.hoveredPath).toBeNull();
    expect(focusedPath(model)).toBe('$.rule.and[1]');
  });

  it('leaving the tree with nothing selected leaves nothing focused', () => {
    expect(focusedPath(setHovered(setHovered(EMPTY_HIGHLIGHT, '$.rule'), null))).toBeNull();
  });

  it('deselecting hands focus to the hover when there is one', () => {
    const model = setSelected(setHovered(setSelected(EMPTY_HIGHLIGHT, '$.rule'), '$.rule.and[0]'), null);
    expect(focusedPath(model)).toBe('$.rule.and[0]');
  });
});
