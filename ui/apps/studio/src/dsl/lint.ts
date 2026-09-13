import type { Diagnostic } from '@codemirror/lint';
import {
  diagnosticsFor as ruleDiagnosticsFor,
  type ParseResult,
  type RuleError,
} from '@motiv-rules/core';

/**
 * Separates a diagnostic's machine-readable code from its human message. The package keeps them
 * as separate fields; CodeMirror's `Diagnostic` has one `message` string, so joining them — and
 * splitting them back apart for the hover card — is this integration's own plumbing.
 */
const SEPARATOR = ': ';

/** Joins a code and a human message; {@link splitDiagnosticMessage} is the inverse. */
function joinDiagnosticMessage(code: string, message: string): string {
  return `${code}${SEPARATOR}${message}`;
}

/** Folds parser errors and path-keyed backend errors into CodeMirror diagnostics. */
export function diagnosticsFor(
  text: string,
  result: ParseResult,
  errors: readonly RuleError[],
): Diagnostic[] {
  return ruleDiagnosticsFor(text, result, errors).map((diagnostic): Diagnostic => ({
    from: diagnostic.from,
    to: diagnostic.to,
    severity: diagnostic.severity,
    message: joinDiagnosticMessage(diagnostic.code, diagnostic.message),
    ...(diagnostic.path !== undefined ? { source: diagnostic.path } : {}),
  }));
}

/** Splits a message built by {@link diagnosticsFor} back into its code and human text. */
export function splitDiagnosticMessage(text: string): { code: string; message: string } {
  const index = text.indexOf(SEPARATOR);
  if (index < 0) return { code: '', message: text };
  return { code: text.slice(0, index), message: text.slice(index + SEPARATOR.length) };
}
