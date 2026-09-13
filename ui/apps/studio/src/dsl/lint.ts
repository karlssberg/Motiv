import type { Action, Diagnostic } from '@codemirror/lint';
import {
  diagnosticsFor as ruleDiagnosticsFor,
  finishLocalName,
  type ParseResult,
  type RuleError,
  type RuleEditorStore,
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

/** What {@link diagnosticsFor}'s quick-fix needs: the store to act on, and the sync controller to
 *  re-print the buffer once it has. A minimal shape rather than `@motiv-rules/react`'s `DslSync`,
 *  since the fix touches nothing else on it — and a test stubs it with a bare `reformatFromTree`. */
export interface QuickFixContext {
  store: RuleEditorStore;
  sync: { reformatFromTree: () => void };
}

/**
 * The `PreferLet` hint's quick-fix: turns the `as "…"` clause the diagnostic covers into a real
 * `let` declaration for the node it names — `defineLocal(path, …)` is exactly the move the hint
 * is nudging towards. `path` is the diagnostic's own `path` (carried through as `source` above),
 * which is where the parser attached it for precisely this.
 *
 * There is no UI here to reprompt for a name a lint action rejected — the buffer's `as` text is
 * all `apply` has to go on — so an invalid or already-taken name (caught by `defineLocal`, which
 * validates and throws) is left exactly as it was: the hint stays, and the user can rename by
 * hand from the Builder instead.
 */
function preferLetAction(path: string, { store, sync }: QuickFixContext): Action {
  return {
    name: 'Extract to definition',
    apply(view, from, to) {
      const match = /as\s+"([^"]*)"/.exec(view.state.sliceDoc(from, to));
      if (!match) return;
      const name = finishLocalName(match[1]!);
      if (!name) return;
      try {
        store.defineLocal(path, name);
      } catch {
        return;
      }
      sync.reformatFromTree();
    },
  };
}

/**
 * Folds parser errors and path-keyed backend errors into CodeMirror diagnostics. `context`, when
 * given, attaches the `PreferLet` quick-fix — omitted, every diagnostic maps across with no
 * `actions` at all, which is what a caller with no store/sync to act through should get.
 */
export function diagnosticsFor(
  text: string,
  result: ParseResult,
  errors: readonly RuleError[],
  context?: QuickFixContext,
): Diagnostic[] {
  return ruleDiagnosticsFor(text, result, errors).map((diagnostic): Diagnostic => ({
    from: diagnostic.from,
    to: diagnostic.to,
    severity: diagnostic.severity,
    message: joinDiagnosticMessage(diagnostic.code, diagnostic.message),
    ...(diagnostic.path !== undefined ? { source: diagnostic.path } : {}),
    ...(diagnostic.code === 'PreferLet' && diagnostic.path !== undefined && context
      ? { actions: [preferLetAction(diagnostic.path, context)] }
      : {}),
  }));
}

/** Splits a message built by {@link diagnosticsFor} back into its code and human text. */
export function splitDiagnosticMessage(text: string): { code: string; message: string } {
  const index = text.indexOf(SEPARATOR);
  if (index < 0) return { code: '', message: text };
  return { code: text.slice(0, index), message: text.slice(index + SEPARATOR.length) };
}
