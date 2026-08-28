import { act } from '@testing-library/react';
import { EditorView } from '@codemirror/view';

/** The live CodeMirror view, so tests drive the editor the way a keystroke would. */
export function editorView(container: HTMLElement): EditorView {
  const view = EditorView.findFromDOM(container);
  if (!view) throw new Error('No CodeMirror view was mounted.');
  return view;
}

/** The editor's visible text. */
export function editorText(container: HTMLElement): string {
  return container.querySelector('.cm-content')?.textContent ?? '';
}

/** Types over the whole document, as a user replacing the buffer would. */
export function replaceBuffer(container: HTMLElement, text: string): void {
  const view = editorView(container);
  act(() => view.dispatch({ changes: { from: 0, to: view.state.doc.length, insert: text } }));
}
