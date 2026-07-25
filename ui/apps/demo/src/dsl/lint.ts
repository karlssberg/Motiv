import type { Diagnostic } from '@codemirror/lint';
import type { DslError, NodeSpan, ParseResult, RuleError } from '@motiv/rules-core';

/** Separates a diagnostic's machine-readable code from its human message. */
const SEPARATOR = ': ';

/** A half-open source range `[from, to)`. */
interface Range {
  from: number;
  to: number;
}

/** The path one level up, or null once the root is reached. */
function parentPath(path: string): string | null {
  const index = path.lastIndexOf('.');
  return index <= 0 ? null : path.slice(0, index);
}

/**
 * The span recorded for `path`, or for its nearest ancestor that has one — so a sub-field path
 * like `$.rule.whenTrue` anchors on the node that owns it. Falls back to the whole document.
 */
function rangeOfPath(path: string, spans: readonly NodeSpan[], text: string): Range {
  for (let current: string | null = path; current !== null; current = parentPath(current)) {
    const span = spans.find((candidate) => candidate.path === current);
    if (span) return { from: span.from, to: span.to };
  }
  return { from: 0, to: text.length };
}

/** Widens a range so it always covers at least one character, which marks a zero-width error. */
function nonEmpty({ from, to }: Range): Range {
  return { from, to: Math.max(to, from + 1) };
}

/** A parser error already carries native source offsets. */
function fromParserError(error: DslError): Diagnostic {
  return {
    ...nonEmpty(error),
    severity: 'error',
    message: `${error.code}${SEPARATOR}${error.message}`,
  };
}

/** A backend error is keyed by node path, so it is mapped through the parse's spans. */
function fromBackendError(error: RuleError, spans: readonly NodeSpan[], text: string): Diagnostic {
  return {
    ...nonEmpty(rangeOfPath(error.path, spans, text)),
    severity: 'error',
    source: error.path,
    message: `${error.code}${SEPARATOR}${error.message}`,
  };
}

/** Folds parser errors and path-keyed backend errors into one set of editor diagnostics. */
export function diagnosticsFor(
  text: string,
  result: ParseResult,
  errors: readonly RuleError[],
): Diagnostic[] {
  return [
    ...result.errors.map(fromParserError),
    ...errors.map((error) => fromBackendError(error, result.spans, text)),
  ];
}

/** Splits a message built by {@link diagnosticsFor} back into its code and human text. */
export function splitDiagnosticMessage(text: string): { code: string; message: string } {
  const index = text.indexOf(SEPARATOR);
  if (index < 0) return { code: '', message: text };
  return { code: text.slice(0, index), message: text.slice(index + SEPARATOR.length) };
}
