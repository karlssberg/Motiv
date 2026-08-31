import { describe, it, expect, vi } from 'vitest';
import { shallowRef } from 'vue';
import { RuleEditorStore } from '@motiv-rules/core';
import { provideRuleEditorStore, useRuleEditorStore } from '../src/context.js';
import { useRuleNode } from '../src/useRuleNode.js';
import { mountUnderProvider } from './mount.js';

describe('useRuleNode', () => {
  it('reads the node at a path and follows the store', () => {
    const store = new RuleEditorStore({ rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }] } });
    const { value: view, unmount } = mountUnderProvider(
      () => provideRuleEditorStore(store),
      () => useRuleNode('$.rule.and[1]'),
    );

    expect(view.value.node).toEqual({ spec: 'is-adult' });

    store.replaceNode('$.rule.and[1]', { spec: 'is-verified' });
    expect(view.value.node).toEqual({ spec: 'is-verified' });
    unmount();
  });

  it('follows a reactive path', () => {
    const store = new RuleEditorStore({ rule: { and: [{ spec: 'is-active' }, { spec: 'is-adult' }] } });
    const path = shallowRef('$.rule.and[0]');
    const { value: view, unmount } = mountUnderProvider(
      () => provideRuleEditorStore(store),
      () => useRuleNode(path),
    );

    expect(view.value.node).toEqual({ spec: 'is-active' });
    path.value = '$.rule.and[1]';
    expect(view.value.node).toEqual({ spec: 'is-adult' });
    unmount();
  });

  it('surfaces the errors anchored on the node and its sub-fields', () => {
    const store = new RuleEditorStore({ rule: { spec: 'is-active' } });
    const { value: view, unmount } = mountUnderProvider(
      () => provideRuleEditorStore(store),
      () => useRuleNode('$.rule'),
    );

    store.setErrors([
      { path: '$.rule', code: 'UnknownSpec', message: 'no such spec' },
      { path: '$.rule.whenTrue', code: 'UnknownSpec', message: 'bad decoration' },
      { path: '$.name', code: 'UnknownSpec', message: 'elsewhere' },
    ]);

    expect(view.value.errors.map((error) => error.path)).toEqual(['$.rule', '$.rule.whenTrue']);
    unmount();
  });

  it('refuses to run outside a provider', () => {
    // Vue also warns that `inject` was called outside `setup`. Both are true and the adapter's
    // message is the useful one, so the warning is silenced rather than asserted on.
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    expect(() => useRuleEditorStore()).toThrow(/provideRuleEditorStore/);
    warn.mockRestore();
  });
});
