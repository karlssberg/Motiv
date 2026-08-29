import { describe, it, expect, afterEach } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { parseHash, formatHash, useHashRoute } from '../../src/routing/useHashRoute.js';

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

  it('reads the admin route', () => {
    expect(parseHash('#/admin')).toEqual({ page: 'admin', name: null });
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
      { page: 'admin', name: null },
    ] as const) {
      expect(parseHash(formatHash(route))).toEqual(route);
    }
  });
});

describe('useHashRoute', () => {
  afterEach(() => { window.history.replaceState(null, '', '#'); });

  it('reads the route out of the hash', () => {
    window.history.replaceState(null, '', '#/propositions/customer.is-active');

    const { result } = renderHook(() => useHashRoute());

    expect(result.current[0]).toEqual({ page: 'propositions', name: 'customer.is-active' });
  });

  it('resyncs to a hash that moved after the seed was read', () => {
    // The seed comes from a `useState` initialiser, and the listener is only attached once the
    // effect runs. Anything that moves the hash in between — a same-tick navigation, or React
    // 18 StrictMode double-invoking the initialiser — fires a `hashchange` that reaches nobody,
    // and the hook would go on reporting a route the address bar has already left.
    // `replaceState` is used deliberately: it changes the hash *without* firing `hashchange`, so
    // nothing but the effect's own resync can make this pass.
    window.history.replaceState(null, '', '#/rules');
    let moved = false;

    const { result } = renderHook(() => {
      const route = useHashRoute();
      if (!moved) {
        moved = true;
        window.history.replaceState(null, '', '#/propositions/customer.is-active');
      }
      return route;
    });

    expect(result.current[0]).toEqual({ page: 'propositions', name: 'customer.is-active' });
  });

  it('writes the route it is asked to navigate to into the address bar', () => {
    // The write is all `navigate` does: the `hashchange` it fires is what updates the state, and
    // jsdom queues that as a task rather than firing it inline — so this asserts the bar, and the
    // resync above covers the reading half.
    window.history.replaceState(null, '', '#/rules');
    const { result } = renderHook(() => useHashRoute());

    act(() => { result.current[1]({ page: 'propositions', name: 'customer.a' }); });

    expect(window.location.hash).toBe('#/propositions/customer.a');
  });
});
