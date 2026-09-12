/*
 * PROTOTYPE — throwaway. Do not ship.
 *
 * One palette for both kinds. With dynamic tabs there is no "which page am I on" to scope the
 * chooser by, so it lists rules and propositions together, each row saying which it is.
 */
import { useMemo, useSyncExternalStore } from 'react';
import { CommandPalette } from '../../shell/CommandPalette.js';
import { KIND_LABEL, KindGlyph } from './TabCard.js';
import { tabIdOf, type DocKind, type Workspace } from './workspace.js';

interface Option { id: string; kind: DocKind; name: string; version: number; open: boolean }

export function OpenPalette(props: { workspace: Workspace; onChoose: (kind: DocKind, name: string) => void; onClose: () => void }) {
  const state = useSyncExternalStore(props.workspace.subscribe, props.workspace.getState);
  const items = useMemo((): Option[] => [
    ...state.rules.map((rule) => ({ id: tabIdOf('rule', rule.name), kind: 'rule' as const, name: rule.name, version: rule.version, open: tabIdOf('rule', rule.name) in state.docs })),
    ...state.propositions.map((prop) => ({ id: tabIdOf('proposition', prop.name), kind: 'proposition' as const, name: prop.name, version: prop.version, open: tabIdOf('proposition', prop.name) in state.docs })),
  ], [state.rules, state.propositions, state.docs]);

  return (
    <CommandPalette<Option>
      label="Open"
      placeholder="Open a rule or proposition"
      items={items}
      match={(option, needle) => option.name.toLowerCase().includes(needle.toLowerCase()) || option.kind.startsWith(needle.toLowerCase())}
      renderItem={(option) => (
        <span className="palette-open-row">
          <KindGlyph kind={option.kind} />
          <span className="palette-name">{option.name}</span>
          <span className="palette-kind">{KIND_LABEL[option.kind]}</span>
          {option.open && <span className="origin-badge">open</span>}
        </span>
      )}
      onChoose={(option) => { props.onChoose(option.kind, option.name); props.onClose(); }}
      onClose={props.onClose}
    />
  );
}
