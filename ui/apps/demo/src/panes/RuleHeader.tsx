import { useEffect, useState } from 'react';
import type { RuleListEntry, RulesApiClient } from '@motiv/rules-core';
import { useRuleEditor, useRuleEditorStore } from '@motiv/rules-react';

/** The picked rule's server identity: what Save must send back to avoid clobbering. */
interface LoadedRule {
  name: string;
  version: number;
  isCodeDefault: boolean;
}

/**
 * Seam: dynamic replacement. Picks a live server rule, loads its document into the shared
 * editor store, and saves it back with the loaded version — a stale version surfaces as a
 * conflict banner (open two tabs to watch the race protection work).
 */
export function RuleHeader(props: { client: RulesApiClient }) {
  const store = useRuleEditorStore();
  const state = useRuleEditor(store);
  const [rules, setRules] = useState<RuleListEntry[]>([]);
  const [loaded, setLoaded] = useState<LoadedRule | null>(null);
  const [conflict, setConflict] = useState<number | null>(null);
  const [saving, setSaving] = useState(false);

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
      return;
    }
    const response = await props.client.getRule(name);
    setConflict(null);
    setLoaded({ name, version: response.version, isCodeDefault: response.document === null });
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
      }
      // 'invalid' outcomes surface through the store's live validation errors already.
    } finally {
      setSaving(false);
    }
  };

  return (
    <header className="pane rule-header">
      <label className="field">
        <span>Rule</span>
        <select
          className="control"
          value={loaded?.name ?? ''}
          onChange={(e) => void load(e.target.value)}
        >
          <option value="">— local draft —</option>
          {rules.map((rule) => (
            <option key={rule.name} value={rule.name}>{rule.name}</option>
          ))}
        </select>
      </label>
      {loaded && (
        <span className="rule-version">
          v{loaded.version}
          {loaded.isCodeDefault && <em> — code-defined default (builder starts fresh)</em>}
        </span>
      )}
      <button type="button" className="btn" disabled={!loaded || saving} onClick={() => void save()}>
        Save
      </button>
      {conflict !== null && loaded && (
        <div role="alert" className="conflict-banner">
          Someone else saved version {conflict} of “{loaded.name}”.
          <button type="button" className="btn" onClick={() => void load(loaded.name)}>
            Reload latest
          </button>
        </div>
      )}
    </header>
  );
}
