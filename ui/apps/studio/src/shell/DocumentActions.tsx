import { useState } from 'react';
import { Toolbar } from './Toolbar.js';
import { SplitButton, type SplitVariant } from './SplitButton.js';
import { DiscardDialog } from './DiscardDialog.js';
import { IconClose, IconJson, IconOpen, IconSave } from './icons.js';

/** The two ways of saving, and the key the one in force is remembered under. */
const SAVE_VARIANTS: readonly SplitVariant[] = [
  { id: 'save', label: 'Save', description: 'Keep editing after saving.' },
  { id: 'save-close', label: 'Save & close', description: 'Save, then return to the listing.' },
];
const SAVE_DEFAULT_KEY = 'motiv.studio.save-default';

/**
 * Which save the primary button runs. Per browser, not per session or tab: it is a way of working,
 * like a keyboard layout, and a person who wants "save and get out" wants it everywhere. Guarded,
 * because storage can be denied and the toolbar must still render without it.
 */
function readSaveDefault(): string {
  try {
    return window.localStorage.getItem(SAVE_DEFAULT_KEY) ?? SAVE_VARIANTS[0]!.id;
  } catch {
    return SAVE_VARIANTS[0]!.id;
  }
}

function writeSaveDefault(id: string): void {
  try {
    window.localStorage.setItem(SAVE_DEFAULT_KEY, id);
  } catch {
    // Not remembered; the choice still ran.
  }
}

/**
 * The toolbar both document pages share — Open, JSON, Close, and Save as a split button — and the
 * unsaved-changes question Close asks. One component rather than two inline action lists, so the
 * pages cannot drift: a rule and a proposition are opened, viewed, closed and saved the same way.
 *
 * `onSave` resolves whether the save landed, and "& close" waits for that: closing over a conflict
 * or a rejection would hide the banner that reports it.
 */
export function DocumentActions(props: {
  /** What the page edits — only the wording of the reasons changes with it. */
  kind: 'rule' | 'proposition';
  /** The open document's name, or `null` while nothing is open. */
  name: string | null;
  dirty: boolean;
  /** Why Save cannot run right now, or `undefined` when it can. */
  saveUnavailable: string | undefined;
  /** Text appended to both save labels — the blast radius count on the propositions page. */
  saveDetail?: string;
  onOpen: () => void;
  onJson: () => void;
  onSave: () => Promise<boolean>;
  onClose: () => void;
}) {
  const [asking, setAsking] = useState(false);
  const [saveDefault, setSaveDefault] = useState(readSaveDefault);
  const nothingOpen = props.name === null ? `Nothing open — choose a ${props.kind} first.` : undefined;

  const saveAndClose = async (): Promise<void> => {
    if (await props.onSave()) props.onClose();
  };

  const runSave = (variant: SplitVariant): void => {
    setSaveDefault(variant.id);
    writeSaveDefault(variant.id);
    if (variant.id === 'save-close') void saveAndClose();
    else void props.onSave();
  };

  const detail = props.saveDetail === undefined ? '' : ` ${props.saveDetail}`;
  const variants = SAVE_VARIANTS.map((variant) => ({ ...variant, label: `${variant.label}${detail}` }));

  return (
    <>
      <Toolbar actions={[
        { id: 'open', label: 'Open', icon: IconOpen, onActivate: props.onOpen },
        { id: 'json', label: 'JSON', icon: IconJson, onActivate: props.onJson, unavailable: nothingOpen },
        {
          id: 'close', label: 'Close', icon: IconClose, unavailable: nothingOpen,
          onActivate: () => { if (props.dirty) setAsking(true); else props.onClose(); },
        },
      ]}>
        <SplitButton
          id="save"
          icon={IconSave}
          variants={variants}
          defaultId={saveDefault}
          onChoose={runSave}
          unavailable={props.saveUnavailable}
        />
      </Toolbar>
      {asking && props.name !== null && (
        <DiscardDialog
          name={props.name}
          onKeep={() => setAsking(false)}
          onSaveAndClose={() => { setAsking(false); void saveAndClose(); }}
          onDiscard={() => { setAsking(false); props.onClose(); }}
        />
      )}
    </>
  );
}
