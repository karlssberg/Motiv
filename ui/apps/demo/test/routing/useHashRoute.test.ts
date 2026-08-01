import { describe, it, expect } from 'vitest';
import { parseHash, formatHash } from '../../src/routing/useHashRoute.js';

describe('parseHash', () => {
  it('defaults to the rules page with no selection', () => {
    expect(parseHash('')).toEqual({ page: 'rules', name: null });
    expect(parseHash('#')).toEqual({ page: 'rules', name: null });
    expect(parseHash('#/')).toEqual({ page: 'rules', name: null });
  });

  it('reads a rule route', () => {
    expect(parseHash('#/rules/can-checkout')).toEqual({ page: 'rules', name: 'can-checkout' });
  });

  it('reads a proposition route with a dotted name', () => {
    expect(parseHash('#/propositions/customer.eligibility.is-active'))
      .toEqual({ page: 'propositions', name: 'customer.eligibility.is-active' });
  });

  it('reads a page with no selection', () => {
    expect(parseHash('#/propositions')).toEqual({ page: 'propositions', name: null });
  });

  it('decodes a percent-encoded name', () => {
    expect(parseHash('#/rules/a%20b')).toEqual({ page: 'rules', name: 'a b' });
  });

  it('falls back to rules for an unknown page', () => {
    expect(parseHash('#/nonsense/x')).toEqual({ page: 'rules', name: null });
  });

  it('keeps a malformed escape rather than throwing', () => {
    // `decodeURIComponent` throws URIError on a lone or truncated `%`, and parseHash runs inside a
    // useState initialiser — so a hand-typed or mis-pasted hash would throw during render and take
    // the whole app down. A name that cannot be decoded is far better read literally.
    expect(parseHash('#/rules/%')).toEqual({ page: 'rules', name: '%' });
    expect(parseHash('#/rules/a%20b%')).toEqual({ page: 'rules', name: 'a%20b%' });
    expect(parseHash('#/propositions/customer.%zz'))
      .toEqual({ page: 'propositions', name: 'customer.%zz' });
    // A lone continuation byte is well-formed percent-encoding but not valid UTF-8, and throws too.
    expect(parseHash('#/rules/%80')).toEqual({ page: 'rules', name: '%80' });
  });
});

describe('formatHash', () => {
  it('formats a page with no selection', () => {
    expect(formatHash({ page: 'propositions', name: null })).toBe('#/propositions');
  });

  it('formats a selection', () => {
    expect(formatHash({ page: 'rules', name: 'can-checkout' })).toBe('#/rules/can-checkout');
  });

  it('leaves dots unescaped so the hash stays readable', () => {
    expect(formatHash({ page: 'propositions', name: 'customer.is-active' }))
      .toBe('#/propositions/customer.is-active');
  });

  it('round-trips every route it formats', () => {
    for (const route of [
      { page: 'rules', name: null },
      { page: 'propositions', name: 'customer.a.b' },
      { page: 'rules', name: 'can-checkout' },
    ] as const) {
      expect(parseHash(formatHash(route))).toEqual(route);
    }
  });
});
