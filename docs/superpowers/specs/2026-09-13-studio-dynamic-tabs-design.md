# Studio dynamic tabs: one shell, many open documents

**Date:** 2026-09-13
**Status:** Implemented
**Primary source:** the prototype in PR #233 (`ui/apps/studio/src/prototype/` at commit `72d47c66`),
three variants switchable via `?variant=`; verdict **C** with the refinements recorded below.

## Problem

Studio had two static pages, Rules and Propositions, each with one document open at a time and one
`RuleEditorStore` shared between them. Editing a proposition that a rule uses meant closing the
rule, switching page, editing, saving, switching back and reopening — and the shared store made
"switch page" and "lose the draft" the same act. A person working on a rule and the propositions it
composes needs both open at once, and needs the rule to know when a proposition it uses has moved.

## What the prototype settled

The question was *what should a multi-document shell look like, and how do tabs stay in sync?*
Three structurally different answers were built over real reads and stubbed saves:

- **A** — a browser-style elastic strip under the app bar;
- **B** — a vertical "open documents" rail grouped by kind;
- **C** — fixed-width chips *in* the app bar, spilling into a "+N" overflow menu.

**C won**, refined in review:

| Question | Decision |
|---|---|
| Where the tabs live | **In the app bar**, in a recessed well after the brand. No vertical space is spent on a second row. |
| Width pressure | **Chips never shrink below a readable width.** What does not fit collapses into a "+N" menu; the active tab is always kept visible (an overflowed tab that is activated swaps into the last slot). Under 640px the strip becomes **one dropdown** naming the active document with a count, listing every open tab. |
| Kind cue | An **outlined** lettered tile — `R` in the accent, `P` in the higher-order green — and a matching glyph in the hover card. Outlined, not filled, so the filled brand mark is the only solid tile in the bar and cannot be read as a tab. |
| Title | The last dotted segment, with the namespace muted before it; the namespace yields first when squeezed. |
| Hover / focus card | Full name, kind and origin, model pill, version, async / code-default badges, unsaved state and error count, the propositions a rule uses (marking which are open), the open rules a proposition is used by. Delayed 350ms; `role="tooltip"`, referenced by `aria-describedby`. |
| Close | The chip's **×** (also middle-click, Delete, ⌘W), through the same *Keep editing / Save & close / Discard* question the toolbar's Close asked. Neither Open nor Close has an app-bar button any more: opening is the strip's **+** and ⌘K. |
| Document actions | **JSON and Save sit at the end of the editor pane's header**, after the Builder / DSL tabs, so they read as the document's. When the strip is a dropdown they move into the app bar's free space; narrower still, the bar wraps and they take a second row. `EditorPane` gains an optional `actions` slot for this. |
| Sync between tabs | **One `RuleEditorStore` per open document**, and one workspace that knows every name's latest version. A rule tab shows the propositions it references with their version; **"editing"** while another tab has one dirty, **"updated"** once one is saved after the tab last looked, with a *Got it* that re-baselines. Saves reach the server exactly as before; the workspace only learns the new version. |
| Route | **The hash is the active document.** `#/rules/<name>` opens (or activates) that tab; closing the active tab navigates to its neighbour, or to the bare page. Deep links, the back button and a hand-edited URL all take the same path they did. The open-tab *list* is session state (`sessionStorage`), so a reload keeps the tabs and the hash keeps the one in front. |
| Admin | Still a route (`#/admin`) with its own page. The app bar offers it as a link at the far end once capabilities allow, as before. |
| Palettes | ⌘K and **+** open one **Open** palette listing rules and propositions together, each row saying which it is and whether it is already open. Its footer offers **Manage propositions**, which opens the existing Propositions explorer — the namespace tree with New / Derive / Override / Delete — unchanged. |

## Not in scope

- **Cross-browser-tab sync.** Two browser tabs are two workspaces. The server's generation token and
  the 409 conflict banner remain the guard, as before.
- **Reordering tabs by drag.** The workspace supports `move`; no gesture drives it yet.
- **Restoring drafts across reloads.** The tab list is remembered; unsaved edits are not.

## Architecture

```
App
├─ useHashRoute ─────────────── the active document (or admin)
├─ Workspace (src/shell/workspace.ts)   tabs, per-tab RuleEditorStore, latest versions, seenAt
├─ AppBar ── TabStrip (chips | dropdown) ── + ── Admin link
├─ WorkspaceShell
│   ├─ one <TabPanel hidden={!active}> per tab, kept mounted so drafts, undo, surface and
│   │   workflow state survive a switch:
│   │     RuleDocument   = useRuleWorkflow(client, tab.store) + EditorPane(actions) + rail
│   │     PropositionDocument = usePropositionWorkflow(client, tab.store) + EditorPane(actions) + rail
│   ├─ OpenPalette (⌘K / +) → PropositionExplorer (Manage propositions)
│   └─ empty state when no tab is active
└─ AdminPage (route)
```

The route is the one *writer* of which tab is active — a chip click, a palette choice and a close all
navigate, and the shell's route effect opens and activates the tab the hash names. One refinement
found in the suites: `openTab` opens the tab in the workspace *before* navigating. `hashchange` is
asynchronous, so a click that only navigated left the previous panel on screen for a frame; opening
first means the strip answers in the same tick, and the route effect then finds the tab already
open and does nothing.

`RulesPage` and `PropositionsPage` become `RuleDocument` and `PropositionDocument`: the same
workflow hooks, bound to the tab's store rather than the shared one, minus the app bar, the
palette and the route plumbing, which the shell now owns. `lastSelection` is retired: the tab list
does its job.

The propositions *listing* and its authoring actions (create, delete) are shell-level — one
`usePropositionWorkflow` bound to a scratch store that never loads a document — because they act on
the set, not on a tab. A document's save reports its new version to the workspace, which refreshes
the listings so every other tab's references are current.
