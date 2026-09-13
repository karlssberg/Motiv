/**
 * Scrapes `let NAME = …` declarations out of raw DSL text, without a full parse. This is the
 * single source `tokenSpans` and `completeDsl` share for "what locals does this text declare" —
 * a regex scrape mirrors `completion.ts`'s `PARAMETER_DECLARATION`, deliberately using the
 * narrower `[A-Za-z0-9_-]` rest class (no dot) rather than the lexer's `WORD_REST_CHARS`, since a
 * local name is never namespaced.
 */
const LOCAL_DECLARATION = /\blet\s+([A-Za-z_][A-Za-z0-9_-]*)\s*=/g;

/** The names declared by every `let` statement in `text`, in no particular order. */
export function declaredLocals(text: string): Set<string> {
  const names = new Set<string>();
  for (const match of text.matchAll(LOCAL_DECLARATION)) names.add(match[1]!);
  return names;
}
