import type { RuleErrorCode } from '../contracts.js';

/** The lexical category of one leaf token. */
export type LeafTokenKind = 'ident' | 'param' | 'number' | 'string' | 'keyword' | 'op' | 'punct' | 'end';

/** One lexed token of a leaf expression, with its half-open source range. */
export interface LeafToken { kind: LeafTokenKind; value: string; from: number; to: number }

/** A tokenize- or parse-time problem, anchored to a range in the leaf's source text. */
export interface LeafProblem { code: RuleErrorCode; message: string; from: number; to: number; warning?: true }

const KEYWORDS: ReadonlySet<string> = new Set(['null', 'true', 'false']);
const TWO_CHAR_OPS = ['=>', '==', '!=', '<=', '>=', '&&', '||'];

/** Lexes a leaf. Whitespace is dropped; the list always ends with an `end` token at the text's length. */
export function tokenizeLeaf(text: string): { tokens: LeafToken[]; problems: LeafProblem[] } {
  const tokens: LeafToken[] = [];
  const problems: LeafProblem[] = [];
  let i = 0;
  const push = (kind: LeafTokenKind, from: number): void => { tokens.push({ kind, value: text.slice(from, i), from, to: i }); };
  while (i < text.length) {
    const c = text[i]!;
    const start = i;
    if (/\s/.test(c)) { i++; continue; }
    if (/[A-Za-z_]/.test(c)) {
      while (i < text.length && /[A-Za-z0-9_]/.test(text[i]!)) i++;
      push(KEYWORDS.has(text.slice(start, i)) ? 'keyword' : 'ident', start);
      continue;
    }
    if (c === '@') {
      i++;
      while (i < text.length && /[A-Za-z0-9_]/.test(text[i]!)) i++;
      if (i === start + 1) problems.push({ code: 'InvalidExpression', message: "expected a parameter name after '@'", from: start, to: i });
      push('param', start);
      continue;
    }
    if (/[0-9]/.test(c)) {
      while (i < text.length && /[0-9]/.test(text[i]!)) i++;
      if (text[i] === '.' && /[0-9]/.test(text[i + 1] ?? '')) { i++; while (i < text.length && /[0-9]/.test(text[i]!)) i++; }
      push('number', start);
      continue;
    }
    if (c === '"') {
      i++;
      while (i < text.length && text[i] !== '"') i++;
      if (i >= text.length) {
        problems.push({ code: 'InvalidExpression', message: 'unterminated string', from: start, to: text.length });
        push('string', start);
        break;
      }
      i++;
      push('string', start);
      continue;
    }
    const two = text.slice(i, i + 2);
    if (TWO_CHAR_OPS.includes(two)) { i += 2; push('op', start); continue; }
    if ('<>!+-*/'.includes(c)) { i++; push('op', start); continue; }
    if ('().,'.includes(c)) { i++; push('punct', start); continue; }
    problems.push({ code: 'InvalidExpression', message: `unexpected character '${c}'`, from: start, to: start + 1 });
    i++;
  }
  tokens.push({ kind: 'end', value: '', from: text.length, to: text.length });
  return { tokens, problems };
}
