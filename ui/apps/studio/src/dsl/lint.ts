import type { Diagnostic } from '@codemirror/lint';
import {
  diagnosticsFor as ruleDiagnosticsFor,
  type LeafScope,
  type NodeSpan,
  type ParseResult,
  type RuleError,
  type RuleLeafFact,
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
  leafScope?: (path: string) => LeafScope | null,
): Diagnostic[] {
  return ruleDiagnosticsFor(text, result, errors, leafScope).map((diagnostic): Diagnostic => ({
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

/**
 * A {@link RuleLeafFact} placed at document offsets. Facts arrive keyed by leaf path and a
 * range relative to that leaf's own text; placing them means finding the leaf's span and adding
 * its start. The fact's own `from` (the model field that fixed its type, or `null`) is renamed
 * `anchor` here so it does not collide with the document offset `from` every other placed range
 * in this editor uses.
 */
export type PlacedFact = Omit<RuleLeafFact, 'from'> & { anchor: string | null; from: number; to: number };

/** Facts arrive keyed by path and leaf-relative range; place them at document offsets through the parse's spans. */
export function placeFacts(facts: readonly RuleLeafFact[], spans: readonly NodeSpan[]): PlacedFact[] {
  return facts.flatMap((fact) => {
    const span = spans.find((s) => s.path === fact.path);
    if (!span) return [];
    const { from: anchor, ...rest } = fact;
    return [{ ...rest, anchor, from: span.from + 1 + fact.range.start, to: span.from + 1 + fact.range.end }];
  });
}
