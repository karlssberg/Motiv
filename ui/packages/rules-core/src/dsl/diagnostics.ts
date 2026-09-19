import type { RuleError } from '../contracts.js';
import { isExpressionNode } from '../document.js';
import { getNode, isNodePath } from '../paths.js';
import { analyseLeaf } from '../expression/check.js';
import type { LeafScope } from '../expression/scope.js';
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
  severity: 'error' | 'warning';
  /** Stable machine-readable code, e.g. `UnexpectedToken`, `UnknownSpec`. */
  code: string;
  message: string;
  /**
   * The node path this diagnostic is about. Set for a backend error (which is keyed by path);
   * absent for every other parser diagnostic, which has no node yet.
   */
  path?: string;
}

/** Widens a range so it always covers at least one character, which marks a zero-width error. */
function nonEmpty({ from, to }: SourceRange): SourceRange {
  return { from, to: Math.max(to, from + 1) };
}

/** The severity a parser-level code maps to. Warnings carry their own code; anything else is an error. */
function severityOf(code: string): RuleDiagnostic['severity'] {
  if (code === 'ShadowsCatalog') return 'warning';
  return 'error';
}

/** A parser error or warning already carries native source offsets. */
function fromParserError(error: DslError): RuleDiagnostic {
  return {
    ...nonEmpty(error),
    severity: severityOf(error.code),
    code: error.code,
    message: error.message,
  };
}

/** Leaf text starts one past the opening backtick; a closed leaf ends one before the closing one. */
function leafOffset(span: NodeSpan): number {
  return span.from + 1;
}

/**
 * A backend error is keyed by node path, so it is mapped through the parse's spans. When the
 * error carries its own `range` and the path resolves to an expression-leaf node, that range is
 * interpreted relative to the leaf's text (offset by the opening backtick) instead of covering
 * the node's whole span.
 */
function fromBackendError(
  error: RuleError,
  spans: readonly NodeSpan[],
  documentLength: number,
): RuleDiagnostic {
  const span = spans.find((candidate) => candidate.path === error.path);
  const placed = error.range && span
    ? { from: leafOffset(span) + error.range.start, to: leafOffset(span) + error.range.end }
    : rangeOfPath(error.path, spans, documentLength);
  return {
    ...nonEmpty(placed),
    severity: 'error',
    code: error.code,
    message: error.message,
    path: error.path,
  };
}

/**
 * Runs `analyseLeaf` over every expression node in the document, offsetting each problem's range
 * by the leaf's position in the source text. A node whose path has no scope (`leafScope` returns
 * `null` — e.g. an ancestor quantifier couldn't be resolved) is skipped.
 */
function fromLeafProblems(
  result: ParseResult,
  leafScope: (path: string) => LeafScope | null,
): RuleDiagnostic[] {
  if (!result.document) return [];
  const out: RuleDiagnostic[] = [];
  for (const span of result.spans) {
    if (!isNodePath(span.path)) continue;
    const node = getNode(result.document, span.path);
    if (!node || !isExpressionNode(node)) continue;
    const scope = leafScope(span.path);
    if (!scope) continue;
    const offset = leafOffset(span);
    for (const problem of analyseLeaf(node.expression, scope).problems) {
      out.push({
        ...nonEmpty({ from: offset + problem.from, to: offset + problem.to }),
        severity: problem.warning ? 'warning' : 'error',
        code: problem.code,
        message: problem.message,
        path: span.path,
      });
    }
  }
  return out;
}

/**
 * Folds parser errors, parser warnings, client-side leaf problems and path-keyed backend errors
 * into one set of diagnostics. `leafScope` is optional — omitted, no leaf analysis runs and the
 * result is exactly the parser/backend diagnostics as before. When present, a client-side
 * diagnostic at the same range and code as a backend error is dropped in favour of the backend's
 * message, since the backend has already re-derived (and possibly refined) that same complaint.
 */
export function diagnosticsFor(
  text: string,
  result: ParseResult,
  errors: readonly RuleError[],
  leafScope?: (path: string) => LeafScope | null,
): RuleDiagnostic[] {
  const backend = errors.map((error) => fromBackendError(error, result.spans, text.length));
  const client = leafScope ? fromLeafProblems(result, leafScope) : [];
  const shadowed = (diagnostic: RuleDiagnostic): boolean =>
    backend.some((b) => b.from === diagnostic.from && b.to === diagnostic.to && b.code === diagnostic.code);
  return [
    ...result.errors.map(fromParserError),
    ...result.warnings.map(fromParserError),
    ...client.filter((diagnostic) => !shadowed(diagnostic)),
    ...backend,
  ];
}
