# Studio — browser-like tabs: a tab is a slot, not a document

**Date:** 2026-09-15 · **Decided by prototype:** branch `prototype/browser-like-tabs` (variant C of three)

## The problem

After #233 a Studio tab *was* a document: its id was `kind:name`, so a tab with nothing in it
was unrepresentable. The consequences showed at the edges. A fresh visit had no tab at all, only
an empty state with two buttons; the strip's "+" opened a palette rather than a tab; and there was
no way to change what a tab showed without closing it and opening another, so the strip's order
churned with every switch. Browsers settled these questions long ago, and the ask was to borrow
their answers: the page loads with one empty tab, "+" adds another, a tab's content can be
replaced in place, and a tab can be emptied without being closed.

## The decision

A tab is a **slot**. It can be empty, can take a document, and can be emptied again, keeping its
place in the strip each time.

- **Identity.** A tab keeps `TabId` as its key. A loaded tab's id is still `kind:name`; an empty
  tab's is `empty:<n>`, with no entry in `docs`. The id changes when the content changes — the
  strip, the roving focus and the tests already look a tab up by id and already tolerate a
  missing document, so this is the smallest change that gives an empty tab an identity. Position
  is what the user perceives, and position is preserved by in-place replacement.
- **Where a document lands.** `Workspace.open(kind, name, { into? })`: into the tab named, replacing
  what it held; else into the active tab while that tab is empty; else in a new tab at the end. A
  document already open is activated where it is — one editor per document stays the invariant.
- **The route.** Still the truth for which tab is active, with one more mapping: the bare page
  means *an empty tab is in front*. `activateEmpty()` finds the first empty tab or makes one, so a
  fresh visit, "+", unloading, and closing the last tab all land on the catalog by the same path.
  Empty tabs are not remembered across a reload; the bare route makes one.
- **What an empty tab shows.** The catalog (`CatalogPane`): every rule and proposition with model
  type, version and description, filterable, with New proposition and Manage. Choosing a row opens
  it in that tab. A browser's new-tab page, with the catalog where the bookmarks would be.
- **How a loaded tab changes.** A breadcrumb above the document, *Catalog › Rules › name*. The
  first segment unloads the tab back to the catalog; the kind segment drops the other documents of
  that kind to swap one in. Both go through the unsaved-changes question, whose save button now
  says what it is about to do: *Save & close* or *Save & unload*.

## The variants weighed

Three structurally different shells were driven on the live app, each mounting the real editors.
**A**, an omnibox under the bar and a new-tab page of tiles, put the change-content affordance in
a second bar row that cost height on every document. **B** made each chip its own chooser, which
kept the chrome minimal but hid the affordance behind a click on an already-active chip, and its
empty panel had nothing to say. **C** won: the catalog *is* the empty tab, so the page a fresh
visit lands on is the most useful page in the app, and the breadcrumb says where a tab is in the
words the route already uses.

## What did not change

The tab strip's overflow, compact dropdown, hover card and keyboard model; the per-tab store and
the reference sync between tabs; the palette and the explorer, still reached by ⌘K, the strip's
search button, and now the catalog's Manage.
