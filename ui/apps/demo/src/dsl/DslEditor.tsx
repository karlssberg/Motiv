import { useEffect, useLayoutEffect, useMemo, useRef, useState, type CSSProperties } from 'react';
import { autocompletion, completionKeymap } from '@codemirror/autocomplete';
import { defaultKeymap, history, historyKeymap } from '@codemirror/commands';
import { lintKeymap, setDiagnostics } from '@codemirror/lint';
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
import { placePopover, type PopoverPlacement } from './popoverPlacement.js';
import type { DslSync, SyncStatus } from './useDslSync.js';

/** The document this demo edits. The DSL is file-shaped, so it is shown with a filename. */
const FILENAME = 'quota-rule.motiv';

/** What the sync pill says for each status. */
const PILL_TEXT: Record<SyncStatus, string> = {
  synced: 'synced',
  dirty: 'unsynced',
  error: 'parse error',
};

/** The spec node the popover is open for, and the token in the text it is anchored to. */
interface PopoverTarget {
  path: string;
  spec: string;
  /** Document position of the token's first character. */
  from: number;
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

/** The spec node the caret sits inside, or null when it is anywhere else. */
function popoverTargetAt(live: LiveContext, position: number): PopoverTarget | null {
  const span = innermostSpanAt(live.sync.parseResult.spans, position);
  if (!span) return null;
  const node = getNode(live.store.getState().document, span.path);
  if (!node || !isSpecNode(node)) return null;
  return { path: span.path, spec: node.spec, from: span.from };
}

/**
 * The token's box on screen, or null when it has none. A position that is not currently drawn
 * has no coordinates, and one past the end of the document throws outright — both of which the
 * caller treats the same way, as "unmeasurable".
 */
function tokenCoordsAt(view: EditorView, position: number): { top: number; bottom: number; left: number } | null {
  const clamped = Math.max(0, Math.min(position, view.state.doc.length));
  try {
    return view.coordsAtPos(clamped);
  } catch {
    return null;
  }
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
 * The view is built once on mount and never rebuilt: its extensions close over a ref holding the
 * latest render's callbacks, catalog and diagnostics, so none of them can go stale while the
 * editor keeps its own state (history, selection, scroll) across renders.
 */
export function DslEditor(props: { store: RuleEditorStore; catalog: Catalog; sync: DslSync }) {
  const { store, catalog, sync } = props;
  const editorState = useRuleEditor(store);
  const [popover, setPopover] = useState<PopoverTarget | null>(null);
  const [placement, setPlacement] = useState<PopoverPlacement | null>(null);

  const diagnostics = useMemo(
    () => diagnosticsFor(sync.text, sync.parseResult, editorState.errors),
    [sync.text, sync.parseResult, editorState.errors],
  );

  const live = useRef<LiveContext>({ sync, catalog, diagnostics, store });
  live.current = { sync, catalog, diagnostics, store };

  const frame = useRef<HTMLDivElement | null>(null);
  const host = useRef<HTMLDivElement | null>(null);
  const card = useRef<HTMLDivElement | null>(null);
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
        setPopover(popoverTargetAt(live.current, update.state.selection.main.head));
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

  // Diagnostics are pushed into the editor rather than polled by a `linter()` source. A lint
  // source only re-runs after a document change (and `forceLinting` is a no-op unless one is
  // already pending), so backend errors — which arrive without one — would never be marked.
  // `setDiagnostics` enables the lint extensions on first use, so no `linter()` is needed.
  useEffect(() => {
    const instance = view.current;
    if (!instance) return;
    instance.dispatch(setDiagnostics(instance.state, diagnostics));
  }, [diagnostics]);

  // The popover is anchored to its token, so it can only be placed once both it and the token
  // have been laid out — hence measuring here, after the card is in the DOM but before it is
  // painted. It renders hidden until then, so it is never seen in the wrong place.
  useLayoutEffect(() => {
    const frameEl = frame.current;
    const surfaceEl = host.current;
    const cardEl = card.current;
    if (!popover || !frameEl || !surfaceEl || !cardEl) {
      setPlacement(null);
      return;
    }

    const frameBox = frameEl.getBoundingClientRect();
    const cardBox = cardEl.getBoundingClientRect();
    // The toolbar sits above the editing surface, and the card must not cover it.
    const minTop = surfaceEl.getBoundingClientRect().top - frameBox.top;

    const coords = view.current && tokenCoordsAt(view.current, popover.from);
    const anchor = coords
      ? {
        top: coords.top - frameBox.top,
        bottom: coords.bottom - frameBox.top,
        left: coords.left - frameBox.left,
      }
      // Nothing to anchor to: fall back to the first row of the surface, which the clamping
      // below would have pulled it to anyway.
      : { top: minTop, bottom: minTop, left: 0 };

    setPlacement(placePopover(
      anchor,
      { width: cardBox.width, height: cardBox.height },
      { width: frameBox.width, height: frameBox.height, minTop },
    ));
  }, [popover]);

  const cardStyle: CSSProperties = placement
    ? { top: `${placement.top}px`, left: `${placement.left}px`, maxHeight: `${placement.maxHeight}px` }
    : { visibility: 'hidden' };

  return (
    <div className="dsl-frame" ref={frame}>
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
          cardRef={card}
          style={cardStyle}
        />
      )}
    </div>
  );
}
