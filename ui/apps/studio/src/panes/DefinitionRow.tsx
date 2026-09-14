import { useId, useRef, useState } from 'react';
import {
  definitionBodyPath, isValidLocalName, localReferences,
  type Definition, type RuleDocument, type RuleEditorStore,
} from '@motiv-rules/core';
import { useRuleEditorStore } from '@motiv-rules/react';
import { LocalNameInput } from '../builder/LocalNameInput.js';
import { RuleDslStrip } from '../builder/RuleDslStrip.js';
import { RuleNodeEditor, useBuilderTree } from '../builder/RuleNodeEditor.js';
import { usePopoverCard } from '../builder/usePopoverCard.js';
import { IconMore } from '../shell/icons.js';
import { MODEL_TYPE } from '../App.js';

/** Repeats `inlineLocal` until no reference to `name` remains — see {@link DefinitionRow}'s menu. */
function inlineEverywhere(store: RuleEditorStore, name: string): void {
  // Re-read after every call rather than snapshotting the list once: the store's own `inlineLocal`
  // deletes the definition the moment its last reference is inlined, and — when a reference sits
  // inside *another* definition's body — that removal can make an earlier-computed path stale. A
  // fresh read each time means the loop only ever inlines a path that is still there, in whatever
  // order `localReferences` currently reports it (last one first). This is several undo steps, one
  // per reference — a single-commit variant belongs in the store, not here.
  for (;;) {
    const refs = localReferences(store.getState().document, name);
    const path = refs.at(-1);
    if (path === undefined) return;
    store.inlineLocal(path);
  }
}

/**
 * One row of the {@link DefinitionsPane}: a definition's name, how many places reference it, the
 * menu that renames its shape (inline or promote) or removes it — and below that, its body as the
 * same strip-and-tree the rule is, editable row for row, with its `whenTrue`/`whenFalse` in the
 * body root's detail panel where the rule keeps its own (#234). A definition is a rule that the
 * document names, so it looks and behaves like one.
 *
 * `id={definition-<name>}` and `tabIndex={-1}` are load-bearing, not decorative: the builder's
 * `Go to definition` button focuses this exact element by that id.
 */
export function DefinitionRow(props: {
  name: string;
  definition: Definition;
  document: RuleDocument;
  /** Every other definition's name — what this row's rename must not collide with. */
  taken: ReadonlySet<string>;
  open: boolean;
  setOpen: (open: boolean) => void;
  onPromote?: ((name: string) => void) | undefined;
}) {
  const { name, definition, document, taken, open, setOpen, onPromote } = props;
  const store = useRuleEditorStore();
  const { highlight, locals, catalog } = useBuilderTree();
  const bodyPath = definitionBodyPath(name);
  /** Names the strip's generated text, so the tree below can be described by it. */
  const expressionId = useId();

  // A draft the input edits freely; committed to the store only once, on blur, and only if it
  // actually names something usable. Held in a ref alongside the state because `LocalNameInput`
  // calls `onChange` then `onCommit` back to back on blur, and the state update from the first is
  // not yet visible to a closure captured by the second.
  const [draft, setDraft] = useState(name);
  const draftRef = useRef(name);

  const handleChange = (next: string): void => {
    draftRef.current = next;
    setDraft(next);
  };

  const handleCommit = (): void => {
    const value = draftRef.current;
    if (value !== name && isValidLocalName(value) && !taken.has(value)) {
      store.renameLocal(name, value);
    } else {
      // Not a usable name (invalid, taken, or unchanged): the field reverts rather than sticking
      // on text the document was never updated to match.
      draftRef.current = name;
      setDraft(name);
    }
  };

  const references = localReferences(document, name);
  const referenced = references.length > 0;

  const { trigger, card, style, close } = usePopoverCard(open, setOpen);

  const menuItems: Array<{ label: string; run: () => void; disabled?: boolean }> = [
    { label: 'Inline everywhere', run: () => inlineEverywhere(store, name) },
    ...(onPromote ? [{ label: 'Promote to catalog…', run: () => onPromote(name) }] : []),
    { label: 'Remove', run: () => store.removeDefinition(name), disabled: referenced },
  ];

  return (
    <div id={`definition-${name}`} tabIndex={-1} className="definition-row">
      <div className="definition-row-head">
        <LocalNameInput
          value={draft}
          onChange={handleChange}
          onCommit={handleCommit}
          ariaLabel={`name of definition ${name}`}
          taken={taken}
        />
        <span className="definition-count">
          {references.length} {references.length === 1 ? 'reference' : 'references'}
        </span>
        <button
          ref={trigger}
          type="button"
          className={open ? 'node-menu-trigger open' : 'node-menu-trigger'}
          aria-haspopup="menu"
          aria-expanded={open}
          aria-label={`actions for definition ${name}`}
          onClick={() => setOpen(!open)}
        >
          <IconMore size={14} />
        </button>
        {open && (
          <div ref={card} role="menu" className="node-menu" style={style} aria-label={`actions for definition ${name}`}>
            {menuItems.map((item) => (
              <button
                key={item.label}
                type="button"
                role="menuitem"
                className="node-menu-item"
                disabled={item.disabled}
                onClick={() => { item.run(); close(); }}
              >
                {item.label}
              </button>
            ))}
          </div>
        )}
      </div>
      <RuleDslStrip
        rule={definition.rule}
        rootPath={bodyPath}
        ariaLabel={`definition ${name} expression`}
        highlight={highlight}
        textId={expressionId}
        locals={locals}
        catalog={catalog}
      />
      {/* Described by its own strip, exactly as the rule's composition is by the rule's. */}
      <div role="group" aria-label={`definition ${name} composition`} aria-describedby={expressionId}>
        <RuleNodeEditor path={bodyPath} modelType={MODEL_TYPE} />
      </div>
    </div>
  );
}
