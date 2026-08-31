# Spec 4F — Two Roles Honoured — Design

**Date:** 2026-08-30 (the slice); this document written 2026-08-31
**Status:** Shipped
**Source:** Follow-up to Spec 4D, closing the two `Partially Supports` rows its Accessibility
Conformance Report recorded. Bundle spec [4 — Surface Quality](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/4-surface-quality.md)
§4, under ticket [18](https://github.com/karlssberg/Motiv/issues/118)'s enforcement clause. Tracked as
[#156](https://github.com/karlssberg/Motiv/issues/156); shipped as
[#159](https://github.com/karlssberg/Motiv/pull/159).

> **Written after the merge**, per the [#169](https://github.com/karlssberg/Motiv/issues/169) docs
> backlog. Recovered from the shipped diff and its one follow-up commit.
>
> **Ticket #156 states both problems and lays out the options**, including which way it expected each
> to go. It is not repeated here.

## Summary

Spec 4D's report recorded two rows as `Partially Supports`. **Neither was a missing semantic.** Each was
a **role that promised more than the markup delivered** — which is exactly what `axe-core` cannot see,
because an approximation is not a violation.

Both had been noted honestly in the source before the report existed. This slice closes them, and the
two go in **opposite directions**.

## Decisions (locked)

### 1. One rule, two opposite outcomes

Spec 4D established the rule: **a role is a promise about behaviour, and declaring one you do not
implement is worse than declaring none.** Applying it here does not mean removing both roles — it means
asking, per surface, whether the promise is the right one:

| Surface | Is the role right? | Outcome |
|---|---|---|
| The palette's namespace browser | **Yes** — this really is navigation *and* selection | Keep `tree`; **implement it** |
| The page switcher | **No** — page navigation controls no panel | Drop `tablist`; become what it is |

That the same rule produces opposite answers is the point. Ticket 18 used this argument to keep `tree`
*away* from the builder, whose rows hold editors; here it is used to keep `tree` and earn it.

### 2. The palette's tree implements the pattern, and focus is the only state

Every row was a tab stop — **precisely what a `tree` must not be**. A screen-reader user was told they
were in a tree, switched into the navigation mode a tree implies, and the arrow keys did nothing.

It now has a **roving tabindex** (one stop for the whole tree), `ArrowUp`/`Down` through the rendered
rows, `ArrowRight`/`Left` into a subtree and back to the parent, `Home`/`End`, accumulating type-ahead
on the segment names, and `Enter`/`Space` to choose.

**Focus is the state, and the tabindex follows it** — so there is no second copy of *where am I* that
could disagree with the focus ring. That is a deliberate rejection of the obvious alternative (a
`focusedIndex` in component state, with focus applied from it), which creates exactly that second copy.

**Bare namespaces join the arrow-key order** — a tree navigates its *structure*, not just its leaves —
while keeping `aria-selected` off, which is what says they are not selectable.

### 3. The page switcher becomes a `nav` of anchors, which is also a behaviour win

Page navigation **controls no panel**, so `role="tablist"` had no tabpanel to point at. Worse, the real
tabs were one file over: `EditorPane`'s Builder/DSL tabs *are* tabs and implement the full pattern. **Two
adjacent surfaces wearing the same role while only one means it is the part that misleads.**

It is now a `<nav>` of anchors carrying `aria-current="page"`, with hrefs minted by **`formatHash` — the
same function the router parses back**, so the link and the route it produces cannot drift.

Making it real anchors is not only an accessibility fix. Middle-click, open-in-new-tab and a visible
destination on hover all come for free, and the `onNavigate` callback that had been threaded through
four components **goes**.

### 4. The gate grows the half a scan cannot judge

`axe-core` passes over both of these — that is why they survived 4D. So the enforcement has to be a
different kind of check: `e2e-a11y/keyboard.spec.ts` drives the keyboard against the **built bundle in a
real browser** — one tab stop, the movement keys, type-ahead, a proposition chosen **with no pointer at
any point**, and the nav links' `aria-current` and `Enter`.

**jsdom has no tab sequence of its own**, so a unit test can say which row *would* be reached and only
this can say that `Tab` reaches it. That is the whole argument for a second Playwright spec rather than
more unit tests.

**Verified by breaking it**: restoring the old per-row `tabindex` fails four of its six checks.

### 5. The report moves, and what remains still says so

The two rows move to `Supports` and the **Known gaps** section goes. The manual screen-reader pass
remains outstanding and continues to say so — [#172](https://github.com/karlssberg/Motiv/issues/172).

> Spec 4H later made the report **generated** from `a11y/conformance.ts`, with a test refusing a record
> that rests a *Supports* on nothing but an owed manual pass. The keyboard suite this slice added is one
> of the evidence kinds that record can cite.

## The follow-up commit: the comment was wrong, not the code

The `includeCurrent` comment claimed the first character of a search *"must move off the current row"*.
The scan wraps the whole tree either way, so a first character whose only match is the current row comes
round to it — the same outcome as finding nothing, since **both leave the focus where it is**.

**The comment moved rather than the bounds.** Wrapping is the behaviour worth having: a search that
finds only where it started should stay there, not report nothing. What was wrong was the description.

This is worth recording because the tempting fix is the other one — change the code to match the comment,
and lose a good behaviour to a bad description of it.

## What this does not do

- **It does not perform the manual screen-reader pass** — #172, still open and assigned to a human.
- **It does not touch the builder's markup.** 4D's decision stands: the builder is an editing surface and
  is not a `tree`. This slice is the other half of the same rule, not a reversal of it.
- **It does not change `EditorPane`'s tabs**, which were always real tabs.

## Verification obligations

- The palette is **one tab stop**, and every movement key does what the `tree` role promises.
- A proposition can be chosen with **no pointer at any point**.
- The nav links carry `aria-current="page"` and activate on `Enter`.
- `pnpm -C ui/apps/studio a11y` stays green — it will not catch either of these, **which is the point**.
- The keyboard suite fails if the old per-row `tabindex` returns.

## Outcome (recorded after the build)

Shipped as [#159](https://github.com/karlssberg/Motiv/pull/159): 22 files, **+744 / −173**, in two
commits.

**Six keyboard checks**, four of which fail on the old markup — the check earns its CI minutes.

**A callback deleted, not added.** Replacing `role="tablist"` with anchors removed `onNavigate` from four
components: the honest markup was also the smaller one, which is the pleasant case and worth noting
because it is not the usual one.

**Neither defect was findable by the tooling that was supposed to find it.** Both were caught by a person
reading the markup and writing the gap down in a source comment, *before* the report that later graded
them existed. The mechanical half of 4D's enforcement is ~50% by design, and this slice is what the other
half looks like when it is done.
