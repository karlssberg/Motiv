import { describe, it, expect } from 'vitest';
import { parse } from '@motiv-rules/core';
import type { RuleError } from '@motiv-rules/core';
import { diagnosticsFor, splitDiagnosticMessage } from '../../src/dsl/lint.js';

// The mapping logic itself (ranges, span lookups, fallbacks) lives in @motiv-rules/core and is
// tested there; these cover only what this CodeMirror adapter adds — the joined message string
// and the `source` field.
describe('the CodeMirror lint adapter', () => {
  it('joins the code and message into the one string CodeMirror displays', () => {
    const text = 'is-nonsense';
    const errors: RuleError[] = [{ path: '$.rule', code: 'UnknownSpec', message: 'unknown' }];
    const [diagnostic] = diagnosticsFor(text, parse(text), errors);
    expect(diagnostic!.message).toBe('UnknownSpec: unknown');
    expect(diagnostic!.severity).toBe('error');
  });

  it('carries a backend error path as the diagnostic source, and no source for parser errors', () => {
    const backend = diagnosticsFor('is-a', parse('is-a'), [
      { path: '$.rule', code: 'UnknownSpec', message: 'x' } as RuleError,
    ]);
    expect(backend[0]!.source).toBe('$.rule');

    const parser = diagnosticsFor('(is-a', parse('(is-a'), []);
    expect(parser[0]!.source).toBeUndefined();
  });

  it('splitDiagnosticMessage inverts the join', () => {
    expect(splitDiagnosticMessage('UnknownSpec: not a registered spec'))
      .toEqual({ code: 'UnknownSpec', message: 'not a registered spec' });
    expect(splitDiagnosticMessage('no separator here'))
      .toEqual({ code: '', message: 'no separator here' });
  });
});
