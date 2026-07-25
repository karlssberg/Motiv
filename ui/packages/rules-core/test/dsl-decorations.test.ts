import { describe, it, expect } from 'vitest';
import { mergeDecorations } from '../src/dsl/decorations.js';
import { parse } from '../src/dsl/parser.js';
import { print } from '../src/dsl/printer.js';
import type { RuleDocument } from '../src/document.js';

describe('mergeDecorations', () => {
  it('re-attaches payloads when the structure is identical', () => {
    const prior: RuleDocument = {
      rule: { spec: 'is-active', whenTrue: 'yes', whenFalse: 'no' },
    };
    const parsed: RuleDocument = { rule: { spec: 'is-active' } };

    expect(mergeDecorations(parsed, prior)).toEqual({
      rule: { spec: 'is-active', whenTrue: 'yes', whenFalse: 'no' },
    });
  });

  it('re-attaches object payloads', () => {
    const prior: RuleDocument = {
      rule: { spec: 'is-active', name: 'a', whenTrue: { tier: 'gold' }, whenFalse: { tier: 'bronze' } },
    };
    const parsed: RuleDocument = { rule: { spec: 'is-active', name: 'a' } };

    expect(mergeDecorations(parsed, prior).rule).toMatchObject({
      whenTrue: { tier: 'gold' }, whenFalse: { tier: 'bronze' },
    });
  });

  it('merges payloads onto operands by indexed path', () => {
    const prior: RuleDocument = {
      rule: { andAlso: [{ spec: 'a', whenTrue: 'A' }, { spec: 'b', whenTrue: 'B' }] },
    };
    const parsed: RuleDocument = { rule: { andAlso: [{ spec: 'a' }, { spec: 'b' }] } };

    expect(mergeDecorations(parsed, prior).rule).toEqual({
      andAlso: [{ spec: 'a', whenTrue: 'A' }, { spec: 'b', whenTrue: 'B' }],
    });
  });

  it('drops a payload when the node kind at that path changed', () => {
    const prior: RuleDocument = { rule: { spec: 'is-active', whenTrue: 'yes' } };
    const parsed: RuleDocument = { rule: { not: { spec: 'is-active' } } };

    expect(mergeDecorations(parsed, prior)).toEqual({ rule: { not: { spec: 'is-active' } } });
  });

  it('drops a payload when the spec at that path changed', () => {
    const prior: RuleDocument = { rule: { spec: 'is-active', whenTrue: 'yes' } };
    const parsed: RuleDocument = { rule: { spec: 'is-verified' } };

    expect(mergeDecorations(parsed, prior)).toEqual({ rule: { spec: 'is-verified' } });
  });

  it('drops payloads for paths that no longer exist', () => {
    const prior: RuleDocument = {
      rule: { andAlso: [{ spec: 'a', whenTrue: 'A' }, { spec: 'b', whenTrue: 'B' }] },
    };
    const parsed: RuleDocument = { rule: { spec: 'a' } };

    expect(mergeDecorations(parsed, prior)).toEqual({ rule: { spec: 'a' } });
  });

  it('keeps the name from the parsed document, not the prior one', () => {
    const prior: RuleDocument = { rule: { spec: 'is-active', name: 'old', whenTrue: 'yes' } };
    const parsed: RuleDocument = { rule: { spec: 'is-active', name: 'new' } };

    expect(mergeDecorations(parsed, prior).rule).toMatchObject({ name: 'new', whenTrue: 'yes' });
  });

  it('keeps the parameters from the parsed document', () => {
    const prior: RuleDocument = {
      parameters: { old: { type: 'integer' } }, rule: { spec: 'a' },
    };
    const parsed: RuleDocument = {
      parameters: { fresh: { type: 'string' } }, rule: { spec: 'a' },
    };

    expect(mergeDecorations(parsed, prior).parameters).toEqual({ fresh: { type: 'string' } });
  });

  it('does not mutate either input', () => {
    const prior: RuleDocument = { rule: { spec: 'a', whenTrue: 'A' } };
    const parsed: RuleDocument = { rule: { spec: 'a' } };
    const priorCopy = structuredClone(prior);
    const parsedCopy = structuredClone(parsed);

    mergeDecorations(parsed, prior);

    expect(prior).toEqual(priorCopy);
    expect(parsed).toEqual(parsedCopy);
  });

  it('merges into a quantifier body by its node-key path', () => {
    const prior: RuleDocument = {
      rule: { asAllSatisfied: { spec: 'is-positive', whenTrue: 'ok' }, path: 'orders' },
    };
    const parsed: RuleDocument = {
      rule: { asAllSatisfied: { spec: 'is-positive' }, path: 'orders' },
    };

    expect(mergeDecorations(parsed, prior).rule).toMatchObject({
      asAllSatisfied: { spec: 'is-positive', whenTrue: 'ok' },
    });
  });

  it('does not alias a merged object payload with the prior document', () => {
    const prior: RuleDocument = { rule: { spec: 'a', whenTrue: { tier: 'gold' } } };
    const merged = mergeDecorations({ rule: { spec: 'a' } }, prior);

    expect(merged.rule.whenTrue).not.toBe(prior.rule.whenTrue);
  });

  it('survives a print/parse round-trip, as the editor does on every keystroke', () => {
    const original: RuleDocument = {
      parameters: { minOrders: { type: 'integer', default: 3 } },
      rule: {
        andAlso: [
          { spec: 'is-active', whenTrue: 'active', whenFalse: 'inactive' },
          { not: { spec: 'is-flagged', whenTrue: { code: 'FLAGGED' } } },
          {
            asAtLeastNSatisfied: { spec: 'is-positive', whenTrue: 'positive' },
            n: '@minOrders',
            path: 'orders',
            name: 'quota',
            whenFalse: 'not enough orders',
          },
        ],
      },
    };

    const result = parse(print(original));
    expect(result.errors).toEqual([]);

    expect(mergeDecorations(result.document!, original)).toEqual(original);
  });
});
