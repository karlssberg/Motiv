import { StateField } from '@codemirror/state';
import { Decoration, EditorView, type DecorationSet } from '@codemirror/view';
import { declaredLocals, tokenSpans } from '@motiv-rules/core';

/** How a reference to a declared `let` local is set apart from an ordinary spec word. */
const localMark = Decoration.mark({ class: 'cm-local' });

/**
 * The document's `local`-kind token runs, turned into `[from, to)` ranges — everything
 * `tokenSpans` doesn't already carry an offset for. `tokenSpans` tags a `let NAME`'s own
 * declaration the same `local` kind as every reference to it (it is, after all, the same word),
 * so the declaration is told apart here by what sits right before it: nothing else immediately
 * follows the `let` keyword. Marking it too would double up with whatever `let` itself is styled
 * as, for no benefit — a declaration already announces itself by being the target of a `let`.
 */
function localRanges(text: string): DecorationSet {
  const ranges: ReturnType<typeof localMark.range>[] = [];
  let cursor = 0;
  let previousKind: string | null = null;
  let previousValue = '';
  for (const span of tokenSpans(text, declaredLocals(text))) {
    const from = cursor;
    const to = cursor + span.value.length;
    const isDeclaration = previousKind === 'keyword' && previousValue === 'let';
    if (span.kind === 'local' && !isDeclaration) ranges.push(localMark.range(from, to));
    if (span.kind !== 'gap') {
      previousKind = span.kind;
      previousValue = span.value;
    }
    cursor = to;
  }
  return Decoration.set(ranges);
}

/**
 * Highlights references to declared `let` locals distinctly from catalog spec references (#234).
 *
 * This can't live in `motivLanguage.ts`'s stream parser: `StreamLanguage`'s `token` hook only
 * ever sees the current line, and "is this word a declared local" is a whole-document question —
 * the same one `tokenSpans`/`declaredLocals` already answer for the builder's rows. Rather than
 * re-solving it one line at a time inside the stream parser, this field asks the same function
 * directly, over the whole document, and turns its answer into marks.
 *
 * Recomputed only when the document changes — the buffer's set of declared locals doesn't move
 * just because the selection or viewport did.
 */
export const localMarks = StateField.define<DecorationSet>({
  create: (state) => localRanges(state.doc.toString()),
  update: (marks, transaction) => (
    transaction.docChanged ? localRanges(transaction.newDoc.toString()) : marks
  ),
  provide: (field) => EditorView.decorations.from(field),
});
