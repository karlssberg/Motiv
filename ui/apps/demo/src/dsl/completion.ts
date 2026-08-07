import type { Completion, CompletionContext, CompletionResult } from '@codemirror/autocomplete';
import { PARAM_REST_CHARS, WORD_REST_CHARS, WORD_START_CHARS, type Catalog } from '@motiv-rules/core';
import { DSL_KEYWORDS, DSL_QUANTIFIERS, DSL_TYPES } from './motivLanguage.js';

/**
 * The word shape a completion can replace: a parameter reference (`@` then the core lexer's
 * non-dotted `PARAM_REST_CHARS` — params aren't namespaced) or a plain identifier (`WORD_START_CHARS`
 * then the dotted `WORD_REST_CHARS` — a spec name may be). Built from `@motiv-rules/core`'s exported
 * character classes rather than a hand-copied class, which is exactly how this drifted out of sync
 * with `tokenize` before: once dots were admitted to spec words in the core lexer, this file's own
 * copy silently kept stopping at the dot, so completion past a namespace dot returned nothing.
 */
const WORD = new RegExp(`@[${PARAM_REST_CHARS}]*|[${WORD_START_CHARS}][${WORD_REST_CHARS}]*`);

/** Finds `param <name> :` declarations so their references can be offered. Parameter names are
 * not namespaced, so this deliberately uses the non-dotted `PARAM_REST_CHARS`, not `WORD_REST_CHARS`. */
const PARAMETER_DECLARATION = new RegExp(`\\bparam\\s+([${WORD_START_CHARS}][${PARAM_REST_CHARS}]*)\\s*:`, 'g');

/** Joins the non-empty parts of a detail line. */
function detail(...parts: Array<string | null | undefined>): string {
  return parts.filter((part) => !!part).join(' · ');
}

/** Catalog specs, marked when asynchronous and annotated with their description. */
function specOptions(catalog: Catalog): Completion[] {
  return catalog.specs.map((spec) => ({
    label: spec.name,
    type: 'variable',
    detail: detail(spec.isAsync ? 'async' : null, spec.description),
    boost: 1,
  }));
}

/** Catalog collections, annotated with the element type they iterate. */
function collectionOptions(catalog: Catalog): Completion[] {
  return catalog.collections.map((collection) => ({
    label: collection.path,
    type: 'namespace',
    detail: detail('collection', `${collection.elementModelType}[]`),
  }));
}

/** The fixed vocabulary: quantifiers, keywords and parameter types. */
const VOCABULARY_OPTIONS: Completion[] = [
  ...DSL_QUANTIFIERS.map((label) => ({ label, type: 'keyword', detail: 'quantifier' })),
  ...DSL_KEYWORDS.map((label) => ({ label, type: 'keyword', detail: 'keyword' })),
  ...DSL_TYPES.map((label) => ({ label, type: 'type', detail: 'type' })),
];

/** The `@name` references declared by the document's own `param` statements. */
function parameterOptions(text: string): Completion[] {
  const names = new Set<string>();
  for (const match of text.matchAll(PARAMETER_DECLARATION)) names.add(match[1]!);
  return [...names].map((name) => ({
    label: `@${name}`,
    type: 'constant',
    detail: 'parameter',
  }));
}

/**
 * A catalog-driven completion source for the Motiv DSL. The catalog is read through a getter
 * because it loads asynchronously, so the source always sees the latest one.
 */
export function createMotivCompletion(
  getCatalog: () => Catalog,
): (context: CompletionContext) => CompletionResult | null {
  return (context) => {
    const word = context.matchBefore(WORD);
    if (!word) return null;

    const catalog = getCatalog();
    const options = word.text.startsWith('@')
      ? parameterOptions(context.state.doc.toString())
      : [...specOptions(catalog), ...collectionOptions(catalog), ...VOCABULARY_OPTIONS];

    // CodeMirror filters only at render time, so the returned list is narrowed here to keep
    // the source honest about what it offers for the typed prefix.
    const prefix = word.text.toLowerCase();
    const matching = options.filter((option) => option.label.toLowerCase().startsWith(prefix));
    if (matching.length === 0) return null;

    return {
      from: word.from,
      options: matching,
      // Further typing keeps this list only while it still extends the prefix that produced it.
      validFor: (text) => text.toLowerCase().startsWith(prefix),
    };
  };
}
