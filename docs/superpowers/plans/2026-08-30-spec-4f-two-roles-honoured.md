# Spec 4F — Two Roles Honoured — Implementation Plan

**Design:** [2026-08-30-spec-4f-two-roles-honoured-design.md](../specs/2026-08-30-spec-4f-two-roles-honoured-design.md)
**Ticket:** [#156](https://github.com/karlssberg/Motiv/issues/156), closing the two `Partially Supports`
rows Spec 4D's report recorded
**Source:** bundle spec [4 — Surface Quality](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/4-surface-quality.md),
§4, under ticket [18](https://github.com/karlssberg/Motiv/issues/118)

> Reconstructed from the shipped diff after the merge, per the docs backlog on
> [#169](https://github.com/karlssberg/Motiv/issues/169). Ticket #156 states both problems and lays out
> the options for each; it is not repeated here.

## Global constraints

- **TDD throughout**, and here the tests come in two kinds because the defects do: unit tests can say
  which row *would* be reached, but only a real browser can say that `Tab` reaches it. jsdom has no tab
  sequence of its own.
- **A check that cannot fail is not a check.** The keyboard suite is validated by restoring the old
  markup and confirming it goes red.
- **`axe` will stay green throughout, and that is the point.** Neither of these is a violation it can
  see; if the sweep had caught them they would not have survived 4D.
- **Existing role queries are an asset.** Honouring `tree` preserves every `getByRole('treeitem')` in
  the explorer tests and `e2e/shell.ts`; dropping to groups would have rewritten them. That is a real
  argument in the choice, not a rationalisation of it.
- **The manual pass stays outstanding**, and the report must keep saying so.

## File structure

```
ui/apps/studio/src/explorer/PropositionExplorer.tsx  (the tree pattern: roving tabindex, keys, type-ahead)
ui/apps/studio/test/explorer/PropositionExplorer.test.tsx   (+248 lines of coverage)
ui/apps/studio/src/panes/AppBar.tsx                  (tablist → nav of anchors, hrefs via formatHash)
ui/apps/studio/src/App.tsx ; panes/{RulesPage,PropositionsPage,AdminPage}.tsx
                                                     (onNavigate unthreaded from four components)
ui/apps/studio/src/panes/{RuleHeader}.tsx            (…and its callers)
ui/apps/studio/e2e-a11y/keyboard.spec.ts             (new — six checks, in a real browser)
ui/apps/studio/playwright.a11y.config.ts             (+ the new spec)
.github/workflows/ui.yml                             (the a11y job runs both specs)
ui/apps/studio/src/styles/app.css                    (.page-nav)
docs/accessibility/index.md ; docs/Overview.md ; ui/apps/studio/README.md
```

## Sequence

1. **Decide each surface separately**, against 4D's rule rather than for consistency between them. The
   palette is navigation *and* selection, so the role is right and the behaviour is missing. The page
   switcher controls no panel, so the role is wrong and no amount of behaviour would fix it. Opposite
   outcomes from one rule.
2. **The palette's tree.** Roving tabindex first — it is the change every other key depends on, and the
   one that makes `role="tree"` defensible at all. Then `ArrowUp`/`Down`, `ArrowRight`/`Left`,
   `Home`/`End`, `Enter`/`Space`, and accumulating type-ahead last, since it is the only one with state
   of its own.
3. **Keep focus as the single source of truth.** No `focusedIndex` in component state — the tabindex
   follows focus, so there is no second copy of "where am I" to disagree with the focus ring.
4. **Bare namespaces join the arrow-key order**, `aria-selected` stays off them. A tree navigates its
   structure; selectability is a separate claim.
5. **The page switcher.** `<nav>` of anchors, `aria-current="page"`, hrefs from `formatHash` — the same
   function the router parses back, so link and route cannot drift. Then unthread `onNavigate` from the
   four components that were only passing it along.
6. **The keyboard suite.** Six checks against the built bundle in a real browser, including one
   proposition chosen with **no pointer at any point**. Wire it into the a11y job beside the axe sweep.
7. **Break it to prove it.** Restore the per-row `tabindex`; four of the six must fail.
8. **Move the report's two rows to `Supports`**, delete the *Known gaps* section, and leave the manual
   pass stated as outstanding.

## The follow-up commit

9. **A comment corrected, not the code.** `includeCurrent`'s comment claimed a search's first character
   *must* move off the current row. The scan wraps either way, so a first character matching only the
   current row comes back round to it — indistinguishable from finding nothing, since both leave focus
   where it is. Wrapping is the behaviour worth having, so the description moved rather than the bounds.
   Recorded because the tempting fix is the opposite one.

## Not run

The manual screen-reader pass — [#172](https://github.com/karlssberg/Motiv/issues/172). The keyboard
suite closes the mechanically checkable part of what the two roles promised; announcement quality still
needs a person.
