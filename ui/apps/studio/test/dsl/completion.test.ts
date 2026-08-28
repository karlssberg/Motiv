import { describe, it, expect } from 'vitest';
import { EditorState } from '@codemirror/state';
import { CompletionContext } from '@codemirror/autocomplete';
import { createMotivCompletion } from '../../src/dsl/completion.js';
import type { Catalog } from '@motiv-rules/core';

const CATALOG: Catalog = {
  specs: [
    { name: 'is-active', modelType: 'customer', metadataType: 'String', isAsync: false, description: 'Currently active.', origin: 'Compiled' },
    { name: 'is-positive', modelType: 'order', metadataType: 'String', isAsync: false, description: 'Above zero.', origin: 'Compiled' },
    { name: 'is-premium', modelType: 'customer', metadataType: 'String', isAsync: true, description: 'Premium tier.', origin: 'Compiled' },
    { name: 'customer.has-orders', modelType: 'customer', metadataType: 'String', isAsync: false, description: 'Has placed an order.', origin: 'Compiled' },
  ],
  collections: [{ path: 'orders', parentModelType: 'customer', elementModelType: 'order' }],
};

/** Runs the completion source against a document whose caret sits at the end. */
function complete(text: string) {
  const state = EditorState.create({ doc: text, selection: { anchor: text.length } });
  const context = new CompletionContext(state, text.length, true);
  return createMotivCompletion(() => CATALOG)(context);
}

describe('createMotivCompletion', () => {
  it('offers specs matching the typed prefix', () => {
    const result = complete('is-p');
    expect(result?.options.map((o) => o.label)).toContain('is-positive');
    expect(result?.options.map((o) => o.label)).toContain('is-premium');
    expect(result?.options.map((o) => o.label)).not.toContain('is-active');
  });

  it('anchors the completion at the start of the typed word', () => {
    expect(complete('is-p')?.from).toBe(0);
  });

  it('carries the spec description as detail', () => {
    const option = complete('is-a')?.options.find((o) => o.label === 'is-active');
    expect(option?.detail).toContain('Currently active.');
  });

  it('marks an async spec', () => {
    const option = complete('is-pre')?.options.find((o) => o.label === 'is-premium');
    expect(option?.detail).toContain('async');
  });

  it('offers collections after the in keyword', () => {
    expect(complete('all in ord')?.options.map((o) => o.label)).toContain('orders');
  });

  it('offers quantifiers', () => {
    expect(complete('atL')?.options.map((o) => o.label)).toContain('atLeast');
  });

  it('offers keywords', () => {
    expect(complete('par')?.options.map((o) => o.label)).toContain('param');
  });

  it('offers parameter references declared in the document', () => {
    const labels = complete('param minOrders: integer = 3\n\natLeast(@min')?.options.map((o) => o.label);
    expect(labels).toContain('@minOrders');
  });

  it('does not offer specs when completing a parameter reference', () => {
    const labels = complete('param minOrders: integer = 3\n\natLeast(@min')?.options.map((o) => o.label);
    expect(labels).not.toContain('is-active');
  });

  it('returns null when there is no word to complete', () => {
    expect(complete('is-active ')).toBeNull();
  });

  it('offers a dotted spec once the prefix continues past its namespace dot', () => {
    const labels = complete('customer.has-')?.options.map((o) => o.label);
    expect(labels).toContain('customer.has-orders');
  });

  it('anchors a dotted completion at the start of the whole dotted word', () => {
    expect(complete('customer.has-')?.from).toBe(0);
  });

  it('offers nothing after a dot typed onto a parameter reference', () => {
    // Parameters are not namespaced, so `@minOrders.` cannot continue into anything. `WORD`'s two
    // alternatives both bear on this: the `@…` branch stops at the dot, and the identifier branch
    // then matches the bare `minOrders.` — which takes the *spec* path with that as its prefix and
    // matches no spec. The outcome is right either way, but only by way of that second branch, so
    // it is pinned rather than left to alternation order.
    expect(complete('param minOrders: integer = 3\n\natLeast(@minOrders.')).toBeNull();
  });
});
