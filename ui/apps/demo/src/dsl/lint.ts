import type { Diagnostic } from '@codemirror/lint';
import {
  rangeOfPath,
  type DslError,
  type NodeSpan,
  type ParseResult,
  type RuleError,
  type SourceRange,
} from '@motiv/rules-core';

/** Separates a diagnostic's machine-readable code from its human message. */
const SEPARATOR = ': ';

/** Joins a code and a human message; {@link splitDiagnosticMessage} is the inverse. */
function joinDiagnosticMessage(code: string, message: string): string {
  return `${code}${SEPARATOR}${message}`;
}

/** Widens a range so it always covers at least one character, which marks a zero-width error. */
function nonEmpty({ from, to }: SourceRange): SourceRange {
  return { from, to: Math.max(to, from + 1) };
}

/** A parser error already carries native source offsets. */
function fromParserError(error: DslError): Diagnostic {
  return {
    ...nonEmpty(error),
    severity: 'error',
    message: joinDiagnosticMessage(error.code, error.message),
  };
}

/** A backend error is keyed by node path, so it is mapped through the parse's spans. */
function fromBackendError(
  error: RuleError,
  spans: readonly NodeSpan[],
  documentLength: number,
): Diagnostic {
  return {
    ...nonEmpty(rangeOfPath(error.path, spans, documentLength)),
    severity: 'error',
    source: error.path,
    message: joinDiagnosticMessage(error.code, error.message),
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
    ...errors.map((error) => fromBackendError(error, result.spans, text.length)),
  ];
}

/** Splits a message built by {@link diagnosticsFor} back into its code and human text. */
export function splitDiagnosticMessage(text: string): { code: string; message: string } {
  const index = text.indexOf(SEPARATOR);
  if (index < 0) return { code: '', message: text };
  return { code: text.slice(0, index), message: text.slice(index + SEPARATOR.length) };
}
