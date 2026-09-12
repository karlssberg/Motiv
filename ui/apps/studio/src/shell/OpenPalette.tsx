import { useMemo } from 'react';
import type { PropositionListEntry, RuleListEntry } from '@motiv-rules/core';
import { CommandPalette } from './CommandPalette.js';
import { Toolbar } from './Toolbar.js';
import { IconPropositions } from './icons.js';
import { KIND_LABEL, KindGlyph } from './TabCard.js';
import { tabIdOf, type DocKind, type TabId } from './workspace.js';

interface Option { id: TabId; kind: DocKind; name: string; open: boolean }

/**
 * One palette for both kinds. With tabs there is no "which page am I on" to scope the chooser by,
 * so rules and propositions are listed together, each row saying which it is and whether it is
 * already open. Authoring — New, Derive, Override, Delete — stays with the propositions
 * explorer, one step further in from the footer: it acts on the set, and browsing a namespace
 * tree is worth its own surface.
 */
export function OpenPalette(props: {
  rules: readonly RuleListEntry[];
  propositions: readonly PropositionListEntry[];
  /** The ids of the tabs already open. */
  open: ReadonlySet<TabId>;
  onChoose: (kind: DocKind, name: string) => void;
  /** Opens the propositions explorer in this palette's place. */
  onManage: () => void;
  onClose: () => void;
}) {
  const { rules, propositions, open } = props;
  const items = useMemo((): Option[] => [
    ...rules.map((rule) => {
      const id = tabIdOf('rule', rule.name);
      return { id, kind: 'rule' as const, name: rule.name, open: open.has(id) };
    }),
    ...propositions.map((prop) => {
      const id = tabIdOf('proposition', prop.name);
      return { id, kind: 'proposition' as const, name: prop.name, open: open.has(id) };
    }),
  ], [rules, propositions, open]);

  return (
    <CommandPalette<Option>
      label="Open"
      placeholder="Open a rule or proposition"
      items={items}
      match={(option, needle) => {
        const query = needle.toLowerCase();
        return option.name.toLowerCase().includes(query) || KIND_LABEL[option.kind].toLowerCase().startsWith(query);
      }}
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
      footer={() => (
        <Toolbar
          labelled
          actions={[{ id: 'manage', label: 'Manage propositions', icon: IconPropositions, onActivate: props.onManage }]}
        />
      )}
    />
  );
}
