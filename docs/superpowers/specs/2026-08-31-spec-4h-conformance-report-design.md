# Spec 4H — The Conformance Report — Design

**Date:** 2026-08-31 (the slice); this document written 2026-08-31
**Status:** Shipped
**Source:** bundle spec [4 — Surface Quality](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/4-surface-quality.md),
§4 — the VPAT / Accessibility Conformance Report named as an explicit output of ticket
[18](https://github.com/karlssberg/Motiv/issues/118). Third in the accessibility run: Spec 4D built
the mechanical half, Spec 4F closed the two roles it found, this one makes the *document* answerable.
Tracked as [#163](https://github.com/karlssberg/Motiv/issues/163); shipped as
[#164](https://github.com/karlssberg/Motiv/pull/164).

> **Written after the merge**, per the [#169](https://github.com/karlssberg/Motiv/issues/169) docs
> backlog. Recovered from the shipped diff, its two follow-up commits, and the PR's review round.
>
> **Ticket #163 states both wrong claims with their reproductions**, prescribes the record-and-gate
> shape, and predicts the `document-title` fallout. It is not repeated here.

## Summary

The defect this slice fixes is a **category** of defect the previous two accessibility slices could
not reach.

4D's sweep and 4F's keyboard suite check the *application*. The conformance report is a claim about
**the suite itself** — and a suite passing tells you nothing about whether a sentence describing that
suite is true. So the fourteen-row hand-written table could be, and was, wrong in both directions at
once, with every check in the repository green:

| | published as | actually |
|---|---|---|
| 1.4.11 Non-text Contrast | *Supports — Enforced by axe* | axe has **no rule** for it at any version |
| 1.4.4 Resize Text | *Owed — not covered by the mechanical suite* | `meta-viewport` is tagged `wcag144` and had been running all along |

Both are the same defect wearing opposite signs: **a claim about a mechanical suite, maintained by
hand, beside the suite rather than against it.** The fix is not to correct two rows — it is to make
the claim a join that is computed, and to check it.

## Decisions (locked)

### 1. A row claims a *kind* of evidence and never names an axe rule

This is the hinge of the whole design, and it is what makes the two defects unrepeatable rather than
merely repaired.

A row says `{ kind: 'axe' }`. **Which rules that resolves to is read from axe's own tags** — at check
time in the gate, and again at render time in the appendix. So:

- a claim cannot outlive the rule it rests on;
- an `axe-core` upgrade that drops a rule **changes the published report**, rather than quietly
  falsifying a sentence nobody re-reads;
- and there is no hand-maintained list anywhere that could drift, because the only list is axe's.

`axeTagFor('1.4.11') === 'wcag1411'` is the entire join. It is one line, and it is the design.

### 2. Fifty rows, because omission is indistinguishable from a pass

The old table answered for fourteen and dismissed thirty-six in one sentence (*"either not applicable
… or inherited from the platform"*). The record answers for **all fifty**, and the gate refuses
anything else — not a subset, not a superset, and in WCAG's order.

The reasoning is about the reader, not tidiness: **a criterion nobody listed is a criterion nobody
decided about**, and in a document handed to a buyer that is indistinguishable from a pass. The
sentence that dismissed thirty-six was itself two wrong claims wide — 1.3.2 and 4.1.3 among them are
neither inapplicable nor platform-inherited.

### 3. `Not Evaluated` is added to ITI's vocabulary, on purpose

The VPAT vocabulary has four terms. This report has five.

The manual screen-reader pass is scripted (4D) and outstanding (#172, assigned to a human by design).
For a criterion only a person can judge, the four available terms offer only two lies: *Supports* on
no evidence, or *Does Not Support* for a failure nobody observed. **The first is the one a buyer is
harmed by**, so a fifth term was added and its non-standardness stated in the document itself.

Fifteen of the fifty rows carry it. That is not an embarrassment to be minimised — it is the report
being honest about how much of WCAG a machine cannot settle.

### 4. The fail-closed hinge, checked in both directions

> *a row whose only evidence is an owed manual pass cannot claim support, and a row claiming nothing
> was evaluated has to name the pass that would evaluate it*

Both halves are asserted. The first stops the record drifting back into the failure it was built to
end. The second stops `Not Evaluated` becoming a shrug — every one of the fifteen names what the pass
must establish, which is also what generates the report's **worklist** section.

### 5. The structural arguments are *published*, not kept in source

A `reasoned` or `not-applicable` verdict renders its `because` into the remarks column.

"Supports, structural" with the reason left behind in a TypeScript file **is the same unsupported
assertion the record exists to prevent, merely relocated**. A reader of the document has to be able to
weigh the argument, not take delivery of a verdict.

### 6. The generator *is* the gate

`a11y:report` is `vitest run test/a11y/conformance.test.ts -u` — the drift test in snapshot-update
mode, writing `docs/accessibility/vpat.md` through `toMatchFileSnapshot`.

There is no separate generator script, deliberately. A generator and a checker that are different
programs can disagree; making regeneration the *same run* as the check means the published document
and the record cannot diverge in either direction — an unpublished record edit fails, and so does a
document edit the record does not support.

### 7. The keyboard titles are read from source, not imported

`keyboard.spec.ts` is a Playwright suite; importing it into vitest would run Playwright's test
registration inside the wrong runner. So the gate reads the file and regexes out the `test('…')`
titles — the same strings a reader of the report goes looking for.

Two things about that were decided rather than stumbled into:

- **A guard on the guard.** A title regex that matched nothing would make the citation check vacuous
  and silently green, so the gate also asserts `keyboardTestTitles.length > 0`.
- **The path is composed, not a `new URL(…, import.meta.url)` literal.** Vite rewrites that exact
  pattern into an asset URL, and the path then arrives as something `readFileSync` cannot open. The
  comment in the source says so, because the next person to "simplify" it will otherwise rediscover it.

### 8. One definition of the AA floor

`axe.spec.ts` declared `WCAG_AA` locally; the record needed the same four tags. Two copies would
drift, and **the drift is silent in exactly the direction that matters**: a report claiming coverage
from a tag the sweep no longer runs. The constant moved to `criteria.ts` and the sweep imports it.

`best-practice` stays excluded, as in 4D: it is not AA.

## The defect the enumeration found

Answering for **2.4.2 Page Titled** — a criterion the old table never listed — surfaced a real product
bug that the sweep had passed on every run since 4D.

axe's `document-title` rule asks only that a title **exist**. Studio is a hash-routed single-page
application: one document, one `<title>`, and all three routes shared the static string from
`index.html`. Green scan, and both things a title is actually *for* were lost — what a screen reader
announces on navigation, and how a user tells four Studio entries in their history apart.

`useDocumentTitle` writes it from the route, **selection first**: `"{name} — {page} — Motiv Studio"`.
The ordering is a decision, not a default — every surface that shows a title truncates from the
*right*, so the half worth keeping is which proposition is open, not which application it is open in.

This is the slice's best argument for itself. The enumeration was justified as a documentation
property; it paid off as a bug-finding one.

## The review rounds

### CodeQL: the escaping order, and a genuine bug

Escaping `|` as `\|` without first escaping `\` is incomplete. A remark ending in a backslash
immediately before a pipe renders as `\\|` — which markdown reads as an escaped backslash followed by
a **bare** pipe. The cell ends early and every column after it shifts, **in a document whose entire
purpose is to be read as a table of claims, and whose per-row correctness nothing else checks.**

Verified before fixing (`'a backslash \\| and more'` came out as `'a backslash \\\\| and more'`), then
fixed by escaping the backslash first. Two further choices came with it:

- the escaping became a named `escapeCell` **applied to every cell both table renderers emit**, not
  to the remarks column alone — the safety is a property of the renderer, not of one column;
- four tests cover the order, a lone backslash, a lone pipe, a newline, and text that must survive
  untouched.

**The published report is byte-identical.** No remark in the record contains a backslash or a pipe
today — which is precisely why nothing had noticed, and why the fix was worth making before one did.

### Copilot: a document that contradicted its own gate

The audit section told an auditor to *"record findings there"*, pointing at `vpat.md` — which is
generated, carries a do-not-edit banner, and is guarded by a drift gate that would reject exactly that
edit on the next run.

The finding was right, and the reason it matters is more than a broken instruction: **a page arguing
that claims must be checked against their source cannot then send an auditor to write findings into
the output.** Findings now go to the row in `conformance.ts` — replace the `manual` evidence with what
the pass established, move the verdict off `Not Evaluated`, put the date and audited commit in the
remark — and the report and worklist both follow from that one edit.

The rewrite also states the consequence the old instruction would have walked into: **a verdict of
*Does Not Support* deliberately fails** the check guarding the "nothing is failing" sentence on the
prose page, because the audit has contradicted a claim that page makes and the page has to be
rewritten in the same commit.

## What this does not do

- **It does not perform the manual pass.** Fifteen rows stay `Not Evaluated`, and #172 stays assigned
  to a person with a screen reader.
- **It does not check non-text contrast.** 1.4.11 went from a false *Supports* to an honest
  *Not Evaluated*, and is now the sharpest item the manual pass owes. The slice makes the gap visible;
  it does not close it.
- **It does not evaluate AAA.** The floor ticket 18 set is AA, and `criteria.ts` says so.
- **It does not answer for the `@motiv-rules/*` packages.** They are headless and ship no components;
  the report's scope line says so and points at the adoption page for what that costs an adopter.
- **It does not verify that axe's tags are correct.** The record trusts axe's own account of what it
  covers. That is a smaller trust than trusting a human's memory of it, which is what it replaces.

## Verification obligations

The gate is seven refusals, each verified by breaking it:

| the gate refuses | which is |
|---|---|
| an `axe` claim on a criterion axe has no rule for | the 1.4.11 defect |
| a missing `axe` claim where the sweep does cover an applicable criterion | the 1.4.4 defect |
| a cited keyboard test `keyboard.spec.ts` does not declare | |
| a *Supports* resting on nothing but an owed manual pass, or a *Not Evaluated* naming no pass | the fail-closed hinge |
| a structural or not-applicable judgement with no reason | |
| a record answering for anything but exactly the fifty criteria, in order | |
| a published document that has drifted from the record | |

Plus a standing check that no row is `Does Not Support` or `Partially Supports`, which is how a gate
holds a **prose sentence** on a page it cannot read: the day a row becomes a failure, that page needs
rewriting, and this is what says so. (It passes because 4F closed the two `Partially Supports` rows
this report's predecessor recorded.)

Two assertions pin the join to reality rather than to itself:
`enabledAxeRules('1.4.3') === ['color-contrast']` and `enabledAxeRules('1.4.11') === []`.

## Outcome (recorded after the build)

Fifteen files, +1,425/−40. No C#.

- **1,139 UI tests green** — rules-core 684, rules-react 28, studio 427 — with typecheck clean and
  the 34-test accessibility sweep passing unchanged against the now-shared tag set.
- The report answers for fifty criteria: **27 Supports, 8 Not Applicable, 15 Not Evaluated**, zero
  failing.
- One real product defect found and fixed, which no scan could have surfaced.
- `CLAUDE.md` gained the standing rule: `docs/accessibility/vpat.md` is generated and never edited by
  hand; a record row claims a *kind* of evidence and never an axe rule; and adding a criterion,
  changing a verdict or upgrading `axe-core` all mean regenerating the report **in the same commit**.

### Where later slices moved this

- **Neither 4I nor 4J touched the record, `report.ts`, the gate or `vpat.md`.** Both edited only the
  prose page — 4I adding the Vue adapter's measured price, 4J adding the API-failure view to the
  sweep's list. That is the arrangement working as intended: a criterion already carrying a
  mechanical claim absorbs a new surface without a new row, because the claim resolves to axe's rules
  rather than to an inventory of views.
- **4J's addition earned its keep the way 2.4.2 did here.** Breaking a server to reach the failure
  banner was a *new view*, not a new criterion — and the first scan of it found a real contrast
  failure no route-only sweep would ever have reached. Same lesson, one slice later: the enumeration
  is where the defects are.
- The outstanding manual pass is **Spec 4L / #172**, still open and still assigned to a human. Until
  it runs, fifteen rows of this report stay `Not Evaluated` by design.
