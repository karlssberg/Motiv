# Spec 3E follow-up — The seam that was named after its consumer — Design

**Date:** 2026-09-09
**Ticket:** [#211](https://github.com/karlssberg/Motiv/issues/211)
**Plan:** [`2026-09-09-spec-3e-followup-foldable-operation-rename.md`](../plans/2026-09-09-spec-3e-followup-foldable-operation-rename.md)
**Source:** bundle spec
[3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md),
§2 Structural safety (19) — the seam Spec 3E introduced for §6 step 1.
**Lineage:** Spec 3E ([#144](https://github.com/karlssberg/Motiv/pull/144)) →
[#203](https://github.com/karlssberg/Motiv/pull/203) → [#206](https://github.com/karlssberg/Motiv/pull/206) →
[#210](https://github.com/karlssberg/Motiv/pull/210) → [#212](https://github.com/karlssberg/Motiv/pull/212) →
[#214](https://github.com/karlssberg/Motiv/pull/214) → [#215](https://github.com/karlssberg/Motiv/pull/215) →
[#216](https://github.com/karlssberg/Motiv/pull/216) → [#217](https://github.com/karlssberg/Motiv/pull/217) → here.

## The defect, which is in prose rather than in code

`IOperationFold<TModel, TMetadata>` is not a fold. It is the seam an operation exposes *so that*
`EvaluationFold` can fold it. The old type's own summary had to spend its first sentence saying so:

> A logical operation whose operands are evaluated by `EvaluationFold` **rather than by the operation
> itself**, so that a composition of any depth costs heap frames instead of stack frames.

A doc comment whose first clause exists to deny what the name implies is the name failing, not the doc
succeeding.

### Why it was worth the churn

Not because one type read backwards — because **"fold" carries three senses in this subsystem and only
the third is load-bearing**:

1. the functional operation — `Combine(first, second)` collapses children into a parent;
2. *to fold* — Spec 3E's transformation from non-tail recursion into a heap `Frame[]` loop;
3. **a fold** — one countable invocation of `EvaluationFold.Fold`.

Sense 3 is the unit the entire follow-up series turns on, because `EvaluationFold.Fold` descends only
into operands that are *themselves* operations. Everything else — decorators, higher-order
propositions, `ChangeModelTo`, leaves — is evaluated through the operand's own evaluation, which
**re-enters `Fold` with a fresh invocation**. A composition of alternating layers is therefore a stack
of nested folds, and five separate tickets state their result in that unit:

| Ticket | Stated in the "one fold" sense |
|---|---|
| [#202](https://github.com/karlssberg/Motiv/issues/202) | `MaxEvaluationSize` bounded one *fold*, not one evaluation |
| [#205](https://github.com/karlssberg/Motiv/issues/205) | `Matches`' allocation-free contract is per *fold* |
| [#204](https://github.com/karlssberg/Motiv/issues/204) | the async driver still bounds one *fold* |
| [#209](https://github.com/karlssberg/Motiv/issues/209) | a *nested fold*'s `Ownership` deliberately does not release |
| [#201](https://github.com/karlssberg/Motiv/issues/201) | the ceiling, measured in alternating layers = nested folds |

Five documents using "fold" to mean *an invocation*, against a type named `…Fold` that means *a
participant*. Each sentence costs the reader a disambiguation it should not have to make, and the cost
compounds precisely where the open work is densest.

### The name

`IFoldableOperation` / `IAsyncFoldableOperation`. It reads correctly at the call site, which is where a
type name is actually consumed:

```csharp
Fold<TModel, TMetadata, TValue, TDriver>(IFoldableOperation<TModel, TMetadata> root, TModel model)
```

The ticket considered and rejected `IOperationNode`: it severs the connection to the fold entirely,
which is the one thing the name has to keep. The type is not a node in a tree — it is a node *a
particular driver knows how to consume*, and its four members exist for no other reason.

`EvaluationFold` and `AsyncEvaluationFold` keep their names. They **are** folds, and their being
correctly named is exactly what made the participant's name wrong by contrast.

## What actually changed

31 C# files; 169 references across two interfaces; every implementation *explicit*, so not one member
name moved. Three XML summaries were rewritten, because they are what the ticket was about, and the
code review pulled in one further private symbol — `Frame.Node` → `Frame.Operation` — for the reason
recorded below.

| File | Before | After |
|---|---|---|
| `IFoldableOperation.cs` | "…whose operands are evaluated by `EvaluationFold` **rather than by the operation itself**" | "A logical operation `EvaluationFold` **can fold**: it hands the driver its operands and its composition rule instead of evaluating them itself" |
| `IAsyncFoldableOperation.cs` | "…a logical operation whose operands are evaluated by `AsyncEvaluationFold` rather than by the operation itself" | "…a logical operation `AsyncEvaluationFold` **can fold**" |
| `EvaluationFold.cs` | "a tree of `IOperationFold` **operations**" | "a tree of `IFoldableOperation`" |

The async summary was amended a second time during review; see finding 3.

The third is the rename paying for itself immediately: "a tree of `IFoldableOperation` operations" is a
stutter the old name did not produce.

## Verification: the compiler is the test, and the diff is the proof

**No test was written and none changed**, and that is the correct outcome rather than a lapse in TDD.
Both interfaces are `internal`, every implementation is explicit, and an explicit implementation of an
interface that no longer exists **does not compile**. There is no runtime path a rename could break
that the build does not catch first. A test asserting that the compiler resolved a name would assert
something the produced assembly already demonstrates. Under the TDD cycle this slice is step 5 —
refactor while green — applied to the whole 3E series at once.

What *is* worth verifying is the claim the diff makes about itself. A green suite only shows the tests
did not notice a change; it cannot show there was nothing to notice. So the claim was checked directly,
by rewriting both the old and the new name to one placeholder and diffing:

```
git diff --cached -- 'src/**/*.cs' | grep '^[-+]' | grep -v '^[-+][-+]' \
  | perl -pe 's/IAsyncFoldableOperation|IAsyncOperationFold/X/g;
              s/IFoldableOperation|IOperationFold/Y/g;
              s/\b(Operation|Node)\b/N/g; s/\b(operation|node)\b/n/g;' \
  | sed 's/^.//' | sort | uniq -u
```

Every line that differs *only* by a rename collapses to an identical pair and cancels under `uniq -u`.
**Not one code line survived** across all 31 files — only the doc-comment lines of the three summaries
above. That is a mechanical proof, and it is the check this kind of slice should ship with; the
technique generalises to any rename whose reviewability is its whole value.

Two notes on the technique, both learned here:

- **Use `perl`, not `sed`.** BSD `sed` does not support `\b`, so the word-boundary substitutions in a
  first attempt silently did nothing. It fails *safe* — an inert normalisation leaves extra lines
  standing, so a broken normaliser can only make the proof look worse, never better. That asymmetry is
  what makes the check trustworthy: you cannot accidentally talk yourself into a clean result.
- **Every renamed identifier needs a placeholder, in both spellings.** `Node`/`Operation` had to be
  added when the review pulled a third symbol in (below), and the property and its constructor
  parameter differ only in case, so both map.

## The record docs: annotated, not rewritten

Eight docs under `docs/superpowers/` name the old interfaces. The ticket asks for a one-line "formerly
`IOperationFold`" rather than a find-and-replace, following [#206](https://github.com/karlssberg/Motiv/pull/206)'s
"Naming, after the fact" treatment of its own `Scope/owned` → `Ownership/isRoot` rename. The reason is
that a design doc is a record of what shipped *on its date*; rewriting it would make it describe code
that never existed under those names.

Two shapes of annotation were needed, because the eight docs are not all the same kind of thing:

- **Five that use the name in prose** get a note at the head saying it has since changed. One of these,
  [`2026-09-03-…-decorator-ceiling-measurement-design.md`](2026-09-03-spec-3e-decorator-ceiling-measurement-design.md),
  contains a **captured stack trace** — ``Motiv.And.AndSpec`2.IOperationFold.Combine``. That is measured
  evidence from a run, and editing it would be falsifying the record. The head note covers it.
- **Four that *forecast* the rename as deferred work** get a "landed since" note, because their
  forward-looking tense went stale the moment this shipped.

Five plus four is nine over eight documents, because
[`2026-09-06-…-frame-buffer-depth-design.md`](2026-09-06-spec-3e-followup-frame-buffer-depth-design.md)
is both: it names the old file in a findings list *and* records why that slice declined the rename. It
gets both notes, which is the taxonomy working rather than failing — the two notes answer different
questions, and a doc can raise both.

The ticket names four docs. There are eight — the other four shipped after it was written. Any slice
annotating a series' record should enumerate rather than trust the ticket's list.

## The code review, and what it moved

The mandatory `code-simplifier` pass was asked three questions: are the new names and rewritten
summaries right, does the rename create a *residual* inconsistency inside the files already touched,
and is anything else worth simplifying. It was told explicitly not to widen the diff and to be blunt
about anything not worth doing. Six findings; three were acted on, and it argued against three of its
own.

It also **re-derived the normalising-diff proof independently** before ranking anything, which is the
right instinct: a claim that a diff is mechanical is exactly the kind of claim a reviewer should not
take on the author's word.

### 1. A "landed since" note silently broke a Markdown list

`2026-09-07-…-async-evaluation-budget-design.md` — the note was inserted at column 0 between the first
bullet of *What was deliberately not done* and the second. A column-0 blockquote **terminates** a list,
so the remaining three bullets rendered as a separate list, and a note that annotates one bullet read
as annotating the whole section.

A real defect, in prose this slice wrote, and invisible in the source diff — it only appears rendered.
Fixed by indenting the note two spaces so it nests inside the bullet it belongs to; the same was done
to the one in the matching plan. The other insertions were audited and sit at end-of-section or between
paragraphs, where column 0 is correct.

### 2. `Frame.Node` → `Frame.Operation` — the residual the rename created

Accepted, and it is the one place this slice went past the ticket's literal scope.

The ticket rejected `IOperationNode` *because the type is an operation, not a tree node*. `Frame.Node`
reimports precisely the rejected noun, in the same file whose summary was rewritten to establish the
replacement — a reader who accepts the argument at `EvaluationFold.cs:4` meets
`IFoldableOperation<TModel, TMetadata> Node { get; }` two hundred lines later. Worse, the driver
already *disagrees with itself*: `EvaluationFold.cs:142` binds the value as `operation` and passes it
three lines on into a parameter called `node`.

The decisive evidence is one file away. [`PostOrderFold.cs:97`](../../../src/Motiv/Traversal/PostOrderFold.cs)
has a `TNode Node` too, and there it is **correct** — that fold walks a generic result tree whose
elements really are nodes. The same word, in the same namespace, meaning two different things, and a
third sense again at `EvaluationFold.cs:181` (a rule-document node). Renaming only `EvaluationFold`'s
and `AsyncEvaluationFold`'s is what makes the namespace's vocabulary honest; `PostOrderFold` keeps
`Node` deliberately.

Everything involved is `private` — a nested struct and a nested interface in each driver. No `internal`
surface, no `InternalsVisibleTo` exposure, nothing a test can name, and the compiler checks all of it.

The scope-creep objection is real and worth stating: this is a third symbol against a ticket whose
contract is two interfaces. It was accepted because the alternative state — *rename the interface, but
leave the rejected noun sitting in the driver* — is worse than either endpoint, and because the ticket
had already licensed three non-mechanical edits in these same files for exactly the reason "the old
name forced this wording". `Node` is that same defect one level down.

### 3. The async summary asserted something the interface does not guarantee

The rewritten summary said "a logical operation `AsyncEvaluationFold` can fold." But
`AsyncEvaluationFold.cs:103` folds only `{ IsConcurrent: false }`, and `AsyncAndSpec`, `AsyncOrSpec`
and `AsyncXOrSpec` all implement `IsConcurrent => concurrent` — a *construction-time* flag. The same
type is foldable or not depending on how it was built, and a concurrent one is exactly the case the
driver hands back to evaluate itself.

The old wording was equally overstated, so this is not a regression — but **the old name made no claim
and `…FoldableOperation` does**. That is the finding worth keeping: a name that asserts a capability
inherits the burden of the assertion being true, so a rename can promote sloppy prose into a
name-level falsehood without a line of behaviour changing. The summary now carries the exception and
points at `IsConcurrent`, whose own remarks already had the fact.

### Three it argued against, and was right to

- **"the driver" in `IFoldableOperation`'s new summary** has no antecedent for a reader of that file,
  and `driver` means two things in `EvaluationFold` (the class, and the private `IFoldDriver`
  implementations). Flagged as adjacent to this ticket's own thesis about overloaded vocabulary — then
  dropped, because the pre-existing remarks already said "a driver" and fixing it means rewriting prose
  the ticket has no business touching.
- **The sync/async fold duplication.** `Frame`, `IFoldDriver`, `ResultDriver` and `MatchDriver` are
  structurally identical across the two drivers. Genuine duplication — and `CLAUDE.md`'s over-DRYing
  warning plus `AsyncEvaluationFold`'s own header explain why they cannot share: no `ref` local across
  an `await`, no thread-static buffer, a flowed budget.
- **Missing XML docs on the async `Frame`.** Pre-existing, and the file already says "same frame
  machine".

### An incidental finding worth its own ticket

The repo never sets `GenerateDocumentationFile`, so **a stale `<see cref>` does not fail any build
here** — the reviewer had to force the flag on to check that this slice's crefs still resolve. They do.
But a rename is precisely the change that breaks crefs, and the check that would have caught it is not
wired up. Filed as [#220](https://github.com/karlssberg/Motiv/issues/220).

That is the ledger's "a gate is only worth what it refuses" in its quietest form: not a gate reporting
the wrong thing, but a gate that was never switched on, in a repo whose XML docs are load-bearing
enough that this series keeps correcting them.

## The recommendation that was right and never once taken

#211 is emphatic that this should **not** ship standalone:

> Standalone it is a 30-file diff that reviews as pure noise and fixes nobody's bug. Recommended: fold
> this into whichever budget/traversal slice is next open — #205, #208, #204 or #201 all touch these
> files already, and the marginal cost of renaming while they are open is near zero.

All four shipped. None took it. Each declined in its own design doc, and each gave the same reason:

- [#212](https://github.com/karlssberg/Motiv/pull/212) (frame buffer): "this slice touches **one** [file]
  … folding in a 30-file mechanical rename would make a subtle hot-path change to thread-static
  lifetime review as a rounding error inside a diff that is mostly noise."
- [#215](https://github.com/karlssberg/Motiv/pull/215) (async budget): "Its own argument — that the churn
  is cheap while these files are open — is about the cost **to the author**, and the cost that matters
  here is **to the reviewer**."

That is the design finding of this slice, and it generalises past renaming:

**A "do it opportunistically" recommendation is a claim about whose budget the work lands on. #211
priced the churn in author-effort, where riding along is nearly free. Every candidate slice priced it
in reviewer-attention, where riding along is not free at all — it buys a mechanical change a discount
by spending the scrutiny budget of a semantic one.** Both prices are real; the reviewer's is the one
that decides whether a diff gets read. So the recommendation was individually rational to make and
individually rational to refuse, every time, and the queue drained with the work still undone.

Standalone was not a compromise on #211's advice. It was the only stable outcome, and the honest read
is that the advice was wrong on its own terms: a diff that "reviews as pure noise" is a *reason to
isolate it*, because noise is cheap to review alone and expensive to review next to something subtle.
The ticket had the right observation and drew the opposite conclusion from it.

The generalisable rule: **when a task's defining property is that it is mechanical, that is an argument
for its own PR, not against one.** A reviewer can verify a whole-tree rename with one normalising diff
(above) in less time than it takes to separate that rename from a semantic change sharing a hunk.

## Also checked, and clean

`src/Motiv/Architecture.md`, which the ticket asks about, says nothing about this subsystem — no
mention of `fold`, `Traversal` or stack safety anywhere in the file. Nothing to change. Recorded so the
next reader does not re-check it.

## Test results

Full solution, local, .NET SDK 10.0.302:

- **17 assembly/TFM combinations green, 0 failures, 21,645 assertions.** `Motiv.Tests` × 6,022 and
  `Motiv.Serialization.Tests` × 983, each on net8.0, net9.0 and net10.0; plus the Studio, EF Core, SQL,
  AspNetCore, Blazor, analyzer, CodeFix and all four example suites on net10.0.
- **The `net472` test targets could not run locally** — they need mono, which is not installed on this
  machine. **CI covers them fully**, and by more than the compile this series has previously claimed:
  the Windows `build` job *runs* them, and did on this PR — `Motiv.Tests` × 6,012 and
  `Motiv.Serialization.Tests` × 996 on `.NETFramework,Version=v4.7.2`, both green.

  That correction is worth keeping. Spec 3E's earlier docs say the net472 leg "depends on" a clean
  `dotnet build`, which is true but understates the gate: a local mono gap leaves the TFM *tested*, not
  merely *compiled*. A claim about what a check covers should be read off the check, and this one was
  read off a prior doc until [#221](https://github.com/karlssberg/Motiv/pull/221)'s run was inspected.

**The baseline run was not clean, and finding that out mattered.** With `-v q`, twelve `Passed!` lines
scrolled past while the run's exit code was `1`. Behind them: the two expected mono aborts, and one
genuinely red test —
`EfPropositionStoreTests.Should_replace_a_proposition_saved_under_the_same_name`. It did not reproduce
(green alone, green three times as a project, green in the post-change run), so it is a flake under
load and unrelated to a rename in `Motiv.Traversal`. Filed as
[#219](https://github.com/karlssberg/Motiv/issues/219) with a hypothesis — `SqliteStoreFixture`'s
teardown calls the process-global `SqliteConnection.ClearAllPools()` while sibling fixtures are still
running in parallel — labelled as a hypothesis, because it was not reproduced and a fix aimed at the
wrong mechanism would look like it worked.

This is [#173](https://github.com/karlssberg/Motiv/issues/173)'s lesson in a second guise, and worth
stating as a rule: **summarising a `dotnet test` run by its `Passed!` lines cannot distinguish a clean
run from an aborted one.** Read the exit code, then explain it.
