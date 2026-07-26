import { useEffect, useMemo, useRef, useState } from 'react';
import { autocompletion, completionKeymap } from '@codemirror/autocomplete';
import { defaultKeymap, history, historyKeymap } from '@codemirror/commands';
import { lintKeymap, setDiagnostics } from '@codemirror/lint';
import { EditorState } from '@codemirror/state';
import { EditorView, keymap, lineNumbers, type ViewUpdate } from '@codemirror/view';
import type { Diagnostic } from '@codemirror/lint';
import {
  getNode, isSpecNode,
  type Catalog, type NodeSpan, type RuleDocument, type RuleEditorStore,
} from '@motiv/rules-core';
import { useRuleEditor } from '@motiv/rules-react';
import { createMotivCompletion } from './completion.js';
import { diagnosticsFor } from './lint.js';
import { motivHover } from './hover.js';
import { motiv } from './motivLanguage.js';
import { motivEditorTheme } from './theme.js';
import { PayloadPopover } from './PayloadPopover.js';
import { payloadChips, setPayloadTargets, type PayloadTarget } from './payloadChips.js';
import { useAnchoredCard } from './useAnchoredCard.js';
import type { DslSync, SyncStatus } from './useDslSync.js';

/** The keystroke that opens the payload card for the spec node under the caret. */
const OPEN_PAYLOAD_KEY = 'Mod-.';

/** The document this demo edits. The DSL is file-shaped, so it is shown with a filename. */
const FILENAME = 'quota-rule.motiv';

/** What the sync pill says for each status. */
const PILL_TEXT: Record<SyncStatus, string> = {
  synced: 'synced',
  dirty: 'unsynced',
  error: 'parse error',
};

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
 * The spec node `span` covers, or null when it covers something else.
 *
 * The span comes from the parse of the buffer, but the node behind it is read from `document` —
 * the store's, which the buffer only commits to after a pause. The two therefore disagree for as
 * long as an edit is uncommitted. That is deliberate: the card edits the node the store holds, so
 * a target that named anything else would open a card onto decorations no one can save.
 */
function targetForSpan(document: RuleDocument, span: NodeSpan | undefined): PayloadTarget | null {
  if (!span) return null;
  const node = getNode(document, span.path);
  if (!node || !isSpecNode(node)) return null;
  return { path: span.path, spec: node.spec, from: span.from, to: span.to };
}

/** The spec node the caret sits inside, or null when it is anywhere else. */
function targetAtCaret(live: LiveContext, position: number): PayloadTarget | null {
  return targetForSpan(
    live.store.getState().document,
    innermostSpanAt(live.sync.parseResult.spans, position),
  );
}

/**
 * The DSL editing surface: a CodeMirror instance over the Motiv language, plus the toolbar, the
 * conflict banner and the payload popover.
 *
 * The buffer it edits is owned by the host, not by this component — `sync` comes in as a prop so
 * unmounting the surface (switching to another editing surface, say) discards only the view, and
 * uncommitted text, conflict state and any pending commit survive. `sync` must be the binding for
 * the same `store`, since the two are read as one.
 *
 * The payload card is opened deliberately — from a spec node's chip, or with `Mod-.` on the node
 * under the caret — and never by moving the caret itself. Clicking text is how you say where you
 * want to type, so it summons nothing over the text you were aiming at.
 *
 * The view is built once on mount and never rebuilt: its extensions close over a ref holding the
 * latest render's callbacks, catalog and diagnostics, so none of them can go stale while the
 * editor keeps its own state (history, selection, scroll) across renders.
 */
export function DslEditor(props: { store: RuleEditorStore; catalog: Catalog; sync: DslSync }) {
  const { store, catalog, sync } = props;
  const editorState = useRuleEditor(store);
  const [popover, setPopover] = useState<PayloadTarget | null>(null);

  const diagnostics = useMemo(
    () => diagnosticsFor(sync.text, sync.parseResult, editorState.errors),
    [sync.text, sync.parseResult, editorState.errors],
  );

  const live = useRef<LiveContext>({ sync, catalog, diagnostics, store });
  live.current = { sync, catalog, diagnostics, store };

  const toolbar = useRef<HTMLDivElement | null>(null);
  const host = useRef<HTMLDivElement | null>(null);
  const view = useRef<EditorView | null>(null);
  /** Set while pushing hook-produced text into the view, so the echo is not read back as an edit. */
  const applyingHookText = useRef(false);

  const card = useAnchoredCard({
    anchor: popover?.from ?? null,
    surface: host,
    // The card may float over the rest of the page, but not over the toolbar it belongs to.
    clearOf: toolbar,
    view,
  });

  /** Which node the card is open for, for the once-built extensions to read. */
  const openPath = useRef<string | null>(null);
  openPath.current = popover?.path ?? null;

  /** Returns the caret to the text once the card it was taken from is gone. */
  const closePopover = (): void => {
    setPopover(null);
    view.current?.focus();
  };

  /** A chip opens its node's card, and puts it away again when pressed a second time. */
  const toggleCard = (target: PayloadTarget): void => {
    if (openPath.current === target.path) closePopover();
    else setPopover(target);
  };

  useEffect(() => {
    const parent = host.current;
    if (!parent) return;

    const onUpdate = (update: ViewUpdate) => {
      if (!update.docChanged) return;
      if (!applyingHookText.current) live.current.sync.setText(update.state.doc.toString());
      // The card is anchored to a token and edits the node behind it — an edit can move the one
      // and delete the other, so it does not outlive the text it was opened against.
      setPopover(null);
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
          motivHover(() => live.current.diagnostics),
          // Reads `openPath` rather than closing over `popover`, since the extensions are built
          // once and would otherwise go on toggling against the state of the first render.
          payloadChips((target) => toggleCard(target)),
          // Ahead of the default bindings: nothing there claims this key today, and a future
          // default that did would otherwise shadow it silently.
          keymap.of([{
            key: OPEN_PAYLOAD_KEY,
            run: (editor) => {
              const target = targetAtCaret(live.current, editor.state.selection.main.head);
              if (!target) return false;
              setPopover(target);
              return true;
            },
          }]),
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

  // Diagnostics are pushed into the editor rather than polled by a `linter()` source. A lint
  // source only re-runs after a document change (and `forceLinting` is a no-op unless one is
  // already pending), so backend errors — which arrive without one — would never be marked.
  // `setDiagnostics` enables the lint extensions on first use, so no `linter()` is needed.
  useEffect(() => {
    const instance = view.current;
    if (!instance) return;
    instance.dispatch(setDiagnostics(instance.state, diagnostics));
  }, [diagnostics]);

  // Chips are pushed in for the same reason diagnostics are: the spans they sit on come from the
  // host's parse, which lands a render after the edit that provoked it, so the editor cannot
  // derive them for itself without parsing the text a second time.
  const targets = useMemo(
    () => sync.parseResult.spans.flatMap((span) => targetForSpan(editorState.document, span) ?? []),
    [sync.parseResult, editorState.document],
  );
  useEffect(() => {
    const instance = view.current;
    if (!instance) return;
    instance.dispatch({ effects: setPayloadTargets.of(targets) });
  }, [targets]);

  // The card renders hidden until it has been measured, and a hidden element cannot take focus —
  // so the keyboard is handed to it once it is placed rather than when it mounts.
  useEffect(() => {
    if (popover && card.placed) card.cardRef.current?.focus();
  }, [popover, card.placed]);

  return (
    <div className="dsl-frame">
      <div className="dsl-toolbar" ref={toolbar}>
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
          // The card holds an unsaved draft of the node it was opened for, seeded once from the
          // store. Keying it on the path remounts it when another node is opened, so a draft can
          // never be saved onto a node other than the one it was typed against.
          key={popover.path}
          store={store}
          catalog={catalog}
          path={popover.path}
          spec={popover.spec}
          onClose={closePopover}
          cardRef={card.cardRef}
          style={card.style}
        />
      )}
    </div>
  );
}
