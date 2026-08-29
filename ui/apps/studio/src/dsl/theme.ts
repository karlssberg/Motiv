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
    // CodeMirror paints the caret from whichever of its two base themes is active, and that is
    // fixed when the theme is built — while this one follows the page's colour scheme at runtime.
    // Left to the base theme the caret is black, which is invisible on a dark editor, so it is
    // bound to the same token as the text it sits in.
    caretColor: 'var(--dsl-fg)',
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
