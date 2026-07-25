import { EditorView } from '@codemirror/view';

/** Binds the CodeMirror chrome to the app's design-token custom properties. */
export const motivEditorTheme = EditorView.theme({
  '&': {
    height: '100%',
    fontSize: '13.5px',
    backgroundColor: 'var(--dsl-bg)',
    color: 'var(--dsl-fg)',
  },
  '&.cm-focused': {
    outline: 'none',
  },
  '.cm-content': {
    fontFamily: 'var(--mono)',
    padding: '10px 0',
  },
  '.cm-gutters': {
    backgroundColor: 'var(--dsl-gutter-bg)',
    color: 'var(--dsl-gutter)',
    border: 'none',
    borderRight: '1px solid var(--border)',
  },
  // The active line is already implied by the caret; a wash would fight the token colours.
  '.cm-activeLine': {
    backgroundColor: 'transparent',
  },
  '.cm-activeLineGutter': {
    backgroundColor: 'transparent',
  },
  '.cm-tooltip': {
    backgroundColor: 'var(--dsl-tooltip-bg)',
    color: 'var(--dsl-tooltip-fg)',
    border: '1px solid var(--border)',
    borderRadius: '8px',
  },
});
