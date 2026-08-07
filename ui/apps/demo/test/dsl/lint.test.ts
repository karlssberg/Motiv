import { describe, it, expect } from 'vitest';
import { parse } from '@motiv-rules/core';
import type { RuleError } from '@motiv-rules/core';
import { diagnosticsFor } from '../../src/dsl/lint.js';

describe('diagnosticsFor', () => {
  it('maps a parser error onto its source range', () => {
    const text = '(is-active';
    const diagnostics = diagnosticsFor(text, parse(text), []);
    expect(diagnostics).toContainEqual(
      expect.objectContaining({ from: 0, to: 1, severity: 'error' }),
    );
  });

  it('maps a backend error onto the span of its path', () => {
    const text = 'is-nonsense';
    const errors: RuleError[] = [
      { path: '$.rule', code: 'UnknownSpec', message: "'is-nonsense' is not a registered spec" },
    ];
    const diagnostics = diagnosticsFor(text, parse(text), errors);
    expect(diagnostics).toContainEqual(
      expect.objectContaining({ from: 0, to: 11, message: expect.stringContaining('registered') }),
    );
  });

  it('maps a backend error on a nested path onto that operand', () => {
    const text = 'is-active && is-nonsense';
    const errors: RuleError[] = [
      { path: '$.rule.andAlso[1]', code: 'UnknownSpec', message: 'unknown' },
    ];
    const diagnostics = diagnosticsFor(text, parse(text), errors);
    expect(diagnostics).toContainEqual(expect.objectContaining({ from: 13, to: 24 }));
  });

  it('anchors a sub-field error on its owning node', () => {
    const text = 'is-active && is-verified';
    const errors: RuleError[] = [
      { path: '$.rule.andAlso[1].whenTrue', code: 'MetadataTypeMismatch', message: 'bad payload' },
    ];
    const diagnostics = diagnosticsFor(text, parse(text), errors);
    expect(diagnostics).toContainEqual(expect.objectContaining({ from: 13, to: 24 }));
  });

  it('falls back to the whole document when a path has no span', () => {
    const text = 'is-active';
    const errors: RuleError[] = [
      { path: '$.rule.andAlso[7]', code: 'InvalidNode', message: 'nope' },
    ];
    const diagnostics = diagnosticsFor(text, parse(text), errors);
    expect(diagnostics).toContainEqual(expect.objectContaining({ from: 0, to: text.length }));
  });

  it('includes the error code in the message', () => {
    const text = 'is-nonsense';
    const errors: RuleError[] = [{ path: '$.rule', code: 'UnknownSpec', message: 'unknown' }];
    expect(diagnosticsFor(text, parse(text), errors)[0]!.message).toContain('UnknownSpec');
  });

  it('returns no diagnostics for a clean document', () => {
    const text = 'is-active';
    expect(diagnosticsFor(text, parse(text), [])).toEqual([]);
  });

  it('never produces a zero-length range', () => {
    const text = '';
    for (const d of diagnosticsFor(text, parse(text), [])) {
      expect(d.to).toBeGreaterThan(d.from);
    }
  });
});
