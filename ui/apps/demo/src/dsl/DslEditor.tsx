import { useEffect, useMemo, useRef, useState } from 'react';
import { autocompletion, completionKeymap } from '@codemirror/autocomplete';
import { defaultKeymap, history, historyKeymap } from '@codemirror/commands';
import { forceLinting, linter, lintKeymap } from '@codemirror/lint';
import { EditorState } from '@codemirror/state';
import { EditorView, keymap, lineNumbers, type ViewUpdate } from '@codemirror/view';
import type { Diagnostic } from '@codemirror/lint';
import { getNode, isSpecNode, type Catalog, type NodeSpan, type RuleEditorStore } from '@motiv/rules-core';
import { useRuleEditor } from '@motiv/rules-react';
import { createMotivCompletion } from './completion.js';
import { diagnosticsFor } from './lint.js';
import { motivHover } from './hover.js';
import { motiv } from './motivLanguage.js';
import { motivEditorTheme } from './theme.js';
import { PayloadPopover } from './PayloadPopover.js';
import { useDslSync, type DslSync, type SyncStatus } from './useDslSync.js';

/** The document this demo edits. The DSL is file-shaped, so it is shown with a filename. */
const FILENAME = 'quota-rule.motiv';

/** What the sync pill says for each status. */
const PILL_TEXT: Record<SyncStatus, string> = {
  synced: 'synced',
  dirty: 'unsynced',
  error: 'parse error',
};

/** The spec node the popover is open for. */
interface PopoverTarget {
  path: string;
  spec: string;
}

/** The values the (once-built) editor extensions read, always the latest render's. */
interface LiveContext {
  sync: DslSync;
  catalog: Catalog;
  diagnostics: Diagnostic[];
  store: RuleEditorStore;
}

/** The narrowest span covering `position` — the innermost node the caret sits inside. */
function innermostSpanAt(spans: readonly NodeSpan[], position: number): NodeSpan | undefined {
  let best: NodeSpan | undefined;
  for (const span of spans) {
    if (position < span.from || position > span.to) continue;
    if (!best || span.to - span.from < best.to - best.from) best = span;
  }
  return best;
}

/**
 * The DSL editing surface: a CodeMirror instance over the Motiv language, wired to the rule
 * store through {@link useDslSync}, plus the toolbar, the conflict banner and the payload popover.
 *
 * The view is built once on mount and never rebuilt: its extensions close over a ref holding the
 * latest render's callbacks, catalog and diagnostics, so none of them can go stale while the
 * editor keeps its own state (history, selection, scroll) across renders.
 */
export function DslEditor(props: { store: RuleEditorStore; catalog: Catalog }) {
  const { store, catalog } = props;
  const sync = useDslSync(store);
  const editorState = useRuleEditor(store);
  const [popover, setPopover] = useState<PopoverTarget | null>(null);

  const diagnostics = useMemo(
    () => diagnosticsFor(sync.text, sync.parseResult, editorState.errors),
    [sync.text, sync.parseResult, editorState.errors],
  );

  const live = useRef<LiveContext>({ sync, catalog, diagnostics, store });
  live.current = { sync, catalog, diagnostics, store };

  const host = useRef<HTMLDivElement | null>(null);
  const view = useRef<EditorView | null>(null);
  /** Set while pushing hook-produced text into the view, so the echo is not read back as an edit. */
  const applyingHookText = useRef(false);

  useEffect(() => {
    const parent = host.current;
    if (!parent) return;

    const onUpdate = (update: ViewUpdate) => {
      if (update.docChanged && !applyingHookText.current) {
        live.current.sync.setText(update.state.doc.toString());
      }
      if (update.selectionSet) {
        const { sync: current, store: currentStore } = live.current;
        const span = innermostSpanAt(current.parseResult.spans, update.state.selection.main.head);
        const node = span && getNode(currentStore.getState().document, span.path);
        setPopover(node && isSpecNode(node) ? { path: span.path, spec: node.spec } : null);
      }
    };

    const instance = new EditorView({
      parent,
      state: EditorState.create({
        doc: live.current.sync.text,
        extensions: [
          lineNumbers(),
          history(),
          motiv(),
          motivEditorTheme,
          autocompletion({ override: [createMotivCompletion(() => live.current.catalog)] }),
          linter(() => live.current.diagnostics),
          motivHover(() => live.current.diagnostics),
          keymap.of([...defaultKeymap, ...historyKeymap, ...completionKeymap, ...lintKeymap]),
          EditorView.updateListener.of(onUpdate),
        ],
      }),
    });
    view.current = instance;

    return () => {
      instance.destroy();
      view.current = null;
    };
  }, []);

  // Text the hook produced (a format, an external reprint, a conflict resolution) is pushed back
  // into the view. Dispatching when the text already matches would echo back through the update
  // listener and re-dirty the buffer, so an unchanged document is left alone.
  useEffect(() => {
    const instance = view.current;
    if (!instance || instance.state.doc.toString() === sync.text) return;
    applyingHookText.current = true;
    instance.dispatch({ changes: { from: 0, to: instance.state.doc.length, insert: sync.text } });
    applyingHookText.current = false;
  }, [sync.text]);

  // Backend errors arrive without a document change, which would otherwise leave the linter
  // idle until the next keystroke.
  useEffect(() => {
    if (view.current) forceLinting(view.current);
  }, [diagnostics]);

  return (
    <section aria-label="DSL" className="pane dsl-pane">
      <div className="dsl-toolbar">
        <span className="dsl-filename">{FILENAME}</span>
        <button type="button" onClick={sync.format}>Format</button>
        <span aria-label="sync status" className={`dsl-pill dsl-pill-${sync.status}`}>
          {PILL_TEXT[sync.status]}
        </span>
      </div>

      {sync.conflict && (
        <div className="dsl-conflict" role="alert">
          <span>The Builder changed this rule while you were editing the text.</span>
          <button type="button" onClick={sync.reformatFromTree}>Reformat from tree</button>
          <button type="button" onClick={sync.keepEditing}>Keep editing</button>
        </div>
      )}

      <div className="dsl-surface" ref={host} />

      {popover && (
        <PayloadPopover
          store={store}
          catalog={catalog}
          path={popover.path}
          spec={popover.spec}
          onClose={() => setPopover(null)}
        />
      )}
    </section>
  );
}
