import { useEffect, useMemo, useState } from 'react';
import type { RuleListEntry, RulesApiClient } from '@motiv-rules/core';
import { whyRuleSaveUnavailable } from '@motiv-rules/core/workflow';
import { useRuleEditor, useRuleEditorStore } from '@motiv-rules/react';
import { useRuleWorkflow } from '@motiv-rules/react/workflow';
import { MODEL_TYPE } from '../App.js';
import type { Page } from '../routing/useHashRoute.js';
import { AppBar } from './AppBar.js';
import { DocumentModal } from './DocumentModal.js';
import { DocumentTitle } from './DocumentTitle.js';
import { EditorPane } from './EditorPane.js';
import { EvaluatePane } from './EvaluatePane.js';
import { CheckoutPane } from './CheckoutPane.js';
import { CommandPalette } from '../shell/CommandPalette.js';
import { DocumentActions } from '../shell/DocumentActions.js';
import { ReportBanner } from '../shell/ReportBanner.js';
import { useCommandKey } from '../shell/useCommandKey.js';
import { rememberSelection } from '../shell/lastSelection.js';
import { IconOpen } from '../shell/icons.js';

/** One row of the rule palette. */
interface RuleOption {
  id: string;
  label: string;
}

/**
 * The rules page: the top bar, then the editor beside a rail of the two panes that *run* the rule —
 * Evaluate against a sample, and the live checkout the server decides.
 *
 * Which rule is open is route state, as it is on the propositions page: `App` hands it down as
 * `selected` and the palette writes it back through `onSelect`, so a deep link, a click and the
 * page switch's remembered link all take the same path. The save loop itself is
 * `RuleWorkflowController`'s; this renders it. With nothing selected the page shows an empty state
 * rather than the shared store's standing document — there is no local draft: a document with
 * nothing to save it back to could only mislead.
 *
 * Reports the picked rule's catalog entry via `onLoaded` so the shell can adapt (e.g. async
 * validation).
 */
export function RulesPage(props: {
  client: RulesApiClient;
  page: Page;
  selected: string | null;
  onSelect: (name: string | null) => void;
  onLoaded?: (entry: RuleListEntry | null) => void;
}) {
  const store = useRuleEditorStore();
  const { dirty } = useRuleEditor(store);
  const { rules, loaded, loadedEntry, conflict, failure, saving, refresh, load, save } =
    useRuleWorkflow(props.client, store);
  const [picking, setPicking] = useState(false);
  const [documentOpen, setDocumentOpen] = useState(false);

  // `refresh` and `load` are stable per (client, store) binding, so the listing loads once per
  // server world and the loaded rule follows the route.
  useEffect(() => { void refresh(); }, [refresh]);
  useEffect(() => { void load(props.selected); }, [load, props.selected]);
  // Remembered for the page switch to bring back — including its absence, so a closed rule is not
  // what the Rules link reopens.
  useEffect(() => { rememberSelection('rules', props.selected); }, [props.selected]);

  // What the shell adapts to is workflow state; the prop is just how it is handed over.
  const onLoaded = props.onLoaded;
  useEffect(() => { onLoaded?.(loadedEntry); }, [onLoaded, loadedEntry]);

  // The same shortcut the propositions page opens its palette with — one implementation, so the
  // two cannot drift into meaning different things.
  useCommandKey(() => setPicking(true));

  // Memoised because the palette filters against `items` by identity: a fresh array on every
  // render would re-run the match over the whole listing on every keystroke.
  const options = useMemo(
    (): RuleOption[] => rules.map((rule) => ({ id: rule.name, label: rule.name })),
    [rules],
  );

  return (
    <>
      <AppBar
        page={props.page}
        controls={
          <DocumentActions
            kind="rule"
            name={loaded?.name ?? null}
            dirty={dirty}
            saveUnavailable={whyRuleSaveUnavailable({ loaded, saving })}
            onOpen={() => setPicking(true)}
            onJson={() => setDocumentOpen(true)}
            onSave={save}
            onClose={() => props.onSelect(null)}
          />
        }
      />
      {/*
        Reported first because it is always the newer event: only a save records a conflict, and
        every operation clears the failure on its way out — so a failure standing beside a
        conflict was necessarily raised after it.
      */}
      {failure !== null && (
        <ReportBanner {...(loaded ? { onReload: () => void load(loaded.name) } : {})}>
          {failure}
        </ReportBanner>
      )}
      {conflict !== null && loaded && (
        <ReportBanner onReload={() => void load(loaded.name)}>
          Someone else saved version {conflict} of “{loaded.name}”.
        </ReportBanner>
      )}

      {/*
        Each pane below fetches GET /catalog on mount (EditorPane and EvaluatePane via useCatalog,
        CheckoutPane directly) — and EditorPane's builder surface fetches once more of its own, so
        up to four requests for the same static payload. Deduping would mean lifting the catalog
        here and passing it down, but each pane's self-contained wiring is a deliberate seam Studio
        exists to show, so the duplicate requests are accepted.
      */}
      {loaded ? (
        <div className="shell-body">
          <EditorPane
            client={props.client}
            documentName={loaded.name}
            title={
              <DocumentTitle
                name={loaded.name}
                modelType={MODEL_TYPE}
                version={loaded.version}
                note={loaded.isCodeDefault ? 'code-defined default (builder starts fresh)' : undefined}
              />
            }
          />
          {/*
            One rail for both ways of running the rule. Checkout used to be a full-width footer
            under the columns; beside Evaluate it shares one result language — a verdict, then the
            assertions — and the editor keeps the height it needs.
          */}
          <div className="rail">
            <EvaluatePane client={props.client} />
            <CheckoutPane client={props.client} />
          </div>
        </div>
      ) : (
        <section className="empty-state" aria-label="No rule open">
          <h2>No rule open</h2>
          <p>Choose one of the live rules to edit it here. Rules are declared in code; what you save is the document that overrides the default.</p>
          <div className="run-row">
            <button type="button" className="btn" onClick={() => setPicking(true)}>
              <IconOpen size={14} />Choose a rule<kbd aria-hidden="true">⌘K</kbd>
            </button>
          </div>
        </section>
      )}

      {picking && (
        <CommandPalette<RuleOption>
          label="Rules"
          placeholder="Filter rules"
          items={options}
          match={(option, needle) => option.label.toLowerCase().includes(needle.toLowerCase())}
          renderItem={(option) => <span className="palette-name">{option.label}</span>}
          onChoose={(option) => { props.onSelect(option.id); setPicking(false); }}
          onClose={() => setPicking(false)}
        />
      )}
      {documentOpen && <DocumentModal onClose={() => setDocumentOpen(false)} />}
    </>
  );
}
