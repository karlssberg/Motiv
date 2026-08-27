import type { RuleError } from '../contracts.js';
import { rangeOfPath, type SourceRange } from './spans.js';
import type { DslError, NodeSpan, ParseResult } from './types.js';

/**
 * A problem in a DSL buffer, anchored to a source range. This is the package's own diagnostic
 * shape — an editor integration maps it onto its linter's type — so `code` and `message` stay
 * separate fields rather than being joined into whatever one string that linter displays.
 */
export interface RuleDiagnostic {
  from: number;
  to: number;
  severity: 'error';
  /** Stable machine-readable code, e.g. `UnexpectedToken`, `UnknownSpec`. */
  code: string;
  message: string;
  /** The node path a backend error was keyed by; absent for a parser error, which has no node yet. */
  path?: string;
}

/** Widens a range so it always covers at least one character, which marks a zero-width error. */
function nonEmpty({ from, to }: SourceRange): SourceRange {
  return { from, to: Math.max(to, from + 1) };
}

/** A parser error already carries native source offsets. */
function fromParserError(error: DslError): RuleDiagnostic {
  return {
    ...nonEmpty(error),
    severity: 'error',
    code: error.code,
    message: error.message,
  };
}

/** A backend error is keyed by node path, so it is mapped through the parse's spans. */
function fromBackendError(
  error: RuleError,
  spans: readonly NodeSpan[],
  documentLength: number,
): RuleDiagnostic {
  return {
    ...nonEmpty(rangeOfPath(error.path, spans, documentLength)),
    severity: 'error',
    code: error.code,
    message: error.message,
    path: error.path,
  };
}

/** Folds parser errors and path-keyed backend errors into one set of diagnostics. */
export function diagnosticsFor(
  text: string,
  result: ParseResult,
  errors: readonly RuleError[],
): RuleDiagnostic[] {
  return [
    ...result.errors.map(fromParserError),
    ...errors.map((error) => fromBackendError(error, result.spans, text.length)),
  ];
}
