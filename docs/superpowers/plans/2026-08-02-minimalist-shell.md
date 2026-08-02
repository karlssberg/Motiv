# Minimalist Shell Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the demo's worded chrome and four-column layout with a three-icon ghost toolbar, a command palette for choosing documents, and a modal JSON viewer — on both pages.

**Architecture:** A new `ui/apps/demo/src/shell/` directory holds four presentational components — `icons`, `Modal`, `Toolbar`, `CommandPalette` — none of which know anything about propositions or rules. Both pages compose them: `PropositionsPage` supplies proposition items and a four-action footer, `RuleHeader` supplies rule items and no footer. Modals are native `<dialog>` elements, which give focus trapping, Escape and backdrop inertness with no library.

**Tech Stack:** React 18, TypeScript (strict), Vitest + Testing Library + jsdom 25, Playwright, plain CSS with custom properties.

## Global Constraints

- **Zero new runtime dependencies in `ui/`.** `git diff --stat -- '*package.json' '*pnpm-lock.yaml'` must be empty at the end of every task. No icon package, no focus-trap library, no command-palette library.
- **TDD is mandatory** (see `CLAUDE.md`): failing test first, run it, confirm it fails *for the right reason*, then implement.
- **Every new guard must be mutation-verified.** Apply the mutation, confirm the new test fails, revert, confirm it passes. Report which mutations you proved bite. Nine tests in this codebase have been found incapable of failing; six were caught by mutation rather than by review. A test that has not been mutation-checked is not evidence.
- **All colours come from custom properties** in `ui/apps/demo/src/styles/tokens.css`. No hex literals in new CSS. `--radius` is `6px`; use it rather than a literal.
- **Both light and dark must work.** `tokens.css` redefines every token under `@media (prefers-color-scheme: dark)`.
- **The responsive breakpoint is 900px**, already used by `.shell-body`. Reuse it; do not introduce another.
- Run tests with `cd ui && pnpm -r test` and types with `pnpm -r typecheck`.
- Do **not** run the Playwright suite until Task 9. It needs a probed-free port and takes minutes.

## Design decisions carried from the spec

Read `docs/superpowers/specs/2026-08-02-minimalist-shell-design.md` before starting. Two points that are easy to get wrong:

1. **The palette shows a tree when the query is empty and a flat list when it is not.** Browsing a namespace and searching it are different tasks. With no query the existing `TreeNode` renders, so namespaces stay browsable; once the user types, results flatten to one row per match with the namespace as a dimmed prefix, because hierarchy is noise in a result list. This preserves `buildNamespaceTree` / `filterTree` / `countLeaves` in `@motiv/rules-core` and the `TreeNode` component, all of which are already tested.
2. **`aria-disabled`, never `disabled`.** A `disabled` button leaves the tab order, so a Tab-navigating screen-reader user never reaches it and never hears why it is unavailable. Every unavailable action in this plan uses `aria-disabled="true"` plus a handler that returns early.

## File Structure

| File | Responsibility |
|---|---|
| `src/shell/icons.tsx` | *Create.* One exported function per glyph. No behaviour. |
| `src/shell/Modal.tsx` | *Create.* Native `<dialog>` lifecycle and dismissal. Knows nothing of its contents. |
| `src/shell/Toolbar.tsx` | *Create.* Renders icon buttons from a declarative action list. |
| `src/shell/CommandPalette.tsx` | *Create.* Search input, highlight, keyboard navigation. Generic over item type. |
| `src/panes/DocumentModal.tsx` | *Create.* Wraps the existing `JsonPane` body in a `Modal`. |
| `test/setup.ts` | *Modify.* Add the `showModal` / `close` shim. |
| `src/panes/AppBar.tsx` | *Modify.* Glyph prefixes on nav; toolbar in the controls slot. |
| `src/explorer/PropositionExplorer.tsx` | *Modify.* Loses `<aside>` chrome; becomes palette contents. |
| `src/panes/PropositionsPage.tsx` | *Modify.* Owns palette and modal open state; drops rail and `JsonPane`. |
| `src/panes/RuleHeader.tsx` | *Modify.* `ListboxPicker` → palette. |
| `src/panes/RulesPage.tsx` | *Modify.* Drops `JsonPane`. |
| `src/explorer/PropositionDialog.tsx` | *Modify.* Renders through `Modal`. |
| `src/styles/app.css` | *Modify.* Ghost buttons, palette, mobile fullscreen. |
| `e2e/*.spec.ts` | *Modify.* Rewritten against the new shell in Task 9. |

`ListboxPicker` stays — `OperatorPicker` still uses it. Only `RuleHeader`'s use goes.

---

### Task 1: Icons and the `<dialog>` shim

The foundation both later components need. No domain knowledge, no state.

**Files:**
- Create: `ui/apps/demo/src/shell/icons.tsx`
- Modify: `ui/apps/demo/test/setup.ts`
- Test: `ui/apps/demo/test/shell/icons.test.tsx`

**Interfaces:**
- Consumes: nothing.
- Produces: `IconProps`, and the components `IconOpen`, `IconSave`, `IconJson`, `IconRules`, `IconPropositions`, `IconSearch`, `IconNew`, `IconDerive`, `IconOverride`, `IconDelete`, `IconClose` — each `(props: IconProps) => JSX.Element`.

- [ ] **Step 1: Write the failing test**

Create `ui/apps/demo/test/shell/icons.test.tsx`:

```tsx
import { describe, expect, it } from 'vitest';
import { render } from '@testing-library/react';
import { IconOpen, IconSave } from '../../src/shell/icons.js';

describe('icons', () => {
  it('renders at the requested size', () => {
    const { container } = render(<IconOpen size={20} />);
    const svg = container.querySelector('svg');
    expect(svg?.getAttribute('width')).toBe('20');
    expect(svg?.getAttribute('height')).toBe('20');
  });

  it('inherits colour from its button rather than hard-coding one', () => {
    // The whole ghost treatment is a colour change on hover, which only works if the glyph
    // takes its colour from the element around it.
    const { container } = render(<IconSave />);
    expect(container.querySelector('svg')?.getAttribute('stroke')).toBe('currentColor');
  });

  it('is hidden from assistive technology, because the button carries the name', () => {
    // Without this the button announces its aria-label and then the glyph again.
    const { container } = render(<IconSave />);
    expect(container.querySelector('svg')?.getAttribute('aria-hidden')).toBe('true');
  });
});
```

- [ ] **Step 2: Run it to make sure it fails**

```bash
cd ui && pnpm --filter @motiv/rules-demo test icons
```
Expected: FAIL — cannot resolve `../../src/shell/icons.js`.

- [ ] **Step 3: Write the icons**

Create `ui/apps/demo/src/shell/icons.tsx`:

```tsx
/**
 * The shell's glyph set, hand-drawn as inline SVG.
 *
 * `ui/` takes no new runtime dependencies, so an icon package is out. Unicode glyphs were the
 * other candidate and were rejected: they render inconsistently enough across platforms that a
 * toolbar built from them looks broken on someone else's machine.
 *
 * Every glyph strokes in `currentColor` and is `aria-hidden`. The colour makes the ghost hover
 * treatment possible; the hiding keeps the button's `aria-label` the single accessible name.
 */

export interface IconProps {
  /** Edge length in pixels. Defaults to 17, the size the toolbar uses. */
  size?: number;
}

function Glyph(props: IconProps & { children: React.ReactNode }) {
  const size = props.size ?? 17;
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.6"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
    >
      {props.children}
    </svg>
  );
}

export const IconOpen = (props: IconProps) => (
  <Glyph {...props}><path d="M4 5h5l1.5 2H20v11a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1z" /></Glyph>
);

export const IconSave = (props: IconProps) => (
  <Glyph {...props}>
    <path d="M12 4v10m0 0l-3.5-3.5M12 14l3.5-3.5" />
    <path d="M5 17v2a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1v-2" />
  </Glyph>
);

export const IconJson = (props: IconProps) => (
  <Glyph {...props}>
    <path d="M9 4c-2 0-2.5 1-2.5 3S6 10 4.5 12c1.5 2 2 2.5 2 5s.5 3 2.5 3" />
    <path d="M15 4c2 0 2.5 1 2.5 3s.5 3 2 5c-1.5 2-2 2.5-2 5s-.5 3-2.5 3" />
  </Glyph>
);

export const IconRules = (props: IconProps) => (
  <Glyph {...props}>
    <path d="M4 6h10M4 12h10M4 18h10" />
    <path d="M18 5.5l1.6 1.6L22 4.7" />
  </Glyph>
);

export const IconPropositions = (props: IconProps) => (
  <Glyph {...props}>
    <circle cx="6" cy="12" r="2.2" />
    <circle cx="18" cy="6" r="2.2" />
    <circle cx="18" cy="18" r="2.2" />
    <path d="M8.2 11L15.8 7M8.2 13l7.6 4" />
  </Glyph>
);

export const IconSearch = (props: IconProps) => (
  <Glyph {...props}><circle cx="11" cy="11" r="6" /><path d="M15.5 15.5L20 20" /></Glyph>
);

export const IconNew = (props: IconProps) => (
  <Glyph {...props}><path d="M12 5v14M5 12h14" /></Glyph>
);

export const IconDerive = (props: IconProps) => (
  <Glyph {...props}><path d="M6 3v12a3 3 0 0 0 3 3h9" /><path d="M15 15l3 3-3 3" /></Glyph>
);

export const IconOverride = (props: IconProps) => (
  <Glyph {...props}><path d="M4 8h11a4 4 0 0 1 0 8H8" /><path d="M11 13l-3 3 3 3" /></Glyph>
);

export const IconDelete = (props: IconProps) => (
  <Glyph {...props}><path d="M5 7h14M10 7V5h4v2M7 7l1 13h8l1-13" /></Glyph>
);

export const IconClose = (props: IconProps) => (
  <Glyph {...props}><path d="M6 6l12 12M18 6L6 18" /></Glyph>
);
```

- [ ] **Step 4: Run it to make sure it passes**

```bash
cd ui && pnpm --filter @motiv/rules-demo test icons
```
Expected: PASS, 3 tests.

- [ ] **Step 5: Add the jsdom `<dialog>` shim**

Append to `ui/apps/demo/test/setup.ts`:

```ts
// jsdom 25 defines HTMLDialogElement but implements neither showModal() nor close(), so a
// component that calls them throws on render. These stubs do the one thing jsdom can honestly
// model — the `open` attribute — and nothing else.
//
// THE LIMITS MATTER. jsdom has no top layer, so there is no focus trap, no inertness, and no
// Escape handling here. A unit test asserting any of those would pass or fail for reasons
// unrelated to what ships. Those three behaviours are proven in Playwright (Task 9) and MUST NOT
// be asserted in a jsdom test.
if (typeof HTMLDialogElement !== 'undefined' && typeof HTMLDialogElement.prototype.showModal !== 'function') {
  HTMLDialogElement.prototype.showModal = function showModal(): void { this.open = true; };
  HTMLDialogElement.prototype.close = function close(): void {
    this.open = false;
    this.dispatchEvent(new Event('close'));
  };
}
```

- [ ] **Step 6: Verify the shim works**

```bash
cd ui && pnpm --filter @motiv/rules-demo test
```
Expected: PASS, 341 pre-existing + 3 new = 344.

- [ ] **Step 7: Commit**

```bash
git add ui/apps/demo/src/shell/icons.tsx ui/apps/demo/test/shell/icons.test.tsx ui/apps/demo/test/setup.ts
git commit -m "feat(demo): inline glyph set and a jsdom dialog shim"
```

---

### Task 2: `Modal`

The native `<dialog>` wrapper. Every modal in the app ends up here.

**Files:**
- Create: `ui/apps/demo/src/shell/Modal.tsx`
- Test: `ui/apps/demo/test/shell/Modal.test.tsx`

**Interfaces:**
- Consumes: `IconClose` from Task 1.
- Produces: `Modal`, taking `{ label: string; onClose: () => void; className?: string; fullscreenOnMobile?: boolean; children: ReactNode }`.

- [ ] **Step 1: Write the failing test**

Create `ui/apps/demo/test/shell/Modal.test.tsx`:

```tsx
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Modal } from '../../src/shell/Modal.js';

describe('Modal', () => {
  it('opens itself modally on mount', () => {
    render(<Modal label="Propositions" onClose={() => {}}>body</Modal>);
    expect(screen.getByRole('dialog')).toHaveProperty('open', true);
  });

  it('names itself for assistive technology', () => {
    render(<Modal label="Propositions" onClose={() => {}}>body</Modal>);
    expect(screen.getByRole('dialog', { name: 'Propositions' })).toBeTruthy();
  });

  it('reports the close control', async () => {
    const onClose = vi.fn();
    render(<Modal label="Propositions" onClose={onClose}>body</Modal>);
    await userEvent.click(screen.getByRole('button', { name: /close/i }));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('reports a native cancel, which is what Escape raises', () => {
    // Escape on a <dialog> fires `cancel`, not a keydown the component sees. Dispatching the
    // event directly is the only honest way to reach that path under jsdom, which has no
    // top layer and so never raises it on its own.
    const onClose = vi.fn();
    render(<Modal label="Propositions" onClose={onClose}>body</Modal>);
    screen.getByRole('dialog').dispatchEvent(new Event('cancel', { cancelable: true }));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('reports a click on the backdrop but not one inside the content', async () => {
    // A <dialog>'s backdrop is part of the dialog element, so a backdrop click targets the
    // dialog itself. A click on anything inside must not close it.
    const onClose = vi.fn();
    render(<Modal label="Propositions" onClose={onClose}><button>inside</button></Modal>);

    await userEvent.click(screen.getByRole('button', { name: 'inside' }));
    expect(onClose).not.toHaveBeenCalled();

    await userEvent.click(screen.getByRole('dialog'));
    expect(onClose).toHaveBeenCalledTimes(1);
  });
});
```

- [ ] **Step 2: Run it to make sure it fails**

```bash
cd ui && pnpm --filter @motiv/rules-demo test Modal
```
Expected: FAIL — cannot resolve `../../src/shell/Modal.js`.

- [ ] **Step 3: Write the component**

Create `ui/apps/demo/src/shell/Modal.tsx`:

```tsx
import { useEffect, useRef, type MouseEvent, type ReactNode } from 'react';
import { IconClose } from './icons.js';

/**
 * A modal built on the native `<dialog>` element.
 *
 * `showModal()` gives focus trapping, Escape handling, backdrop inertness and correct assistive-
 * technology semantics with no library and no hand-rolled focus management — which is why this
 * exists rather than another `aria-modal` div. The app previously had one of those, and it had
 * none of those behaviours.
 *
 * Dismissal arrives three ways — the close control, Escape (as a native `cancel` event), and a
 * backdrop click — and all three are reported through the single `onClose`.
 */
export function Modal(props: {
  /** The dialog's accessible name. */
  label: string;
  onClose: () => void;
  className?: string;
  /** When set, the dialog fills the viewport below 900px instead of floating. */
  fullscreenOnMobile?: boolean;
  children: ReactNode;
}) {
  const ref = useRef<HTMLDialogElement>(null);
  const { onClose } = props;

  useEffect(() => {
    const dialog = ref.current;
    if (dialog === null) return;
    // Guarded because React 18 StrictMode runs effects twice, and showModal() on an already-open
    // dialog throws InvalidStateError.
    if (!dialog.open) dialog.showModal();
    return () => { if (dialog.open) dialog.close(); };
  }, []);

  useEffect(() => {
    const dialog = ref.current;
    if (dialog === null) return;
    // Escape reaches a <dialog> as `cancel`, never as a keydown on our own tree, so this is the
    // only place the key can be observed. Prevented so the browser does not also close the
    // dialog behind React's back, leaving the caller's state saying it is still open.
    const cancel = (event: Event): void => { event.preventDefault(); onClose(); };
    dialog.addEventListener('cancel', cancel);
    return () => dialog.removeEventListener('cancel', cancel);
  }, [onClose]);

  // The backdrop belongs to the dialog element itself, so a click on it targets the dialog while
  // a click on any content targets a descendant. Comparing target to currentTarget is what tells
  // them apart — there is no separate backdrop node to listen on.
  const onClick = (event: MouseEvent<HTMLDialogElement>): void => {
    if (event.target === event.currentTarget) onClose();
  };

  const classes = ['modal', props.fullscreenOnMobile === true ? 'modal-mobile-full' : null, props.className]
    .filter((name) => name !== null && name !== undefined)
    .join(' ');

  return (
    <dialog ref={ref} className={classes} aria-label={props.label} onClick={onClick}>
      <button type="button" className="ghost modal-close" aria-label="Close" onClick={onClose}>
        <IconClose size={15} />
      </button>
      {props.children}
    </dialog>
  );
}
```

- [ ] **Step 4: Run it to make sure it passes**

```bash
cd ui && pnpm --filter @motiv/rules-demo test Modal
```
Expected: PASS, 5 tests.

- [ ] **Step 5: Prove the guards bite**

Run each mutation, confirm the named test fails, revert, confirm it passes again.

| Mutation | Test that must fail |
|---|---|
| Delete `event.preventDefault()` from the `cancel` handler | none — **expected**; note it and move on, this is browser-only behaviour the shim cannot model |
| Change `event.target === event.currentTarget` to `true` | reports a click on the backdrop but not one inside |
| Delete the `cancel` listener registration | reports a native cancel |
| Delete `aria-label={props.label}` | names itself for assistive technology |

- [ ] **Step 6: Add the styles**

Append to `ui/apps/demo/src/styles/app.css`:

```css
/* Native <dialog>. The element is display:none until opened, so `[open]` carries the layout. */
.modal {
  border: 1px solid var(--border);
  border-radius: 10px;
  background: var(--sh-panel);
  color: var(--text);
  padding: 0;
  box-shadow: 0 12px 40px rgb(0 0 0 / 22%);
  max-width: min(680px, 92vw);
  width: 100%;
}

.modal::backdrop {
  background: rgb(15 18 22 / 42%);
}

.modal-close {
  position: absolute;
  top: 8px;
  right: 8px;
}

@media (max-width: 900px) {
  .modal-mobile-full {
    max-width: none;
    width: 100vw;
    height: 100dvh;
    max-height: none;
    border: none;
    border-radius: 0;
  }
}
```

- [ ] **Step 7: Run the whole suite and typecheck**

```bash
cd ui && pnpm -r test && pnpm -r typecheck
```
Expected: PASS, 349 demo tests.

- [ ] **Step 8: Commit**

```bash
git add ui/apps/demo/src/shell/Modal.tsx ui/apps/demo/test/shell/Modal.test.tsx ui/apps/demo/src/styles/app.css
git commit -m "feat(demo): a native dialog modal with one dismissal path"
```

---

### Task 3: `Toolbar`

**Files:**
- Create: `ui/apps/demo/src/shell/Toolbar.tsx`
- Test: `ui/apps/demo/test/shell/Toolbar.test.tsx`

**Interfaces:**
- Consumes: `IconProps` from Task 1.
- Produces: `ToolbarAction` = `{ id: string; label: string; icon: (props: IconProps) => JSX.Element; onActivate: () => void; unavailable?: string }`, and `Toolbar` taking `{ actions: ToolbarAction[] }`. `unavailable` is the *reason* — its presence is what makes the action unavailable, so a reason can never be forgotten.

- [ ] **Step 1: Write the failing test**

Create `ui/apps/demo/test/shell/Toolbar.test.tsx`:

```tsx
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Toolbar } from '../../src/shell/Toolbar.js';
import { IconSave } from '../../src/shell/icons.js';

describe('Toolbar', () => {
  it('names each icon button, since a glyph alone announces nothing', () => {
    render(<Toolbar actions={[{ id: 'save', label: 'Save', icon: IconSave, onActivate: () => {} }]} />);
    expect(screen.getByRole('button', { name: 'Save' })).toBeTruthy();
  });

  it('activates on click', async () => {
    const onActivate = vi.fn();
    render(<Toolbar actions={[{ id: 'save', label: 'Save', icon: IconSave, onActivate }]} />);
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));
    expect(onActivate).toHaveBeenCalledTimes(1);
  });

  it('keeps an unavailable action reachable and does not activate it', async () => {
    // Deliberately NOT the `disabled` attribute: a disabled button leaves the tab order, so a
    // keyboard screen-reader user never reaches it and never hears the reason.
    const onActivate = vi.fn();
    render(<Toolbar actions={[{
      id: 'save', label: 'Save', icon: IconSave, onActivate,
      unavailable: 'Nothing to save: this name is served by a compiled spec.',
    }]} />);

    const button = screen.getByRole('button', { name: 'Save' });
    expect(button.getAttribute('aria-disabled')).toBe('true');
    expect(button.hasAttribute('disabled')).toBe(false);

    await userEvent.click(button);
    expect(onActivate).not.toHaveBeenCalled();
  });

  it('ties the reason to the button so it is announced on arrival', () => {
    render(<Toolbar actions={[{
      id: 'save', label: 'Save', icon: IconSave, onActivate: () => {},
      unavailable: 'Nothing to save.',
    }]} />);

    const button = screen.getByRole('button', { name: 'Save' });
    const describedBy = button.getAttribute('aria-describedby');
    expect(describedBy).not.toBeNull();
    expect(document.getElementById(describedBy!)?.textContent).toBe('Nothing to save.');
  });

  it('does not describe an available action', () => {
    render(<Toolbar actions={[{ id: 'save', label: 'Save', icon: IconSave, onActivate: () => {} }]} />);
    expect(screen.getByRole('button', { name: 'Save' }).getAttribute('aria-describedby')).toBeNull();
  });
});
```

- [ ] **Step 2: Run it to make sure it fails**

```bash
cd ui && pnpm --filter @motiv/rules-demo test Toolbar
```
Expected: FAIL — cannot resolve `../../src/shell/Toolbar.js`.

- [ ] **Step 3: Write the component**

Create `ui/apps/demo/src/shell/Toolbar.tsx`:

```tsx
import type { IconProps } from './icons.js';

/**
 * One toolbar action.
 *
 * `unavailable` carries the *reason* rather than a boolean, so the reason cannot be omitted:
 * there is no way to make an action unavailable without saying why.
 */
export interface ToolbarAction {
  id: string;
  /** The button's accessible name and its tooltip. A bare glyph teaches nothing on first sight. */
  label: string;
  icon: (props: IconProps) => JSX.Element;
  onActivate: () => void;
  /** Why this action cannot be used right now. Absent means it can. */
  unavailable?: string;
}

/**
 * The shell's operations, as icons.
 *
 * Unavailable actions use `aria-disabled` and a handler that returns early, never the `disabled`
 * attribute — `disabled` removes a button from the tab order in every major browser, so a
 * keyboard screen-reader user cannot reach it and never hears the `aria-describedby` explaining
 * why it is unavailable.
 */
export function Toolbar(props: { actions: ToolbarAction[] }) {
  return (
    <div className="toolbar">
      {props.actions.map((action) => {
        const Icon = action.icon;
        const unavailable = action.unavailable !== undefined;
        const reasonId = `toolbar-${action.id}-reason`;
        return (
          <span key={action.id} className="toolbar-slot">
            <button
              type="button"
              className="ghost"
              aria-label={action.label}
              title={action.label}
              aria-disabled={unavailable ? true : undefined}
              aria-describedby={unavailable ? reasonId : undefined}
              onClick={() => { if (!unavailable) action.onActivate(); }}
            >
              <Icon />
            </button>
            {unavailable && <span id={reasonId} className="sr-only">{action.unavailable}</span>}
          </span>
        );
      })}
    </div>
  );
}
```

- [ ] **Step 4: Run it to make sure it passes**

```bash
cd ui && pnpm --filter @motiv/rules-demo test Toolbar
```
Expected: PASS, 5 tests.

- [ ] **Step 5: Prove the guards bite**

| Mutation | Test that must fail |
|---|---|
| Drop the `if (!unavailable)` guard from `onClick` | keeps an unavailable action reachable and does not activate it |
| Swap `aria-disabled` for `disabled` | keeps an unavailable action reachable (the `hasAttribute('disabled')` assertion) |
| Always render `aria-describedby={reasonId}` | does not describe an available action |
| Delete `aria-label` | names each icon button |

- [ ] **Step 6: Add the ghost styles**

Append to `ui/apps/demo/src/styles/app.css`:

```css
/* Ghost: invisible at rest, a soft well on hover and focus. The whole toolbar treatment. */
.ghost {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  padding: 6px;
  border: none;
  border-radius: var(--radius);
  background: none;
  color: var(--muted);
  cursor: pointer;
}

.ghost:hover { background: var(--sh-inset); color: var(--text); }
.ghost:focus-visible { outline: 2px solid var(--accent); outline-offset: 1px; }

/* Unavailable actions stay focusable — see Toolbar.tsx — so this is the only signal that they
   are inert, and it must not rely on :disabled. */
.ghost[aria-disabled='true'] { color: var(--faint); cursor: default; }
.ghost[aria-disabled='true']:hover { background: none; color: var(--faint); }

.toolbar { display: flex; align-items: center; gap: 4px; }
.toolbar-slot { display: inline-flex; }

/* Visible to assistive technology, not to the eye. */
.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  margin: -1px;
  overflow: hidden;
  clip-path: inset(50%);
  white-space: nowrap;
  border: 0;
}
```

- [ ] **Step 7: Run the whole suite and typecheck**

```bash
cd ui && pnpm -r test && pnpm -r typecheck
```
Expected: PASS, 354 demo tests.

- [ ] **Step 8: Commit**

```bash
git add ui/apps/demo/src/shell/Toolbar.tsx ui/apps/demo/test/shell/Toolbar.test.tsx ui/apps/demo/src/styles/app.css
git commit -m "feat(demo): ghost icon toolbar whose unavailable actions stay reachable"
```

---

### Task 4: `CommandPalette`

Generic over its item type. Knows nothing about propositions or rules.

**Files:**
- Create: `ui/apps/demo/src/shell/CommandPalette.tsx`
- Test: `ui/apps/demo/test/shell/CommandPalette.test.tsx`

**Interfaces:**
- Consumes: `Modal` (Task 2), `IconSearch` (Task 1).
- Produces:

```ts
export interface PaletteItem { id: string; }

export function CommandPalette<T extends PaletteItem>(props: {
  label: string;
  placeholder: string;
  items: T[];
  /** Whether an item survives the current query. */
  match: (item: T, query: string) => boolean;
  /** One row. `highlighted` drives the visual highlight only — selection is `onChoose`. */
  renderItem: (item: T, highlighted: boolean) => ReactNode;
  /** Rendered instead of the flat list when the query is empty. Optional: without it, an empty
   *  query lists every item. */
  renderBrowse?: () => ReactNode;
  onChoose: (item: T) => void;
  onClose: () => void;
  /** Actions for the highlighted item, rendered in the footer. Omit for a chooser with none. */
  footer?: (highlighted: T | null) => ReactNode;
}): JSX.Element
```

- [ ] **Step 1: Write the failing test**

Create `ui/apps/demo/test/shell/CommandPalette.test.tsx`:

```tsx
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CommandPalette } from '../../src/shell/CommandPalette.js';

interface Row { id: string; }
const ROWS: Row[] = [{ id: 'customer.is-active' }, { id: 'customer.is-adult' }, { id: 'orders.is-large' }];

const setup = (overrides: Partial<Parameters<typeof CommandPalette<Row>>[0]> = {}) => {
  const onChoose = vi.fn();
  const onClose = vi.fn();
  render(
    <CommandPalette<Row>
      label="Propositions"
      placeholder="Filter…"
      items={ROWS}
      match={(item, query) => item.id.includes(query)}
      renderItem={(item) => item.id}
      onChoose={onChoose}
      onClose={onClose}
      {...overrides}
    />,
  );
  return { onChoose, onClose };
};

describe('CommandPalette', () => {
  it('opens with the search input focused, so typing needs no click', () => {
    setup();
    expect(document.activeElement).toBe(screen.getByRole('combobox'));
  });

  it('filters to matching rows as the query is typed', async () => {
    setup();
    await userEvent.type(screen.getByRole('combobox'), 'adult');
    const options = screen.getAllByRole('option');
    expect(options).toHaveLength(1);
    expect(options[0]!.textContent).toBe('customer.is-adult');
  });

  it('moves the highlight with the arrow keys without moving focus off the input', async () => {
    // aria-activedescendant is what lets the highlight move while the caret stays put; without
    // it every arrow key would be a round trip out of the search box and back.
    setup();
    const input = screen.getByRole('combobox');
    await userEvent.keyboard('{ArrowDown}');
    expect(document.activeElement).toBe(input);
    expect(input.getAttribute('aria-activedescendant'))
      .toBe(screen.getAllByRole('option')[1]!.id);
  });

  it('chooses the highlighted row on Enter', async () => {
    const { onChoose } = setup();
    await userEvent.keyboard('{ArrowDown}{Enter}');
    expect(onChoose).toHaveBeenCalledWith(ROWS[1]);
  });

  it('chooses the row that was clicked, not the one highlighted', async () => {
    // A mouse user never touched the arrow keys, so choosing the highlight would select
    // something they never pointed at.
    const { onChoose } = setup();
    await userEvent.keyboard('{ArrowDown}');
    await userEvent.click(screen.getByText('orders.is-large'));
    expect(onChoose).toHaveBeenCalledWith(ROWS[2]);
    expect(onChoose).toHaveBeenCalledTimes(1);
  });

  it('resets the highlight when the query changes', async () => {
    // The row under the highlight is not the row that was under it before the list changed.
    const { onChoose } = setup();
    await userEvent.keyboard('{ArrowDown}{ArrowDown}');
    await userEvent.type(screen.getByRole('combobox'), 'customer');
    await userEvent.keyboard('{Enter}');
    expect(onChoose).toHaveBeenCalledWith(ROWS[0]);
  });

  it('browses instead of listing when the query is empty', async () => {
    setup({ renderBrowse: () => <p>browse view</p> });
    expect(screen.getByText('browse view')).toBeTruthy();
    await userEvent.type(screen.getByRole('combobox'), 'orders');
    expect(screen.queryByText('browse view')).toBeNull();
    expect(screen.getAllByRole('option')).toHaveLength(1);
  });

  it('hands the footer the highlighted item', async () => {
    setup({ footer: (highlighted) => <span>target: {highlighted?.id ?? 'none'}</span> });
    await userEvent.type(screen.getByRole('combobox'), 'orders');
    expect(screen.getByText(/target: orders.is-large/)).toBeTruthy();
  });

  it('does nothing on Enter when nothing matched', async () => {
    const { onChoose } = setup();
    await userEvent.type(screen.getByRole('combobox'), 'nothing-matches-this');
    await userEvent.keyboard('{Enter}');
    expect(onChoose).not.toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Run it to make sure it fails**

```bash
cd ui && pnpm --filter @motiv/rules-demo test CommandPalette
```
Expected: FAIL — cannot resolve `../../src/shell/CommandPalette.js`.

- [ ] **Step 3: Write the component**

Create `ui/apps/demo/src/shell/CommandPalette.tsx`:

```tsx
import { useMemo, useRef, useState, type KeyboardEvent, type ReactNode } from 'react';
import { Modal } from './Modal.js';
import { IconSearch } from './icons.js';

/** The least a palette needs to know about a row: something stable to key and address it by. */
export interface PaletteItem { id: string; }

/**
 * Search-first chooser. Opens with the caret in the search box and reopens fresh every time —
 * a palette still holding the previous query is a palette that has to be cleared before use.
 *
 * With no query it renders `renderBrowse` if given, so a namespaced set stays browsable; once
 * anything is typed the results flatten to one row per match, because hierarchy is noise in a
 * result list.
 */
export function CommandPalette<T extends PaletteItem>(props: {
  label: string;
  placeholder: string;
  items: T[];
  match: (item: T, query: string) => boolean;
  renderItem: (item: T, highlighted: boolean) => ReactNode;
  renderBrowse?: () => ReactNode;
  onChoose: (item: T) => void;
  onClose: () => void;
  footer?: (highlighted: T | null) => ReactNode;
}) {
  const [query, setQuery] = useState('');
  const [cursor, setCursor] = useState(0);
  const listId = useRef(`palette-${Math.random().toString(36).slice(2)}`).current;

  const trimmed = query.trim();
  const browsing = trimmed === '' && props.renderBrowse !== undefined;

  const matches = useMemo(
    () => (trimmed === '' ? props.items : props.items.filter((item) => props.match(item, trimmed))),
    // `props.match` is intentionally not a dependency: callers pass an inline arrow, so including
    // it would recompute on every render and defeat the memo entirely.
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [props.items, trimmed],
  );

  // Derived, not corrected in an effect: when the query changes the list changes under the
  // highlight, and the row at index N is no longer the row the user was looking at.
  const highlighted = matches[cursor] ?? matches[0] ?? null;
  const highlightIndex = highlighted === null ? -1 : matches.indexOf(highlighted);

  const optionId = (index: number): string => `${listId}-option-${index}`;

  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>): void => {
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      setCursor(Math.min(highlightIndex + 1, matches.length - 1));
      return;
    }
    if (event.key === 'ArrowUp') {
      event.preventDefault();
      setCursor(Math.max(highlightIndex - 1, 0));
      return;
    }
    if (event.key === 'Enter' && highlighted !== null) {
      event.preventDefault();
      props.onChoose(highlighted);
    }
  };

  return (
    <Modal label={props.label} onClose={props.onClose} className="palette" fullscreenOnMobile>
      <div className="palette-search">
        <IconSearch size={15} />
        <input
          type="text"
          role="combobox"
          autoFocus
          className="palette-input"
          aria-label={props.placeholder}
          aria-expanded={!browsing}
          aria-controls={listId}
          aria-activedescendant={highlightIndex >= 0 ? optionId(highlightIndex) : undefined}
          placeholder={props.placeholder}
          value={query}
          onChange={(event) => { setQuery(event.target.value); setCursor(0); }}
          onKeyDown={onKeyDown}
        />
        {!browsing && <span className="palette-count">{matches.length} of {props.items.length}</span>}
      </div>

      {browsing
        ? <div className="palette-browse">{props.renderBrowse?.()}</div>
        : (
          <ul id={listId} role="listbox" aria-label={props.label} className="palette-list">
            {matches.map((item, index) => (
              <li
                key={item.id}
                id={optionId(index)}
                role="option"
                aria-selected={index === highlightIndex}
                className={index === highlightIndex ? 'palette-row highlighted' : 'palette-row'}
                onClick={() => props.onChoose(item)}
              >
                {props.renderItem(item, index === highlightIndex)}
              </li>
            ))}
          </ul>
        )}

      {props.footer !== undefined && (
        <div className="palette-footer">{props.footer(highlighted)}</div>
      )}
    </Modal>
  );
}
```

- [ ] **Step 4: Run it to make sure it passes**

```bash
cd ui && pnpm --filter @motiv/rules-demo test CommandPalette
```
Expected: PASS, 9 tests.

- [ ] **Step 5: Prove the guards bite**

| Mutation | Test that must fail |
|---|---|
| Delete `setCursor(0)` from `onChange` | resets the highlight when the query changes |
| `onClick={() => props.onChoose(highlighted!)}` on the row | chooses the row that was clicked, not the one highlighted |
| Drop `aria-activedescendant` | moves the highlight with the arrow keys |
| Drop `autoFocus` | opens with the search input focused |
| Drop the `highlighted !== null` guard on Enter | does nothing on Enter when nothing matched (expect a throw, which is still a failure) |
| Always render the list, ignoring `browsing` | browses instead of listing when the query is empty |

- [ ] **Step 6: Add the styles**

Append to `ui/apps/demo/src/styles/app.css`:

```css
.palette { max-width: min(560px, 92vw); }

.palette-search {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 12px;
  border-bottom: 1px solid var(--border);
  color: var(--faint);
}

.palette-input {
  flex: 1;
  border: none;
  background: none;
  color: var(--text);
  font: 14px var(--sans);
  outline: none;
}

.palette-count { font: 11px var(--sans); color: var(--faint); }

.palette-list { list-style: none; margin: 0; padding: 4px 0; max-height: 46vh; overflow-y: auto; }
.palette-browse { padding: 4px 0; max-height: 46vh; overflow-y: auto; }

.palette-row { padding: 5px 12px; font: 13px var(--sans); color: var(--text); cursor: pointer; }
.palette-row:hover { background: var(--sh-inset); }
.palette-row.highlighted { background: var(--accent-weak); }

.palette-footer {
  display: flex;
  align-items: center;
  gap: 3px;
  padding: 6px 8px;
  border-top: 1px solid var(--border);
  background: var(--surface);
}

@media (max-width: 900px) {
  .palette-list, .palette-browse { max-height: none; flex: 1; }
  .palette { display: flex; flex-direction: column; }
}
```

- [ ] **Step 7: Run the whole suite and typecheck**

```bash
cd ui && pnpm -r test && pnpm -r typecheck
```
Expected: PASS, 363 demo tests.

- [ ] **Step 8: Commit**

```bash
git add ui/apps/demo/src/shell/CommandPalette.tsx ui/apps/demo/test/shell/CommandPalette.test.tsx ui/apps/demo/src/styles/app.css
git commit -m "feat(demo): a search-first command palette that browses when idle"
```

---

### Task 5: `AppBar` — glyph-prefixed navigation

**Files:**
- Modify: `ui/apps/demo/src/panes/AppBar.tsx`
- Test: `ui/apps/demo/test/panes/AppBar.test.tsx` *(create if absent)*

**Interfaces:**
- Consumes: `IconRules`, `IconPropositions` (Task 1).
- Produces: `AppBar`'s props are unchanged — `{ page, onNavigate, controls?, children? }`. Only its rendering changes, so no caller needs editing.

- [ ] **Step 1: Write the failing test**

Create `ui/apps/demo/test/panes/AppBar.test.tsx`:

```tsx
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AppBar } from '../../src/panes/AppBar.js';

describe('AppBar', () => {
  it('keeps navigation labels as words, so the destination is readable', () => {
    render(<AppBar page="rules" onNavigate={() => {}} />);
    expect(screen.getByRole('tab', { name: 'Rules' })).toBeTruthy();
    expect(screen.getByRole('tab', { name: 'Propositions' })).toBeTruthy();
  });

  it('marks the current page as selected', () => {
    render(<AppBar page="propositions" onNavigate={() => {}} />);
    expect(screen.getByRole('tab', { name: 'Propositions' }).getAttribute('aria-selected')).toBe('true');
    expect(screen.getByRole('tab', { name: 'Rules' }).getAttribute('aria-selected')).toBe('false');
  });

  it('navigates on click', async () => {
    const onNavigate = vi.fn();
    render(<AppBar page="rules" onNavigate={onNavigate} />);
    await userEvent.click(screen.getByRole('tab', { name: 'Propositions' }));
    expect(onNavigate).toHaveBeenCalledWith('propositions');
  });

  it('hides the nav glyph from the accessible name', () => {
    // If the glyph were exposed, the tab would announce as something other than its word.
    const { container } = render(<AppBar page="rules" onNavigate={() => {}} />);
    const glyphs = container.querySelectorAll('.page-tabs svg');
    expect(glyphs.length).toBe(2);
    glyphs.forEach((glyph) => expect(glyph.getAttribute('aria-hidden')).toBe('true'));
  });
});
```

- [ ] **Step 2: Run it to make sure it fails**

```bash
cd ui && pnpm --filter @motiv/rules-demo test AppBar
```
Expected: FAIL on "hides the nav glyph" — there are no glyphs yet, so `glyphs.length` is 0.

- [ ] **Step 3: Add the glyphs**

In `ui/apps/demo/src/panes/AppBar.tsx`, change the `PAGES` constant and the button body:

```tsx
import { IconPropositions, IconRules, type IconProps } from '../shell/icons.js';

/** The pages, in the order they are offered. */
const PAGES: ReadonlyArray<{ id: Page; label: string; icon: (props: IconProps) => JSX.Element }> = [
  { id: 'rules', label: 'Rules', icon: IconRules },
  { id: 'propositions', label: 'Propositions', icon: IconPropositions },
];
```

and inside the `map`, replacing `{label}`:

```tsx
{PAGES.map(({ id, label, icon: Icon }) => {
  const active = props.page === id;
  return (
    <button
      key={id}
      type="button"
      role="tab"
      aria-selected={active}
      className={active ? 'tab active' : 'tab'}
      onClick={() => props.onNavigate(id)}
    >
      <Icon size={13} />
      {label}
    </button>
  );
})}
```

Leave the long comment above `.page-tabs` exactly as it is — the `role="tablist"` gap it records is still open and still deliberately out of scope.

- [ ] **Step 4: Run it to make sure it passes**

```bash
cd ui && pnpm --filter @motiv/rules-demo test AppBar
```
Expected: PASS, 4 tests.

- [ ] **Step 5: Align the tab to its glyph**

In `ui/apps/demo/src/styles/app.css`, find the `.tab` rule and add:

```css
.tab { display: inline-flex; align-items: center; gap: 6px; }
```

- [ ] **Step 6: Run the whole suite and typecheck**

```bash
cd ui && pnpm -r test && pnpm -r typecheck
```
Expected: PASS, 367 demo tests.

- [ ] **Step 7: Commit**

```bash
git add ui/apps/demo/src/panes/AppBar.tsx ui/apps/demo/test/panes/AppBar.test.tsx ui/apps/demo/src/styles/app.css
git commit -m "feat(demo): prefix page navigation with a discrete glyph"
```

---

### Task 6: `DocumentModal` — the JSON viewer

**Files:**
- Create: `ui/apps/demo/src/panes/DocumentModal.tsx`
- Test: `ui/apps/demo/test/panes/DocumentModal.test.tsx`

**Interfaces:**
- Consumes: `Modal` (Task 2).
- Produces: `DocumentModal`, taking `{ onClose: () => void }`. Reads the document from the shared editor store exactly as `JsonPane` does, so it needs no props for content.

- [ ] **Step 1: Write the failing test**

Create `ui/apps/demo/test/panes/DocumentModal.test.tsx`. Read `ui/apps/demo/test/panes/JsonPane.test.tsx` first and reuse its store-priming helper verbatim — the store wiring is the same and duplicating it differently would make the two tests disagree about setup.

```tsx
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { DocumentModal } from '../../src/panes/DocumentModal.js';

describe('DocumentModal', () => {
  it('shows the live document as formatted JSON', () => {
    render(<DocumentModal onClose={() => {}} />);
    expect(screen.getByLabelText('rule document')).toBeTruthy();
  });

  it('names itself for assistive technology', () => {
    render(<DocumentModal onClose={() => {}} />);
    expect(screen.getByRole('dialog', { name: /document/i })).toBeTruthy();
  });

  it('reports dismissal', async () => {
    const onClose = vi.fn();
    render(<DocumentModal onClose={onClose} />);
    await userEvent.click(screen.getByRole('button', { name: /close/i }));
    expect(onClose).toHaveBeenCalledTimes(1);
  });
});
```

- [ ] **Step 2: Run it to make sure it fails**

```bash
cd ui && pnpm --filter @motiv/rules-demo test DocumentModal
```
Expected: FAIL — cannot resolve `../../src/panes/DocumentModal.js`.

- [ ] **Step 3: Write the component**

Create `ui/apps/demo/src/panes/DocumentModal.tsx`. The body is `JsonPane`'s, unchanged — this is a relocation, not a rewrite:

```tsx
import { useRuleEditor, useRuleEditorStore } from '@motiv/rules-react';
import { Modal } from '../shell/Modal.js';

/**
 * The live document, as JSON, in a modal.
 *
 * The same content `JsonPane` used to render beside the editor. Moving it behind the toolbar
 * gives both pages the column back; nothing is lost, because it is one keystroke away.
 */
export function DocumentModal(props: { onClose: () => void }) {
  const store = useRuleEditorStore();
  const state = useRuleEditor(store);

  return (
    <Modal label="Document" onClose={props.onClose} className="document-modal" fullscreenOnMobile>
      <h2 className="modal-title">Document<span className="pane-badge">read-only · live</span></h2>
      <div className="modal-body">
        <pre aria-label="rule document" className="json">{JSON.stringify(state.document, null, 2)}</pre>
        {state.errors.length > 0 && (
          <ul aria-label="validation errors" className="errors">
            {state.errors.map((error, i) => (
              <li key={`${error.path}-${i}`} role="alert" className="error">
                {error.code} at {error.path}: {error.message}
              </li>
            ))}
          </ul>
        )}
      </div>
    </Modal>
  );
}
```

- [ ] **Step 4: Run it to make sure it passes**

```bash
cd ui && pnpm --filter @motiv/rules-demo test DocumentModal
```
Expected: PASS, 3 tests.

- [ ] **Step 5: Add the styles**

Append to `ui/apps/demo/src/styles/app.css`:

```css
.modal-title {
  display: flex;
  align-items: center;
  gap: 8px;
  margin: 0;
  padding: 10px 12px;
  border-bottom: 1px solid var(--border);
  font: 600 13px var(--sans);
  color: var(--text);
}

.modal-body { padding: 10px 12px; max-height: 60vh; overflow: auto; }

@media (max-width: 900px) {
  .modal-mobile-full .modal-body { max-height: none; flex: 1; }
  .modal-mobile-full { display: flex; flex-direction: column; }
}
```

- [ ] **Step 6: Run the whole suite and typecheck**

```bash
cd ui && pnpm -r test && pnpm -r typecheck
```
Expected: PASS, 370 demo tests. `JsonPane` is still mounted by both pages at this point and its tests still pass — it is removed in Tasks 7 and 8.

- [ ] **Step 7: Commit**

```bash
git add ui/apps/demo/src/panes/DocumentModal.tsx ui/apps/demo/test/panes/DocumentModal.test.tsx ui/apps/demo/src/styles/app.css
git commit -m "feat(demo): the live document as a modal"
```

---

### Task 7: Propositions page — palette and toolbar

The largest task. The explorer stops being a rail and becomes the palette's contents.

**Files:**
- Modify: `ui/apps/demo/src/explorer/PropositionExplorer.tsx`
- Modify: `ui/apps/demo/src/panes/PropositionsPage.tsx`
- Test: `ui/apps/demo/test/explorer/PropositionExplorer.test.tsx`, `ui/apps/demo/test/panes/PropositionsPage.test.tsx`

**Interfaces:**
- Consumes: `CommandPalette` (Task 4), `Toolbar` + `ToolbarAction` (Task 3), `DocumentModal` (Task 6), all icons (Task 1).
- Produces: `PropositionExplorer` keeps its `ExplorerActions` interface unchanged but gains `onClose: () => void` in that interface, and renders the palette rather than an `<aside>`.

- [ ] **Step 1: Write the failing tests**

Add to `ui/apps/demo/test/panes/PropositionsPage.test.tsx`. Read the file first and reuse its existing `renderPage` helper and client stub rather than building new ones.

```tsx
it('opens the explorer from the toolbar', async () => {
  renderPage();
  expect(screen.queryByRole('dialog', { name: 'Propositions' })).toBeNull();
  await userEvent.click(screen.getByRole('button', { name: 'Open' }));
  expect(screen.getByRole('dialog', { name: 'Propositions' })).toBeTruthy();
});

it('opens the explorer with ⌘K', async () => {
  renderPage();
  await userEvent.keyboard('{Meta>}k{/Meta}');
  expect(screen.getByRole('dialog', { name: 'Propositions' })).toBeTruthy();
});

it('opens the explorer fresh, discarding the previous query', async () => {
  // The palette unmounts on close, so this only holds while the query lives inside it. Move
  // that state up to the page and this breaks.
  renderPage();
  await userEvent.click(screen.getByRole('button', { name: 'Open' }));
  await userEvent.type(screen.getByRole('combobox'), 'adult');
  await userEvent.click(screen.getByRole('button', { name: /close/i }));

  await userEvent.click(screen.getByRole('button', { name: 'Open' }));
  expect(screen.getByRole('combobox')).toHaveProperty('value', '');
});

it('closes the explorer once a proposition is chosen', async () => {
  renderPage();
  await userEvent.click(screen.getByRole('button', { name: 'Open' }));
  await userEvent.type(screen.getByRole('combobox'), 'is-adult');
  await userEvent.keyboard('{Enter}');
  expect(screen.queryByRole('dialog', { name: 'Propositions' })).toBeNull();
});

it('opens the document viewer from the toolbar', async () => {
  renderPage();
  await userEvent.click(screen.getByRole('button', { name: 'JSON' }));
  expect(screen.getByRole('dialog', { name: /document/i })).toBeTruthy();
});

it('explains why Save is unavailable for a name only served by a compiled spec', async () => {
  // Gated on the load becoming observable first: Save is also unavailable while nothing is
  // loaded, so asserting before the load can pass without the version rule existing at all.
  renderPage({ selected: 'customer.is-adult' });   // origin Compiled, version 0
  await screen.findByText('v0');

  const save = screen.getByRole('button', { name: 'Save' });
  expect(save.getAttribute('aria-disabled')).toBe('true');
  const reason = save.getAttribute('aria-describedby');
  expect(document.getElementById(reason!)?.textContent).toMatch(/compiled/i);
});
```

- [ ] **Step 2: Run them to make sure they fail**

```bash
cd ui && pnpm --filter @motiv/rules-demo test PropositionsPage
```
Expected: FAIL — no button named `Open`.

- [ ] **Step 3: Turn the explorer into palette contents**

In `ui/apps/demo/src/explorer/PropositionExplorer.tsx`:

Add `onClose` to the actions interface:

```tsx
export interface ExplorerActions {
  onSelect: (name: string) => void;
  onDerive: (name: string) => void;
  onOverride: (name: string) => void;
  onNew: () => void;
  onDelete: (entry: PropositionListEntry) => void;
  /** Dismiss without choosing. */
  onClose: () => void;
}
```

Replace the returned `<aside>` with a `CommandPalette`. The tree renders through `renderBrowse` so namespaces stay browsable with no query; matches flatten once one is typed. Keep `TreeNode`, `buildNamespaceTree`, `filterTree` and the model chips exactly as they are — they move, they do not change.

```tsx
return (
  <CommandPalette<PropositionListEntry>
    label="Propositions"
    placeholder="Filter propositions"
    items={entries}
    match={(entry, needle) => entry.name.toLowerCase().includes(needle.toLowerCase())}
    renderItem={(entry) => {
      const cut = entry.name.lastIndexOf('.');
      return (
        <span className="palette-name">
          {cut > 0 && <span className="palette-ns">{entry.name.slice(0, cut + 1)}</span>}
          {entry.name.slice(cut + 1)}
          <span className="palette-badge">{ORIGIN_LABEL[entry.origin]}</span>
        </span>
      );
    }}
    renderBrowse={() => (
      shown === 0
        ? <p className="explorer-empty">{emptyMessage}</p>
        : (
          <ul className="explorer-tree" role="tree" aria-label="Proposition namespaces">
            {filtered.map((node) => (
              <TreeNode key={node.path} node={node} depth={0} selected={selected} onSelect={choose} />
            ))}
          </ul>
        )
    )}
    onChoose={(entry) => choose(entry.name)}
    onClose={actions.onClose}
    footer={(highlighted) => <ExplorerFooter
      entry={highlighted ?? selectedEntry ?? null}
      entries={entries}
      actions={actions}
    />}
  />
);
```

with, above the return:

```tsx
// Choosing is the palette's whole purpose, so it closes on the way out rather than leaving the
// user to dismiss a modal they are finished with.
const choose = (name: string): void => { actions.onSelect(name); actions.onClose(); };
```

The `query`/`models` state and the `filterTree` call stay for the browse view; the palette owns its own search for the flat view. Extract the action buttons into a local `ExplorerFooter` component in the same file, carrying over the existing `canOverride` rule and the `Revert to compiled` / `Delete` label switch **unchanged** — both are load-bearing and both have tests.

Every footer button uses `aria-disabled` with a reason, per the global constraint, rather than being conditionally rendered.

- [ ] **Step 4: Wire the page**

In `ui/apps/demo/src/panes/PropositionsPage.tsx`:

Add open state and the keyboard shortcut:

```tsx
const [explorerOpen, setExplorerOpen] = useState(false);
const [documentOpen, setDocumentOpen] = useState(false);

useEffect(() => {
  const onKeyDown = (event: KeyboardEvent): void => {
    if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 'k') {
      event.preventDefault();
      setExplorerOpen(true);
    }
  };
  window.addEventListener('keydown', onKeyDown);
  return () => window.removeEventListener('keydown', onKeyDown);
}, []);
```

Replace the `controls` prop with the toolbar:

```tsx
controls={
  <>
    {loaded && <span className="rule-version">v{loaded.version}</span>}
    <Toolbar actions={[
      { id: 'open', label: 'Open', icon: IconOpen, onActivate: () => setExplorerOpen(true) },
      {
        id: 'save',
        label: 'Save',
        icon: IconSave,
        onActivate: () => void save(),
        // Version 0 is the contract's "purely compiled": no overlay document exists for a PUT
        // to update, and `baseVersion` must be positive, so Save could only ever fail there.
        // Authoring one is what Override is for.
        unavailable: loaded === null
          ? 'Nothing loaded yet.'
          : loaded.version === 0
            ? 'This name is served by a compiled spec. Use Override to author one.'
            : saving ? 'Saving…' : undefined,
      },
      { id: 'json', label: 'JSON', icon: IconJson, onActivate: () => setDocumentOpen(true) },
    ]} />
  </>
}
```

Replace the `shell-body` block — the explorer and `JsonPane` leave, `with-rail` goes with them:

```tsx
<div className="shell-body">
  <EditorPane client={props.client} />
  <EvaluatePane client={props.client} />
</div>

{explorerOpen && (
  <PropositionExplorer
    entries={entries}
    selected={props.selected}
    actions={{ /* …existing handlers…, */ onClose: () => setExplorerOpen(false) }}
  />
)}

{documentOpen && <DocumentModal onClose={() => setDocumentOpen(false)} />}
```

Each of `onDerive`, `onOverride` and `onNew` must also call `setExplorerOpen(false)` — they open `PropositionDialog`, and two stacked modals would trap focus in the wrong one.

- [ ] **Step 5: Run the tests to make sure they pass**

```bash
cd ui && pnpm --filter @motiv/rules-demo test PropositionsPage PropositionExplorer
```
Expected: PASS. Existing explorer tests that queried the `<aside>` by its `Propositions` label now find the dialog by the same name and should still pass; any that assert rail-specific structure need rewriting against the palette, not deleting.

- [ ] **Step 6: Prove the guards bite**

| Mutation | Test that must fail |
|---|---|
| Lift `query` state from the palette up into `PropositionsPage` | opens the explorer fresh, discarding the previous query |
| Drop `actions.onClose()` from `choose` | closes the explorer once a proposition is chosen |
| Drop the `loaded.version === 0` arm from Save's `unavailable` | explains why Save is unavailable |
| Drop `event.preventDefault()` in the ⌘K handler | none — note it; jsdom raises no browser default to prevent |

- [ ] **Step 7: Add the row styles**

Append to `ui/apps/demo/src/styles/app.css`:

```css
.palette-name { display: flex; align-items: center; gap: 6px; font-family: var(--mono); font-size: 12px; }
.palette-ns { color: var(--faint); }
.palette-badge {
  margin-left: auto;
  font: 10px var(--sans);
  color: var(--faint);
  border: 1px solid var(--border);
  border-radius: 4px;
  padding: 0 4px;
}
```

- [ ] **Step 8: Run the whole suite and typecheck**

```bash
cd ui && pnpm -r test && pnpm -r typecheck
```
Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add ui/apps/demo/src/explorer/PropositionExplorer.tsx ui/apps/demo/src/panes/PropositionsPage.tsx ui/apps/demo/test ui/apps/demo/src/styles/app.css
git commit -m "feat(demo): choose propositions from a palette, not a rail"
```

---

### Task 8: Rules page — same toolbar, same palette

**Files:**
- Modify: `ui/apps/demo/src/panes/RuleHeader.tsx`
- Modify: `ui/apps/demo/src/panes/RulesPage.tsx`
- Test: `ui/apps/demo/test/panes/RuleHeader.test.tsx`

**Interfaces:**
- Consumes: `CommandPalette` (Task 4), `Toolbar` (Task 3), `DocumentModal` (Task 6).
- Produces: nothing new.

- [ ] **Step 1: Write the failing tests**

Add to `ui/apps/demo/test/panes/RuleHeader.test.tsx`, reusing its existing render helper:

```tsx
it('chooses a rule from the palette', async () => {
  renderHeader();
  await userEvent.click(screen.getByRole('button', { name: 'Open' }));
  await userEvent.type(screen.getByRole('combobox'), 'fraud');
  await userEvent.keyboard('{Enter}');
  expect(await screen.findByText('fraud-screening')).toBeTruthy();
  expect(screen.queryByRole('dialog', { name: 'Rules' })).toBeNull();
});

it('offers no authoring actions, because rules are not authored here', async () => {
  // The footer is caller-supplied precisely so this page can omit it. Rules are placeholders
  // for compile-time logic; there is nothing to create, derive or delete.
  renderHeader();
  await userEvent.click(screen.getByRole('button', { name: 'Open' }));
  expect(screen.queryByRole('button', { name: /^new$/i })).toBeNull();
  expect(screen.queryByRole('button', { name: /derive/i })).toBeNull();
  expect(screen.queryByRole('button', { name: /delete/i })).toBeNull();
});

it('opens the document viewer from the toolbar', async () => {
  renderHeader();
  await userEvent.click(screen.getByRole('button', { name: 'JSON' }));
  expect(screen.getByRole('dialog', { name: /document/i })).toBeTruthy();
});
```

- [ ] **Step 2: Run them to make sure they fail**

```bash
cd ui && pnpm --filter @motiv/rules-demo test RuleHeader
```
Expected: FAIL — no button named `Open`.

- [ ] **Step 3: Replace the picker with the palette**

In `ui/apps/demo/src/panes/RuleHeader.tsx`, remove the `ListboxPicker` import and its use in the breadcrumb, leaving the current rule as text:

```tsx
<span className="breadcrumb-current">{loaded?.name ?? LOCAL_DRAFT.value}</span>
```

Add open state and the same `⌘K` effect as Task 7, then render the palette with **no footer** — this is the whole reason `footer` is optional:

```tsx
{picking && (
  <CommandPalette<{ id: string }>
    label="Rules"
    placeholder="Filter rules"
    items={options.map((option) => ({ id: option.value }))}
    match={(item, needle) => item.id.toLowerCase().includes(needle.toLowerCase())}
    renderItem={(item) => <span className="palette-name">{item.id}</span>}
    onChoose={(item) => { void load(item.id); setPicking(false); }}
    onClose={() => setPicking(false)}
  />
)}
```

Replace `controls` with the toolbar, mirroring Task 7 but with only the two universal reasons for Save being unavailable:

```tsx
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
        unavailable: loaded === null ? 'Nothing loaded yet.' : saving ? 'Saving…' : undefined,
      },
      { id: 'json', label: 'JSON', icon: IconJson, onActivate: () => setDocumentOpen(true) },
    ]} />
  </>
}
```

and render `{documentOpen && <DocumentModal onClose={() => setDocumentOpen(false)} />}`.

- [ ] **Step 4: Drop the JSON pane from the Rules page**

In `ui/apps/demo/src/panes/RulesPage.tsx`, remove the `JsonPane` import and its element from `shell-body`. Leave `CheckoutPane` where it is.

- [ ] **Step 5: Run the tests to make sure they pass**

```bash
cd ui && pnpm --filter @motiv/rules-demo test RuleHeader RulesPage
```
Expected: PASS.

- [ ] **Step 6: Prove the guards bite**

| Mutation | Test that must fail |
|---|---|
| Pass the Propositions footer to the Rules palette | offers no authoring actions |
| Drop `setPicking(false)` from `onChoose` | chooses a rule from the palette |

- [ ] **Step 7: Delete `JsonPane` and its test**

Both pages now use `DocumentModal`. `grep -rn "JsonPane" ui/apps/demo/src ui/apps/demo/test` must return nothing before deleting `src/panes/JsonPane.tsx` and `test/panes/JsonPane.test.tsx`.

- [ ] **Step 8: Run the whole suite and typecheck**

```bash
cd ui && pnpm -r test && pnpm -r typecheck
```
Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add -A ui/apps/demo
git commit -m "feat(demo): one shell for both pages, and the json pane retires"
```

---

### Task 9: `PropositionDialog` onto `Modal`

**Files:**
- Modify: `ui/apps/demo/src/explorer/PropositionDialog.tsx`
- Modify: `ui/apps/demo/test/explorer/PropositionDialog.test.tsx`
- Modify: `ui/apps/demo/src/styles/app.css`

**Interfaces:**
- Consumes: `Modal` (Task 2).
- Produces: nothing new.

- [ ] **Step 1: Migrate the dialog**

In `ui/apps/demo/src/explorer/PropositionDialog.tsx`, replace the hand-rolled backdrop:

```tsx
return (
  <div className="dialog-backdrop" role="presentation">
    <div className="dialog" role="dialog" aria-modal="true" aria-label={props.seed.title}>
```

with:

```tsx
return (
  <Modal label={props.seed.title} onClose={props.onCancel} className="dialog" fullscreenOnMobile>
```

closing with `</Modal>`. Delete the `.dialog-backdrop` rule from `app.css`.

This closes a recorded defect: the dialog set `aria-modal="true"` — which instructs assistive technology to hide everything outside it — while never moving focus into itself and never handling Escape. `showModal()` does both.

Its `aria-describedby`-on-a-disabled-button problem is the same one `Toolbar` already solved, so switch the Create button to `aria-disabled` with an early-returning handler, matching `Toolbar`'s pattern exactly.

- [ ] **Step 2: Run the dialog's tests**

```bash
cd ui && pnpm --filter @motiv/rules-demo test PropositionDialog
```
Expected: PASS. Tests that queried `.dialog-backdrop` need updating to `getByRole('dialog')`; tests asserting the *disabled* Create button must assert `aria-disabled` now.

- [ ] **Step 3: Run the whole suite and typecheck**

```bash
cd ui && pnpm -r test && pnpm -r typecheck
```
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add ui/apps/demo/src/explorer/PropositionDialog.tsx ui/apps/demo/test/explorer/PropositionDialog.test.tsx ui/apps/demo/src/styles/app.css
git commit -m "fix(demo): give the authoring dialog a real modal, and a reachable reason"
```

---

### Task 10: e2e rewrite, simplification, and full verification

The shell has stopped moving; now the end-to-end suite catches up to it.

**Files:**
- Create: `ui/apps/demo/e2e/shell.ts`
- Modify: `ui/apps/demo/e2e/propositions.spec.ts`, `live-rules.spec.ts`, `dsl.spec.ts`

**Interfaces:**
- Consumes: everything.
- Produces: `openPalette(page, name)` and `chooseFromPalette(page, name, target)` in `e2e/shell.ts`.

- [ ] **Step 1: Write the e2e helper**

Create `ui/apps/demo/e2e/shell.ts`:

```ts
import { expect, type Page } from '@playwright/test';

/** Open the palette and wait for it to be ready to type into. */
export async function openPalette(page: Page, name: 'Propositions' | 'Rules'): Promise<void> {
  await page.getByRole('button', { name: 'Open' }).click();
  await expect(page.getByRole('dialog', { name })).toBeVisible();
}

/** Open the palette, filter to `target`, and choose it. */
export async function chooseFromPalette(
  page: Page, name: 'Propositions' | 'Rules', target: string,
): Promise<void> {
  await openPalette(page, name);
  await page.getByRole('combobox').fill(target);
  await page.getByRole('option', { name: new RegExp(target) }).first().click();
  await expect(page.getByRole('dialog', { name })).toBeHidden();
}
```

- [ ] **Step 2: Rewrite the specs against the new shell**

Every `page.getByRole('button', { name: /rule/i }).click()` followed by an `option` click becomes `chooseFromPalette(page, 'Rules', '<name>')`. Every explorer `treeitem` click becomes `chooseFromPalette(page, 'Propositions', '<name>')`. Every `getByRole('button', { name: /^save$/i })` becomes `getByRole('button', { name: 'Save' })`.

Preserve every existing assertion. The falsifiability of `propositions.spec.ts` test 1 rests on `customer.has-orders` and `customer.is-active` *disagreeing* about the fixture, and on `:175` asserting the rule version incremented exactly once — do not weaken either while porting.

- [ ] **Step 3: Add e2e coverage for what jsdom cannot test**

Append to `ui/apps/demo/e2e/propositions.spec.ts`. These three are the *only* home for these behaviours — the jsdom shim models none of them:

```ts
test('the palette traps focus, closes on Escape, and makes the page behind it inert', async ({ page }) => {
  await page.goto('/#/propositions');
  await openPalette(page, 'Propositions');

  // Focus starts inside and stays inside: tabbing from the last control wraps to the first
  // rather than escaping to the page behind.
  await expect(page.getByRole('combobox')).toBeFocused();
  await page.keyboard.press('Tab');
  const inside = await page.evaluate(() =>
    document.querySelector('dialog[open]')?.contains(document.activeElement) ?? false);
  expect(inside).toBe(true);

  // The page behind is inert: the toolbar button that opened this cannot be clicked through.
  await expect(page.getByRole('button', { name: 'Open' })).not.toBeVisible();

  await page.keyboard.press('Escape');
  await expect(page.getByRole('dialog', { name: 'Propositions' })).toBeHidden();
});

test('the palette fills the screen on a phone', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 780 });
  await page.goto('/#/propositions');
  await openPalette(page, 'Propositions');

  const box = await page.getByRole('dialog', { name: 'Propositions' }).boundingBox();
  expect(box?.width).toBe(390);
});

test('the document viewer opens from the toolbar on both pages', async ({ page }) => {
  await page.goto('/#/propositions');
  await page.getByRole('button', { name: 'JSON' }).click();
  await expect(page.getByRole('dialog', { name: /document/i })).toBeVisible();
  await page.keyboard.press('Escape');

  await page.goto('/#/rules');
  await page.getByRole('button', { name: 'JSON' }).click();
  await expect(page.getByRole('dialog', { name: /document/i })).toBeVisible();
});
```

- [ ] **Step 4: Run the e2e suite**

Probe for a free port first — a busy one fails loudly rather than borrowing another checkout's server, but only if you pick one that is actually free:

```bash
lsof -iTCP:5123 -sTCP:LISTEN -n -P
```
Expected: no output. Then:

```bash
cd ui && MOTIV_E2E_PORT=5123 pnpm --filter @motiv/rules-demo e2e
```
Expected: PASS, 23 tests (20 pre-existing + 3 new).

- [ ] **Step 5: Run everything**

```bash
cd ui && pnpm -r test && pnpm -r typecheck
```
```bash
git diff --stat -- '*package.json' '*pnpm-lock.yaml'
```
The last must be empty.

- [ ] **Step 6: Simplify (mandatory per CLAUDE.md)**

Spawn a `code-simplifier` agent over `ui/apps/demo/src/shell/`, `src/panes/` and `src/explorer/`, with these constraints stated:

```
Constraints that must survive any refactor:
- CommandPalette must stay generic over its item type and free of domain knowledge. The Rules
  page passes no footer; the Propositions page passes four actions.
- The palette's query state must stay INSIDE the palette. Lifting it to a page would make the
  palette remember a stale query across opens, which the "opens fresh" test exists to prevent.
- Unavailable actions use aria-disabled with a reason, never the `disabled` attribute. This is
  deliberate: `disabled` removes a button from the tab order, so the reason becomes unreachable.
- Do not add a dependency. `ui/` takes none.
```

Apply what it finds and re-run the affected tests.

- [ ] **Step 7: Commit**

```bash
git add -A ui/apps/demo
git commit -m "feat(demo): a real modal for the authoring dialog, and e2e for what jsdom cannot see"
```

---

## Self-review notes

- **Spec coverage.** Toolbar → Tasks 3, 7, 8. Palette → Tasks 4, 7, 8. JSON modal → Tasks 6, 7, 8. Nav glyphs → Task 5. Mobile fullscreen → Tasks 2, 4, 6, and asserted in Task 9. `aria-disabled` → Tasks 3, 7, 8, 9. `PropositionDialog` migration → Task 9. jsdom constraint → Task 1 (shim) and Task 9 (the e2e that covers what it cannot). Zero-dependency → global constraint, verified in Task 9.
- **Naming.** `unavailable` (not `disabled`) is used consistently across `ToolbarAction` and every call site. `onClose` is the dismissal callback on `Modal`, `CommandPalette`, `DocumentModal` and `ExplorerActions`.
- **Task 5 ordering.** `AppBar` is modified before the pages that use it, and its props are unchanged, so no page breaks in between. Every task leaves the suite green.
