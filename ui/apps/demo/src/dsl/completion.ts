import type { Completion, CompletionContext, CompletionResult } from '@codemirror/autocomplete';
import { completeDsl, type Catalog, type CompletionItemKind } from '@motiv-rules/core';

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
};

/**
 * Adapts `@motiv-rules/core`'s editor-neutral completion source to CodeMirror. The catalog is
 * read through a getter because it loads asynchronously, so the source always sees the latest
 * one. The whole buffer is handed to the core source: it scopes the word match to the caret's
 * own line itself, and `param` declarations are scanned document-wide by design.
 */
export function createMotivCompletion(
  getCatalog: () => Catalog,
): (context: CompletionContext) => CompletionResult | null {
  return (context) => {
    const completion = completeDsl(context.state.doc.toString(), context.pos, getCatalog());
    if (!completion) return null;

    return {
      from: completion.from,
      options: completion.options.map((option): Completion => ({
        label: option.label,
        type: CM_TYPE[option.kind],
        ...(option.detail !== undefined ? { detail: option.detail } : {}),
        ...(option.boost !== undefined ? { boost: option.boost } : {}),
      })),
      validFor: (text) => completion.isValidFor(text),
    };
  };
}
