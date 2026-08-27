import { describe, it, expect } from 'vitest';
import { parse } from '../src/dsl/parser.js';
import { diagnosticsFor } from '../src/dsl/diagnostics.js';
import type { RuleError } from '../src/contracts.js';

describe('diagnosticsFor', () => {
  it('maps a parser error onto its source range, keeping code and message apart', () => {
    const text = '(is-active';
    const diagnostics = diagnosticsFor(text, parse(text), []);
    expect(diagnostics).toContainEqual(
      expect.objectContaining({ from: 0, to: 1, severity: 'error', code: expect.any(String) }),
    );
    for (const diagnostic of diagnostics) {
      expect(diagnostic.message).not.toContain(diagnostic.code);
    }
  });

  it('maps a backend error onto the span of its path, carrying the path', () => {
    const text = 'is-nonsense';
    const errors: RuleError[] = [
      { path: '$.rule', code: 'UnknownSpec', message: "'is-nonsense' is not a registered spec" },
    ];
    const diagnostics = diagnosticsFor(text, parse(text), errors);
    expect(diagnostics).toContainEqual(
      expect.objectContaining({
        from: 0, to: 11, severity: 'error', code: 'UnknownSpec', path: '$.rule',
        message: "'is-nonsense' is not a registered spec",
      }),
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

  it('returns no diagnostics for a clean document', () => {
    const text = 'is-active';
    expect(diagnosticsFor(text, parse(text), [])).toEqual([]);
  });

  it('never produces a zero-length range', () => {
    const text = '';
    for (const diagnostic of diagnosticsFor(text, parse(text), [])) {
      expect(diagnostic.to).toBeGreaterThan(diagnostic.from);
    }
  });
});
