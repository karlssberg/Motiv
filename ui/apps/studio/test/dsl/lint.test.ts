import { describe, it, expect, vi } from 'vitest';
import { getNode, parse, RuleEditorStore } from '@motiv-rules/core';
import type { EditorView } from '@codemirror/view';
import type { RuleError } from '@motiv-rules/core';
import { diagnosticsFor, splitDiagnosticMessage } from '../../src/dsl/lint.js';

/** A stand-in for the CodeMirror view an action's `apply` reads text back out of. */
function fakeView(text: string): EditorView {
  return { state: { sliceDoc: (from: number, to: number) => text.slice(from, to) } } as unknown as EditorView;
}

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

  it('passes a warning through to CodeMirror as warning severity', () => {
    const text = 'let plain = x\n\nplain';
    const catalog = {
      specs: [{
        name: 'plain', modelType: 'customer', metadataType: 'String', isAsync: false,
        origin: 'Compiled' as const, parameters: null,
      }],
      collections: [],
    };
    const [diagnostic] = diagnosticsFor(text, parse(text, { catalog }), []);
    expect(diagnostic!.severity).toBe('warning');
  });

  it('splitDiagnosticMessage inverts the join', () => {
    expect(splitDiagnosticMessage('UnknownSpec: not a registered spec'))
      .toEqual({ code: 'UnknownSpec', message: 'not a registered spec' });
    expect(splitDiagnosticMessage('no separator here'))
      .toEqual({ code: '', message: 'no separator here' });
  });
});

// The `PreferLet` hint is the one diagnostic with a quick-fix: it names the node its `as` clause
// decorated, so `store.defineLocal` can turn the clause into a real `let`. The fix needs the store
// and the sync controller to act on, so it is only offered when a caller supplies both.
describe('the PreferLet quick-fix', () => {
  const text = 'is-a & (is-b as "b-name")';

  const preferLetIn = (diagnostics: ReturnType<typeof diagnosticsFor>) =>
    diagnostics.find((diagnostic) => diagnostic.message.startsWith('PreferLet'))!;

  it('is attached only to the PreferLet diagnostic, and only when store and sync are given', () => {
    const store = new RuleEditorStore(parse(text).document!);
    const sync = { reformatFromTree: vi.fn() };

    // No context: no diagnostic gets an action.
    for (const diagnostic of diagnosticsFor(text, parse(text), [])) {
      expect(diagnostic.actions).toBeUndefined();
    }

    // Context given: PreferLet gets one, everything else still gets none.
    const withContext = diagnosticsFor(text, parse(text), [], { store, sync });
    const preferLet = preferLetIn(withContext);
    expect(preferLet.actions).toHaveLength(1);
    expect(preferLet.actions![0]!.name).toBe('Extract to definition');

    const backendText = 'is-nonsense';
    const backend = diagnosticsFor(backendText, parse(backendText), [
      { path: '$.rule', code: 'UnknownSpec', message: 'unknown' } as RuleError,
    ], { store, sync });
    expect(backend.every((diagnostic) => diagnostic.actions === undefined)).toBe(true);
  });

  it('applying the action defines a local for the named node and reformats the buffer from the tree', () => {
    const store = new RuleEditorStore(parse(text).document!);
    const sync = { reformatFromTree: vi.fn() };
    const preferLet = preferLetIn(diagnosticsFor(text, parse(text), [], { store, sync }));

    preferLet.actions![0]!.apply(fakeView(text), preferLet.from, preferLet.to);

    expect(store.getState().document.definitions?.['b-name']).toEqual({ rule: { spec: 'is-b' } });
    expect(getNode(store.getState().document, '$.rule.and[1]')).toEqual({ local: 'b-name' });
    expect(sync.reformatFromTree).toHaveBeenCalledOnce();
  });

  it('does nothing when the extracted name is invalid — there is no lint-action UI to reprompt from', () => {
    const reservedText = 'is-a & (is-b as "let")';
    const store = new RuleEditorStore(parse(reservedText).document!);
    const sync = { reformatFromTree: vi.fn() };
    const preferLet = preferLetIn(diagnosticsFor(reservedText, parse(reservedText), [], { store, sync }));

    expect(() => preferLet.actions![0]!.apply(fakeView(reservedText), preferLet.from, preferLet.to))
      .not.toThrow();

    expect(store.getState().document).toEqual(parse(reservedText).document);
    expect(sync.reformatFromTree).not.toHaveBeenCalled();
  });
});
