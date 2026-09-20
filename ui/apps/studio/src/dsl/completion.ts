import type { Completion, CompletionContext, CompletionResult } from '@codemirror/autocomplete';
import { completeDsl, type Catalog, type CompletionItemKind, type LeafScope } from '@motiv-rules/core';

/**
 * How the package's own completion kinds render in CodeMirror's widget — the icon vocabulary is
 * CodeMirror's, so the mapping lives here in the editor integration, not in the package.
 */
const CM_TYPE: Record<CompletionItemKind, string> = {
  spec: 'variable',
  collection: 'namespace',
  quantifier: 'keyword',
  keyword: 'keyword',
  type: 'type',
  parameter: 'constant',
  local: 'variable',
  field: 'property',
  method: 'method',
  variable: 'variable',
};

/**
 * Adapts `@motiv-rules/core`'s editor-neutral completion source to CodeMirror. The catalog is
 * read through a getter because it loads asynchronously, so the source always sees the latest
 * one. The whole buffer is handed to the core source: it scopes the word match to the caret's
 * own line itself, and `param` declarations are scanned document-wide by design.
 *
 * `getLeafScope` is read through a getter for the same reason as the catalog — the resolver a
 * once-built extension closes over must always be the latest render's. It is optional because
 * only editors that know their model type (and so can resolve a leaf's expression scope) can
 * offer field/method completion inside a backtick; the source falls back to DSL-only completion
 * without it.
 */
export function createMotivCompletion(
  getCatalog: () => Catalog,
  getLeafScope: () => ((path: string) => LeafScope | null) | undefined = () => undefined,
): (context: CompletionContext) => CompletionResult | null {
  return (context) => {
    const completion = completeDsl(context.state.doc.toString(), context.pos, getCatalog(), getLeafScope());
    if (!completion) return null;

    return {
      from: completion.from,
      options: completion.options.map((option): Completion => ({
        label: option.label,
        type: CM_TYPE[option.kind],
        ...(option.detail !== undefined ? { detail: option.detail } : {}),
        ...(option.boost !== undefined ? { boost: option.boost } : {}),
        ...(option.insert !== undefined ? {
          apply: (view, _completion, from, to) => {
            const insert = option.insert!;
            const caret = from + (option.caretOffset ?? insert.length);
            view.dispatch({ changes: { from, to, insert }, selection: { anchor: caret } });
          },
        } : {}),
      })),
      validFor: (text) => completion.isValidFor(text),
    };
  };
}
