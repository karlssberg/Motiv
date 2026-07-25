import type { RuleDocument } from '../document.js';

/** The lexical class of a DSL token. */
export type TokenKind =
  | 'spec'        // is-active
  | 'ident'       // bare identifier (param names, collection paths)
  | 'keyword'     // param, in, as
  | 'type'        // integer, number, string, boolean
  | 'quantifier'  // all, any, exactly, atLeast, atMost
  | 'operator'    // && || & | ^ !
  | 'paren'       // ( )
  | 'brace'       // { }
  | 'colon'       // :
  | 'equals'      // =
  | 'string'      // "quota"
  | 'expression'  // `n > 0`
  | 'number'      // 3
  | 'paramRef'    // @minOrders
  | 'error';      // an unrecognised character

/** One lexed token with its half-open source range `[from, to)`. */
export interface Token {
  kind: TokenKind;
  /** Source text of the token, verbatim. */
  value: string;
  from: number;
  to: number;
}

/** A DSL-level error with a source range, mirroring the shape CodeMirror's linter wants. */
export interface DslError {
  from: number;
  to: number;
  /** Stable machine-readable code, e.g. `UnexpectedToken`. */
  code: string;
  message: string;
}

/** Maps a backend node path (e.g. `$.rule.andAlso[0]`) to the text range that produced it. */
export interface NodeSpan {
  path: string;
  from: number;
  to: number;
}

/** The outcome of parsing DSL text. */
export interface ParseResult {
  /** The parsed document; absent when a fatal syntax error prevented a full parse. */
  document?: RuleDocument;
  errors: DslError[];
  spans: NodeSpan[];
}
