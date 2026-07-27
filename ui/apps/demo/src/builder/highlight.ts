/**
 * Which node the strip marks, and which mark it scrolls to.
 *
 * Hover and selection coexist — you can select a parent and then run the pointer over its
 * children, which is the normal traffic pattern rather than an edge case. They are drawn on
 * different axes (selection underlines, hover fills) so a nested pair stays legible.
 *
 * `focus` records which of the two changed most recently, which is the scroll target. Keeping it
 * here rather than in the strip means the decision is made by the transition that knows the answer,
 * instead of reconstructed by comparing previous props during a render.
 */
export interface HighlightModel {
  hoveredPath: string | null;
  selectedPath: string | null;
  focus: 'hover' | 'selection' | null;
}

export const EMPTY_HIGHLIGHT: HighlightModel = {
  hoveredPath: null, selectedPath: null, focus: null,
};

/** Records the row under the pointer, or `null` on leaving the tree. */
export function setHovered(model: HighlightModel, path: string | null): HighlightModel {
  // Leaving hands focus back to the selection rather than leaving a dangling 'hover' focus
  // pointing at nothing.
  return { ...model, hoveredPath: path, focus: path === null ? 'selection' : 'hover' };
}

/** Records the selected row, or `null` on deselecting. */
export function setSelected(model: HighlightModel, path: string | null): HighlightModel {
  return { ...model, selectedPath: path, focus: path === null ? 'hover' : 'selection' };
}

/** The path the strip scrolls into view: the most recently changed of the two, if it is set. */
export function focusedPath(model: HighlightModel): string | null {
  return model.focus === 'hover' ? model.hoveredPath : model.selectedPath;
}
