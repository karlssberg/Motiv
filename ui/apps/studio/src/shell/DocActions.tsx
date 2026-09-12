import { useState } from 'react';
import { Toolbar } from './Toolbar.js';
import { SplitButton, type SplitVariant } from './SplitButton.js';
import { IconJson, IconSave } from './icons.js';

/** The two ways of saving, and the key the one in force is remembered under. */
const SAVE_VARIANTS: readonly SplitVariant[] = [
  { id: 'save', label: 'Save', description: 'Keep editing after saving.' },
  { id: 'save-close', label: 'Save & close', description: 'Save, then close this tab.' },
];
const SAVE_DEFAULT_KEY = 'motiv.studio.save-default';

/**
 * Which save the primary button runs. Per browser, not per session or tab: it is a way of working,
 * like a keyboard layout, and a person who wants "save and get out" wants it everywhere. Guarded,
 * because storage can be denied and the toolbar must still render without it.
 */
function readSaveDefault(): string {
  const fallback = SAVE_VARIANTS[0]!.id;
  try {
    const stored = window.localStorage.getItem(SAVE_DEFAULT_KEY);
    // Only an id a variant answers to: a corrupted key, or one written by a build with other
    // variants, would otherwise ride along in state naming nothing.
    return SAVE_VARIANTS.some((variant) => variant.id === stored) ? stored! : fallback;
  } catch {
    return fallback;
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
 * A document's own actions — JSON, and Save as a split button whose menu offers *Save* and
 * *Save & close* — drawn at the end of the editor pane's header, on the pane that holds the
 * document. One component for both kinds, so a rule and a proposition are viewed and saved the
 * same way. Open and Close are not here: opening is the tab strip's, and closing is the tab's ×.
 *
 * `onSave` resolves whether the save landed, and "& close" waits for that: closing over a conflict
 * or a rejection would hide the banner that reports it.
 */
export function DocActions(props: {
  /** Why Save cannot run right now, or `undefined` when it can. */
  saveUnavailable: string | undefined;
  /** Text appended to both save labels — the blast radius count on a proposition. */
  saveDetail?: string;
  onJson: () => void;
  onSave: () => Promise<boolean>;
  onClose: () => void;
}) {
  const [saveDefault, setSaveDefault] = useState(readSaveDefault);

  const runSave = (variant: SplitVariant): void => {
    setSaveDefault(variant.id);
    writeSaveDefault(variant.id);
    if (variant.id === 'save-close') void props.onSave().then((saved) => { if (saved) props.onClose(); });
    else void props.onSave();
  };

  const detail = props.saveDetail === undefined ? '' : ` ${props.saveDetail}`;
  const variants = SAVE_VARIANTS.map((variant) => ({ ...variant, label: `${variant.label}${detail}` }));

  return (
    <Toolbar actions={[{ id: 'json', label: 'JSON', icon: IconJson, onActivate: props.onJson }]}>
      <SplitButton
        id="save"
        icon={IconSave}
        variants={variants}
        defaultId={saveDefault}
        onChoose={runSave}
        unavailable={props.saveUnavailable}
      />
    </Toolbar>
  );
}
