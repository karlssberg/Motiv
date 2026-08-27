import type { Catalog } from '../contracts.js';
import { DSL_KEYWORDS, DSL_QUANTIFIERS, DSL_TYPES, PARAM_REST_CHARS, WORD_REST_CHARS, WORD_START_CHARS } from './lexer.js';

/**
 * What a completion offers. These are this package's own shapes — an editor integration maps
 * them onto its widget's types (`kind` onto its icon vocabulary, `boost` onto its ranking),
 * so the package takes no dependency on any editor, even at the type level.
 */
export type CompletionItemKind = 'spec' | 'collection' | 'quantifier' | 'keyword' | 'type' | 'parameter';

/** One completion option. */
export interface CompletionItem {
  label: string;
  kind: CompletionItemKind;
  /** The short annotation beside the label, e.g. `async · account is active`. */
  detail?: string;
  /** Ranking nudge relative to sibling options; higher sorts earlier. */
  boost?: number;
}

/** The completions for one word, and the range they would replace. */
export interface DslCompletion {
  /** Offset of the word's first character; a chosen option replaces `[from, cursor)`. */
  from: number;
  options: CompletionItem[];
  /**
   * Whether this list still answers for the word as further typing extends it — the editor can
   * keep filtering the same list instead of asking again.
   */
  isValidFor: (word: string) => boolean;
}

/**
 * The word shape a completion can replace: a parameter reference (`@` then the lexer's
 * non-dotted `PARAM_REST_CHARS` — params aren't namespaced) or a plain identifier
 * (`WORD_START_CHARS` then the dotted `WORD_REST_CHARS` — a spec name may be). Built from the
 * lexer's exported character classes rather than a hand-copied class, which is exactly how a
 * copy drifted out of sync with `tokenize` before: once dots were admitted to spec words in the
 * lexer, the copy silently kept stopping at the dot, so completion past a namespace dot
 * returned nothing. Anchored to the end of the searched slice, so it matches the word that
 * touches the cursor.
 */
const WORD_BEFORE_CURSOR = new RegExp(
  `(?:@[${PARAM_REST_CHARS}]*|[${WORD_START_CHARS}][${WORD_REST_CHARS}]*)$`,
);

/** How much of the line before the cursor is searched for the word being completed. */
const MAX_WORD_SCAN = 250;

/** Finds `param <name> :` declarations so their references can be offered. Parameter names are
 * not namespaced, so this deliberately uses the non-dotted `PARAM_REST_CHARS`, not `WORD_REST_CHARS`. */
const PARAMETER_DECLARATION = new RegExp(
  `\\bparam\\s+([${WORD_START_CHARS}][${PARAM_REST_CHARS}]*)\\s*:`, 'g',
);

/** Joins the non-empty parts of a detail line. */
function detail(...parts: Array<string | null | undefined>): string {
  return parts.filter((part) => !!part).join(' · ');
}

/** Catalog specs, marked when asynchronous and annotated with their description. */
function specOptions(catalog: Catalog): CompletionItem[] {
  return catalog.specs.map((spec) => ({
    label: spec.name,
    kind: 'spec' as const,
    detail: detail(spec.isAsync ? 'async' : null, spec.description),
    boost: 1,
  }));
}

/** Catalog collections, annotated with the element type they iterate. */
function collectionOptions(catalog: Catalog): CompletionItem[] {
  return catalog.collections.map((collection) => ({
    label: collection.path,
    kind: 'collection' as const,
    detail: detail('collection', `${collection.elementModelType}[]`),
  }));
}

/** The fixed vocabulary: quantifiers, keywords and parameter types, from the lexer's single definition. */
const VOCABULARY_OPTIONS: CompletionItem[] = [
  ...DSL_QUANTIFIERS.map((label) => ({ label, kind: 'quantifier' as const, detail: 'quantifier' })),
  ...DSL_KEYWORDS.map((label) => ({ label, kind: 'keyword' as const, detail: 'keyword' })),
  ...DSL_TYPES.map((label) => ({ label, kind: 'type' as const, detail: 'type' })),
];

/** The `@name` references declared by the document's own `param` statements, in declaration order. */
function parameterOptions(text: string): CompletionItem[] {
  const names = new Set<string>();
  for (const match of text.matchAll(PARAMETER_DECLARATION)) names.add(match[1]!);
  return [...names].map((name) => ({
    label: `@${name}`,
    kind: 'parameter' as const,
    detail: 'parameter',
  }));
}

/** The word touching the cursor, searched on the cursor's own line. */
function wordBefore(text: string, cursor: number): { from: number; text: string } | null {
  const lineStart = text.lastIndexOf('\n', cursor - 1) + 1;
  const scanStart = Math.max(lineStart, cursor - MAX_WORD_SCAN);
  const slice = text.slice(scanStart, cursor);
  const found = slice.search(WORD_BEFORE_CURSOR);
  if (found < 0) return null;
  return { from: scanStart + found, text: slice.slice(found) };
}

/**
 * A catalog-driven completion source for the Motiv DSL: the options for the word touching
 * `cursor`, already narrowed to the typed prefix so the source stays honest about what it
 * offers, or `null` when there is no word or nothing matches it.
 */
export function completeDsl(text: string, cursor: number, catalog: Catalog): DslCompletion | null {
  const word = wordBefore(text, cursor);
  if (!word) return null;

  const options = word.text.startsWith('@')
    ? parameterOptions(text)
    : [...specOptions(catalog), ...collectionOptions(catalog), ...VOCABULARY_OPTIONS];

  const prefix = word.text.toLowerCase();
  const matching = options.filter((option) => option.label.toLowerCase().startsWith(prefix));
  if (matching.length === 0) return null;

  return {
    from: word.from,
    options: matching,
    // Further typing keeps this list only while it still extends the prefix that produced it.
    isValidFor: (nextWord) => nextWord.toLowerCase().startsWith(prefix),
  };
}
