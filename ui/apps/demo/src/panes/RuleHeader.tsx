import { useEffect, useState } from 'react';
import type { RuleListEntry, RulesApiClient } from '@motiv/rules-core';
import { useRuleEditor, useRuleEditorStore } from '@motiv/rules-react';
import { ListboxPicker } from '../builder/ListboxPicker.js';
import { MODEL_TYPE } from '../App.js';
import type { Page } from '../routing/useHashRoute.js';
import { AppBar } from './AppBar.js';

/**
 * The picker's first entry: nothing loaded from the server, so nothing to save back to it. The
 * empty name is what `load` reads as "unload" — the document in the editor is left alone, since
 * it is the draft either way.
 */
const LOCAL_DRAFT = { value: '', label: 'local draft' };

/** The picked rule's server identity: what Save must send back to avoid clobbering. */
interface LoadedRule {
  name: string;
  version: number;
  isCodeDefault: boolean;
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
  const options = [LOCAL_DRAFT, ...rules.map((rule) => ({ value: rule.name, label: rule.name }))];

  useEffect(() => {
    let cancelled = false;
    void props.client.listRules().then((entries) => {
      if (!cancelled) setRules(entries);
    });
    return () => {
      cancelled = true;
    };
  }, [props.client]);

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
            <button type="button" className="btn" disabled={!loaded || saving} onClick={() => void save()}>
              Save
            </button>
          </>
        }
      >
        <span className="breadcrumb-sep">/</span>
        <span className="breadcrumb-item">Eligibility rules</span>
        <span className="breadcrumb-sep">/</span>
        {/* The trail's leaf is the rule picker: the crumb already names the rule in force, so a
            separate control alongside it would be the same fact stated twice. */}
        <ListboxPicker
          options={options}
          value={loaded?.name ?? LOCAL_DRAFT.value}
          onChoose={(name) => void load(name)}
          open={picking}
          setOpen={setPicking}
          triggerName="rule"
          listLabel="rules"
          triggerClassName="breadcrumb-current"
          listClassName="breadcrumb-menu"
        />
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
    </>
  );
}
