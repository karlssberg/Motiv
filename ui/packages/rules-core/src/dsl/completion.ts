import type { Catalog } from '../contracts.js';
import { isExpressionNode } from '../document.js';
import { getNode } from '../paths.js';
import type { LeafScope } from '../expression/scope.js';
import { completeLeaf } from '../expression/complete.js';
import { declaredLocals } from './locals.js';
import { parse } from './parser.js';
import { DSL_KEYWORDS, DSL_QUANTIFIERS, DSL_TYPES, PARAM_REST_CHARS, WORD_REST_CHARS, WORD_START_CHARS } from './lexer.js';

/**
 * What a completion offers. These are this package's own shapes — an editor integration maps
 * them onto its widget's types (`kind` onto its icon vocabulary, `boost` onto its ranking),
 * so the package takes no dependency on any editor, even at the type level.
 */
export type CompletionItemKind =
  | 'spec' | 'collection' | 'quantifier' | 'keyword' | 'type' | 'parameter' | 'local'
  | 'field' | 'method' | 'variable';

/** One completion option. */
export interface CompletionItem {
  label: string;
  kind: CompletionItemKind;
  /** The short annotation beside the label, e.g. `async · account is active`. */
  detail?: string;
  /** Ranking nudge relative to sibling options; higher sorts earlier. */
  boost?: number;
  /** Text to insert instead of `label`, e.g. a method template `where(o => o.)`. */
  insert?: string;
  /** Where to leave the caret inside `insert`, from its start; absent means at its end. */
  caretOffset?: number;
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

/**
 * The names declared by the document's own `let` statements — offered with a boost so a local
 * ranks above the catalog specs it can shadow, since a reference to it is almost always what the
 * author means once it exists. A local is only offered once its `let` line has actually been
 * typed; there is no forward-looking pre-scan here the way the parser's `collectLocalNames` does
 * one, because completion has no notion of "the rest of the preamble" — only the text so far.
 */
function localOptions(text: string): CompletionItem[] {
  return [...declaredLocals(text)].map((name) => ({
    label: name,
    kind: 'local' as const,
    detail: 'local',
    boost: 1,
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
export function completeDsl(
  text: string,
  cursor: number,
  catalog: Catalog,
  leafScope?: (path: string) => LeafScope | null,
): DslCompletion | null {
  const leaf = leafScope ? leafAt(text, cursor, leafScope) : null;
  if (leaf) {
    const inner = completeLeaf(text.slice(leaf.from, leaf.to), cursor - leaf.from, leaf.scope);
    return inner ? { ...inner, from: inner.from + leaf.from } : null;
  }

  const word = wordBefore(text, cursor);
  if (!word) return null;

  const options = word.text.startsWith('@')
    ? parameterOptions(text)
    : [...localOptions(text), ...specOptions(catalog), ...collectionOptions(catalog), ...VOCABULARY_OPTIONS];

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

/** The expression leaf whose text covers `cursor`, with its scope; null outside every backtick. */
function leafAt(
  text: string,
  cursor: number,
  leafScope: (path: string) => LeafScope | null,
): { from: number; to: number; scope: LeafScope } | null {
  // The common case: the rest of the document parses cleanly, so the real node paths (and thus
  // proper nested scoping, e.g. inside a quantifier body) are available directly.
  const direct = matchLeaf(parse(text), text, cursor, leafScope, text.length);
  if (direct !== undefined) return direct;

  // The author is typically mid-edit *inside* the very leaf touching the cursor — an unterminated
  // backtick, maybe inside an unterminated quantifier body — which the parser (rightly) refuses
  // to turn into a document. Heal the trailing open constructs with the minimal closing tokens so
  // the surrounding document parses, keeping real node paths (and therefore real nested scope)
  // available while typing. `to` stays clamped to the original text: nothing exists there yet.
  const healed = healUnterminated(text);
  if (healed.length > text.length) {
    const viaHeal = matchLeaf(parse(healed), healed, cursor, leafScope, text.length);
    if (viaHeal !== undefined) return viaHeal;
  }

  // A syntax error elsewhere in the document — one healing cannot fix — leaves the document
  // unavailable even after healing. Fall back to a textual search: the last unmatched backtick
  // before the cursor opens the leaf, scoped at the rule root.
  const head = text.slice(0, cursor);
  const backtickCount = (head.match(/`/g) ?? []).length;
  if (backtickCount % 2 === 0) return null;
  const from = head.lastIndexOf('`') + 1;
  const closeIndex = text.indexOf('`', cursor);
  const to = closeIndex === -1 ? text.length : closeIndex;
  const scope = leafScope('$.rule');
  return scope ? { from, to, scope } : null;
}

/**
 * Looks for the expression span covering `cursor` in a completed parse of `parsedText` (which
 * may be `text` itself, or `text` healed with trailing closing tokens appended). Returns
 * `undefined` — try the next strategy — when the parse failed outright; `null` when it succeeded
 * but no span covers the cursor; otherwise the leaf's bounds, clamped to `limit` (the length of
 * the text actually being edited, since a healed parse can place a span past it).
 */
function matchLeaf(
  result: ReturnType<typeof parse>,
  parsedText: string,
  cursor: number,
  leafScope: (path: string) => LeafScope | null,
  limit: number,
): { from: number; to: number; scope: LeafScope } | null | undefined {
  if (!result.document) return undefined;
  for (const span of result.spans) {
    const node = getNode(result.document, span.path);
    if (!node || !isExpressionNode(node)) continue;
    // The span covers the backticks; the leaf text sits one character inside each.
    const from = span.from + 1;
    const rawTo = parsedText[span.to - 1] === '`' && span.to - 1 > span.from ? span.to - 1 : span.to;
    const to = Math.min(rawTo, limit);
    if (cursor < from || cursor > to) continue;
    const scope = leafScope(span.path);
    return scope ? { from, to, scope } : null;
  }
  return null;
}

/**
 * Appends the minimal closing tokens — a backtick, then any open quantifier braces — needed to
 * heal a leaf or quantifier body an in-progress edit has left open, so the surrounding document
 * can still be parsed for scope purposes. Backtick-delimited leaf text is opaque to this scan, so
 * a brace inside one is never mistaken for a quantifier body's.
 */
function healUnterminated(text: string): string {
  let inLeaf = false;
  let braceDepth = 0;
  for (const char of text) {
    if (char === '`') { inLeaf = !inLeaf; continue; }
    if (inLeaf) continue;
    if (char === '{') braceDepth++;
    else if (char === '}') braceDepth = Math.max(0, braceDepth - 1);
  }
  return text + (inLeaf ? '`' : '') + '}'.repeat(braceDepth);
}
