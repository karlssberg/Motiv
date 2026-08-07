import { useEffect, useMemo, useState } from 'react';
import type { RuleListEntry, RulesApiClient } from '@motiv-rules/core';
import { useRuleEditor, useRuleEditorStore } from '@motiv-rules/react';
import { MODEL_TYPE } from '../App.js';
import type { Page } from '../routing/useHashRoute.js';
import { AppBar } from './AppBar.js';
import { DocumentModal } from './DocumentModal.js';
import { CommandPalette } from '../shell/CommandPalette.js';
import { Toolbar } from '../shell/Toolbar.js';
import { useCommandKey } from '../shell/useCommandKey.js';
import { IconJson, IconOpen, IconSave } from '../shell/icons.js';

/**
 * One row of the rule palette: the name `load` fetches by, and the text the row reads as. They
 * are the same string for every server rule, and deliberately not for the local draft.
 */
interface RuleOption {
  id: string;
  label: string;
}

/**
 * The palette's first row: nothing loaded from the server, so nothing to save back to it. The
 * empty id is what `load` reads as "unload" — the document in the editor is left alone, since
 * it is the draft either way — and the label stands in for the name it has not got.
 */
const LOCAL_DRAFT: RuleOption = { id: '', label: 'local draft' };

/** The picked rule's server identity: what Save must send back to avoid clobbering. */
interface LoadedRule {
  name: string;
  version: number;
  isCodeDefault: boolean;
}

/** Why Save cannot run, or `undefined` when it can. */
function whyNotSave(loaded: LoadedRule | null, saving: boolean): string | undefined {
  if (loaded === null) return 'Nothing loaded yet.';
  if (saving) return 'Saving…';
  return undefined;
}

/**
 * Seam: dynamic replacement. Picks a live server rule, loads its document into the shared
 * editor store, and saves it back with the loaded version — a stale version surfaces as a
 * conflict banner (open two tabs to watch the race protection work). Reports the picked
 * rule's catalog entry via onLoaded so the shell can adapt (e.g. async validation).
 */
export function RuleHeader(props: {
  client: RulesApiClient;
  onLoaded?: (entry: RuleListEntry | null) => void;
  page: Page;
  onNavigate: (page: Page) => void;
}) {
  const store = useRuleEditorStore();
  const state = useRuleEditor(store);
  const [rules, setRules] = useState<RuleListEntry[]>([]);
  const [loaded, setLoaded] = useState<LoadedRule | null>(null);
  const [conflict, setConflict] = useState<number | null>(null);
  const [saving, setSaving] = useState(false);
  const [picking, setPicking] = useState(false);
  const [documentOpen, setDocumentOpen] = useState(false);
  // Memoised because the palette filters against `items` by identity: a fresh array on every
  // render would re-run the match over the whole listing on every keystroke.
  const options = useMemo(
    (): RuleOption[] => [LOCAL_DRAFT, ...rules.map((rule) => ({ id: rule.name, label: rule.name }))],
    [rules],
  );

  useEffect(() => {
    let cancelled = false;
    void props.client.listRules().then((entries) => {
      if (!cancelled) setRules(entries);
    });
    return () => {
      cancelled = true;
    };
  }, [props.client]);

  // The same shortcut the propositions page opens its palette with — one implementation, so the
  // two cannot drift into meaning different things.
  useCommandKey(() => setPicking(true));

  const load = async (name: string): Promise<void> => {
    if (!name) {
      setLoaded(null);
      setConflict(null);
      props.onLoaded?.(null);
      return;
    }
    const response = await props.client.getRule(name);
    setConflict(null);
    setLoaded({ name, version: response.version, isCodeDefault: response.document === null });
    props.onLoaded?.(rules.find((rule) => rule.name === name) ?? null);
    if (response.document) store.loadDocument(response.document);
  };

  const save = async (): Promise<void> => {
    if (!loaded) return;
    setSaving(true);
    try {
      const result = await props.client.putRule(loaded.name, state.document, loaded.version);
      if (result.outcome === 'updated') {
        setConflict(null);
        setLoaded({ ...loaded, version: result.version, isCodeDefault: false });
      } else if (result.outcome === 'conflict') {
        setConflict(result.currentVersion);
      } else {
        // Flavour-specific rejections (e.g. PolicyRequired) are invisible to live
        // validation — surface them in the shared error list.
        store.setErrors(result.errors);
      }
    } finally {
      setSaving(false);
    }
  };

  return (
    <>
      <AppBar
        page={props.page}
        onNavigate={props.onNavigate}
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
                unavailable: whyNotSave(loaded, saving),
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
      {conflict !== null && loaded && (
        <div role="alert" className="conflict-banner">
          Someone else saved version {conflict} of “{loaded.name}”.
          <button type="button" className="btn" onClick={() => void load(loaded.name)}>
            Reload latest
          </button>
        </div>
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
          onChoose={(option) => { void load(option.id); setPicking(false); }}
          onClose={() => setPicking(false)}
        />
      )}
      {documentOpen && <DocumentModal onClose={() => setDocumentOpen(false)} />}
    </>
  );
}
