# Spec 4D — The Accessibility Slice — Design

**Date:** 2026-08-30 (the slice); this document written 2026-08-31
**Status:** Shipped
**Source:** Build step 4 of bundle spec [4 — Surface Quality](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/4-surface-quality.md)
§6, taking ticket [18](https://github.com/karlssberg/Motiv/issues/118). Waited on Spec 4C, which put
the app at its final path. Tracked as [#154](https://github.com/karlssberg/Motiv/issues/154); shipped as
[#155](https://github.com/karlssberg/Motiv/pull/155).

> **Written after the merge**, per the [#169](https://github.com/karlssberg/Motiv/issues/169) docs
> backlog. Recovered from the shipped diff and its two review rounds.
>
> **Ticket #154 carries the grounded inventory** — what was already right, what was missing, and what
> each piece becomes. It is not repeated here.
>
> **This describes the surface as of 4D.** Later slices moved it: 4F gave the command palette a real
> `tree` and 4H made the conformance report generated rather than hand-written. Where that matters it
> is said below.

## Summary

Ticket 18's answer to accessibility is not "add ARIA to a tree". It is that **Motiv already generates a
linear text of any composition** — `printInline` in the DSL, `Reason`/`Justification` in the engine —
and that this generated text is the authoritative accessible description of the structure.

That is the product's own thesis turned into an affordance. Motiv exists to linearise boolean structure
into readable text; a screen-reader user needs exactly that, because the indentation and connecting
lines a sighted reader takes the structure from convey nothing at all.

So: `accessibleExpression` in `rules-core`, nested labelled `group`s in the builder and in
`JustificationTree`, and `axe-core` in its own CI job. The sweep found six real violations on its first
run.

## Decisions (locked)

### 1. The generated text is the accessible name, and it lives in the package

`a11y.ts` derives a node's accessible name from its own printed DSL — the same string the strip above
the tree shows, and the same one the engine's `Reason` is built from. Logic in the package, rendering in
the app: ticket 07's boundary, unchanged.

It also means an adopter building their own UI gets **the text**, even though they get no markup. That
is the only accessibility the headless packages can honestly offer, and decision 9 says so out loud.

**Named by the expression and nothing else.** The `group` role supplies the noun when it is announced,
so no English glue has to be invented — and glue would have to differ per node kind anyway, since an
operator has operands where a quantifier has a body.

### 2. The builder is not a `tree`, and the reason is behavioural

`tree` is a **navigation and selection** pattern: roving `tabindex`, one focusable item, arrow-key
movement. The builder is an **editing** surface whose every row holds an editable field, a popover and a
toolbar. Declaring `tree` would promise a keyboard model the surface does not implement.

The structure is therefore **nested labelled `group`s + `disclosure`**, with the label on each group
being the generated text of the subtree it holds. The builder root is itself a group, described by the
DSL strip's one-liner — which also covers the leaf rule that has no subtree and so no group of its own.

> The rule this rests on — *a role is a promise about behaviour, and declaring one you do not implement
> is worse than declaring none* — is what let Spec 4F later give the command palette a **real** `tree`:
> it implements the roving `tabindex`, the arrow keys, `Home`/`End` and type-ahead that the role
> promises.

### 3. The name is capped at 120 code points, because a group's name is announced on entry

Without a cap, arriving at the builder reads out the entire composition before the user has moved
anywhere — a root group holds the whole rule. 120 is about a spoken sentence: long enough for the
compositions a person actually authors by hand, short enough that a generated thousand-operand document
does not have to be sat through.

**Cut by code point, not by `slice`**, so a truncation cannot land inside a surrogate pair and end the
name with half a character. The ellipsis is part of the name on purpose: a name that stops
mid-expression without saying so reads as a complete, and wrong, expression.

### 4. An `aria-controls` IDREF is dropped while its target is unmounted

The parent's caret names the group it discloses — **only while that group is mounted**. An IDREF to an
element not in the document is invalid, not harmless.

The command palette already followed this rule. Two places did not, and both are fixed here:
`ListboxPicker` carried `aria-controls` unconditionally while rendering its listbox only when open — and
that is every operator badge and rule-name picker in the app.

> This is now a standing convention, recorded in `CLAUDE.md`.

### 5. Row names stay path-based; the content belongs to the group

`expand $.rule.and[0]` identifies a row. The subtree's *expression* is carried by the group that row
controls. Restating it on the row as well would announce the same text once per level on the way down.

### 6. The axe sweep serves a static build and stubs the API — deliberately

The sweep does not drive the .NET host the way `e2e/` does. Two reasons, both about what an audit is
for:

- **It is a gate, so it must be deterministic.** Every finding has to be a fact about the markup, and a
  view whose contents depend on whatever a live store happens to hold is not that. Fixed data means a
  violation that appears is one somebody introduced.
- **It must run on every pull request.** The host needs the .NET SDK; a static build and a browser need
  neither, so the sweep joins the `ui` workflow that already runs on every push rather than waiting on a
  second toolchain.

The cost is stated rather than hidden: these are the **shapes** the endpoints return, not the endpoints
themselves, so a server that started answering differently would not be caught here. The fixture is
self-checking — a call with no stub **fails the test** rather than scanning an error page.

It scans **every view and every hard surface in the state it is hard in** — palette open, filtered, and
filtered to nothing; modal shown; picker triggered; menu and detail panel open — in both colour schemes.

> Spec 4J later added the one surface this sweep still could not reach: the failure banner, which only a
> broken server produces. Its first scan failed 1.4.3 — the same class of finding as decision 8's, for
> the same reason.

### 7. The CI job runs the accessibility spec, not the whole e2e suite

Adopting all 36 e2e tests as a merge gate is a real decision with its own cost, and no ticket asks for
it. Ticket 18 asks for axe in CI. **The job is named for what it gates.**

### 8. The manual pass is authored, not performed — and the report says so per criterion

The screen-reader half needs NVDA or VoiceOver and a person, which is the maintainer's resourcing call.
So the conformance report ships stating, criterion by criterion, which evidence is mechanical and which
is still owed. **A conformance report that claimed an audit nobody ran would be worse than no report.**

That owed half is [#172](https://github.com/karlssberg/Motiv/issues/172), still open and assigned to a
human by design.

> 4D's report was hand-written. Spec 4H made it **generated** from `a11y/conformance.ts`, with a test
> refusing a record that claims axe coverage for a criterion axe has no rule for — so `docs/accessibility/vpat.md`
> must never be edited by hand now.

### 9. The SDK carries no accessibility, stated honestly

Ticket 18 sub-5: the headless packages ship no components, so an adopter's own UI inherits no
accessibility from them. The one exception is `JustificationTree`, kept by
[#99](https://github.com/karlssberg/Motiv/issues/99) precisely as the render-prop component that owns
the semantics — which is why it gets decision 2's treatment identically, replacing a flat run of sibling
`treeitem`s that claimed a nesting the DOM did not have.

## What the first scan found

Six real violations, all fixed in the slice:

- **Five contrast failures**, rooted in `--faint` at **2.6:1 on white** and its three pill colours.
  `--faint` moved to `#646c76`, quoted against the *tightest* ground it is drawn on — the palette's
  highlighted row at 4.8:1, not the white panel at 5.3:1.
- **The DSL pane's CodeMirror textbox with no accessible name.**

Plus two defects of the same class as decision 4's, found by reading rather than by axe: `ListboxPicker`'s
unconditional IDREF, and the palette's *"N of M"* count as a silent `span` — filtering a list by typing
being the canonical live-region case.

## Two review rounds, five findings

**Round 1 — a hole in the tests, then a rewrite it licensed.**

- The test asserted `name.length` — **UTF-16 units** — against a limit stated in **code points**. It
  passed only because its fixture is ASCII, so it was not actually testing the bound the function
  promises, and there was no astral coverage at all: precisely the case the code-point handling exists
  for. Two cases now cover characters outside the BMP — one under the limit by code points but over by
  UTF-16 units, one over. **Both passed against the implementation as it stood**, so this closed a hole
  in the tests rather than fixing a defect in the code.
- `[...text]` materialised every code point of a whole printed subtree to keep the first hundred. Now
  bounded twice over: the UTF-16 length is an upper bound on the code-point count, so a string short
  enough by that cheap measure needs no inspection at all — nearly every expression — and the walk that
  follows stops at the limit, at most `limit + 1` iterations however long the rest is. The strengthened
  astral tests are what **license** the rewrite: they fail on any cut landing mid-character.

**Round 2 — a regression the rewrite introduced, and two empty names.**

- The walk stops on `kept === max`, and **an integer counter never equals a fractional limit and never
  equals `NaN` at all** — so `accessibleExpression(node, 10.5)` and `(node, NaN)` ran the loop to
  exhaustion and returned the *whole* string, withdrawing the one guarantee the function makes. The
  implementation it replaced got both right **by accident**, via `slice`'s own coercion. `limit` is
  exported API and a caller may well compute it (a pixel width over a character width is rarely an
  integer), so it is now normalised rather than trusted: `NaN` falls back to the standard bound, a
  fractional limit floors, a negative one means no room — rather than `slice`'s count-from-the-end
  reading, which returned nearly everything — and `Infinity` is preserved, because "no limit" is a
  meaningful thing to ask for.
- `JustificationTree` claimed accessible names it did not have. `assertions` is a `string[]` the contract
  allows to be empty, and `??` guards only null and undefined — so a node with no assertions, or a caller
  passing `label=""`, reached the DOM as `aria-label=""`. **That is worse than no label**: it claims a
  name where there is none, and assistive technologies disagree about what to do with it, so the same
  markup reads differently in different readers. The attribute is now omitted when there is no text, and
  a blank label counts as absent.

## What this does not do

- **It does not perform the manual audit** — decision 8; #172.
- **It does not gate the whole e2e suite** — decision 7.
- **It does not give the packages components.** Decision 9.
- **It does not fix [#150](https://github.com/karlssberg/Motiv/issues/150)**, still open at this point
  and later closed by Spec 4J.

## Verification obligations

- `axe-core` reports no WCAG 2.1 AA violation on any Studio view, in CI, in both colour schemes.
- A screen-reader user can read a rule's composition via its generated text — as the accessible name of
  every group in the builder, and as the description of the whole.
- `rules-core` still builds and tests with **no React present**: `a11y.ts` is string logic over the
  document model.
- A truncated name never cuts through a surrogate pair, at any limit — fractional, `NaN`, negative or
  `Infinity` included.
- No element carries an `aria-label` that is empty, and no `aria-controls` names an unmounted target.

## Outcome (recorded after the build)

Shipped as [#155](https://github.com/karlssberg/Motiv/pull/155): 32 files, **+1340 / −75**, in three
commits — the slice and the two rounds.

**1,034 UI tests green** at the end (`rules-core` 619, `rules-react` 28, `studio` 387), with nine new
ones from round 2 alone. **The axe sweep: 28/28 across both colour schemes.**

**Six real violations found by the first scan** — see above. None of them were predicted; all of them
were in code that had been read and reviewed.

**The most instructive finding is round 2's first**, because it is a defect a review round *introduced*:
the previous implementation had been correct for fractional and `NaN` limits **by accident**, through
`slice`'s coercion, and the rewrite that made the function faster silently withdrew that. It is the
argument for the round having happened at all.
