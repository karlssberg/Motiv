import { useEffect, useMemo, useState } from 'react';
import type { RuleListEntry, RulesApiClient } from '@motiv-rules/core';
import { whyRuleSaveUnavailable } from '@motiv-rules/core/workflow';
import { useRuleEditorStore } from '@motiv-rules/react';
import { useRuleWorkflow } from '@motiv-rules/react/workflow';
import { MODEL_TYPE } from '../App.js';
import type { Page } from '../routing/useHashRoute.js';
import { AppBar } from './AppBar.js';
import { DocumentModal } from './DocumentModal.js';
import { CommandPalette } from '../shell/CommandPalette.js';
import { ReportBanner } from '../shell/ReportBanner.js';
import { Toolbar } from '../shell/Toolbar.js';
import { useCommandKey } from '../shell/useCommandKey.js';
import { IconJson, IconOpen, IconSave } from '../shell/icons.js';

/**
 * One row of the rule palette: the name the workflow loads by, and the text the row reads as.
 * They are the same string for every server rule, and deliberately not for the local draft.
 */
interface RuleOption {
  id: string;
  label: string;
}

/**
 * The palette's first row: nothing loaded from the server, so nothing to save back to it. The
 * empty id is what the choose handler reads as "unload" — the document in the editor is left
 * alone, since it is the draft either way — and the label stands in for the name it has not got.
 */
const LOCAL_DRAFT: RuleOption = { id: '', label: 'local draft' };

/**
 * Seam: dynamic replacement. Picks a live server rule, loads its document into the shared
 * editor store, and saves it back with the loaded version — a stale version surfaces as a
 * conflict banner (open two tabs to watch the race protection work), and anything the API does
 * not model as an outcome surfaces as a failure banner beside it. The save loop itself is
 * `RuleWorkflowController`'s; this renders it. Reports the picked rule's catalog entry via
 * onLoaded so the shell can adapt (e.g. async validation).
 */
export function RuleHeader(props: {
  client: RulesApiClient;
  onLoaded?: (entry: RuleListEntry | null) => void;
  page: Page;
}) {
  const store = useRuleEditorStore();
  const { rules, loaded, loadedEntry, conflict, failure, saving, refresh, load, save } =
    useRuleWorkflow(props.client, store);
  const [picking, setPicking] = useState(false);
  const [documentOpen, setDocumentOpen] = useState(false);
  // Memoised because the palette filters against `items` by identity: a fresh array on every
  // render would re-run the match over the whole listing on every keystroke.
  const options = useMemo(
    (): RuleOption[] => [LOCAL_DRAFT, ...rules.map((rule) => ({ id: rule.name, label: rule.name }))],
    [rules],
  );

  // `refresh` is stable per (client, store) binding, so this fetches once per server world.
  useEffect(() => { void refresh(); }, [refresh]);

  // What the shell adapts to is workflow state; the prop is just how it is handed over.
  const onLoaded = props.onLoaded;
  useEffect(() => { onLoaded?.(loadedEntry); }, [onLoaded, loadedEntry]);

  // The same shortcut the propositions page opens its palette with — one implementation, so the
  // two cannot drift into meaning different things.
  useCommandKey(() => setPicking(true));

  return (
    <>
      <AppBar
        page={props.page}
        controls={
          <>
            {loaded && (
              <span className="rule-version">
                v{loaded.version}
                {loaded.isCodeDefault && <em> — code-defined default (builder starts fresh)</em>}
              </span>
            )}
            <Toolbar actions={[
              { id: 'open', label: 'Open', icon: IconOpen, onActivate: () => setPicking(true) },
              {
                id: 'save', label: 'Save', icon: IconSave, onActivate: () => void save(),
                unavailable: whyRuleSaveUnavailable({ loaded, saving }),
              },
              { id: 'json', label: 'JSON', icon: IconJson, onActivate: () => setDocumentOpen(true) },
            ]} />
          </>
        }
      >
        <span className="breadcrumb-sep">/</span>
        <span className="breadcrumb-item">Eligibility rules</span>
        <span className="breadcrumb-sep">/</span>
        <span className="breadcrumb-current">{loaded?.name ?? LOCAL_DRAFT.label}</span>
        <span className="model-pill" title="Model type the rule is validated and evaluated against">
          {MODEL_TYPE}
        </span>
      </AppBar>
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
      {picking && (
        <CommandPalette<RuleOption>
          label="Rules"
          placeholder="Filter rules"
          items={options}
          // Matched and rendered on the label rather than the id, because the local draft's id is
          // empty by design: an id-driven row would read as blank and be unsearchable.
          match={(option, needle) => option.label.toLowerCase().includes(needle.toLowerCase())}
          renderItem={(option) => <span className="palette-name">{option.label}</span>}
          onChoose={(option) => { void load(option.id || null); setPicking(false); }}
          onClose={() => setPicking(false)}
        />
      )}
      {documentOpen && <DocumentModal onClose={() => setDocumentOpen(false)} />}
    </>
  );
}
