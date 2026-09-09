# Spec 3E follow-up — The seam that was named after its consumer — Implementation Plan

**Design:** [`2026-09-09-spec-3e-followup-foldable-operation-rename-design.md`](../specs/2026-09-09-spec-3e-followup-foldable-operation-rename-design.md)
**Ticket:** [#211](https://github.com/karlssberg/Motiv/issues/211)
**Source:** bundle spec
[3 — Operability & Evidence](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/3-operability-and-evidence.md),
§2 Structural safety (19) — the machinery Spec 3E built for §6 step 1.

## What this is

`IOperationFold<TModel, TMetadata>` → `IFoldableOperation<TModel, TMetadata>`, and
`IAsyncOperationFold` → `IAsyncFoldableOperation`. Naming only. No behaviour, no public API, no test
changes.

Both names were locked by the ticket, including its rejection of `IOperationNode` — that name severs
the connection to the fold, which is the one thing the name should keep. Neither is reopened here.

## Global constraints

- **Nothing but the rename.** The value of this diff is that it reviews as noise. Anything that is not
  a token substitution has to earn its place individually and be visible as such.
- **`EvaluationFold` and `AsyncEvaluationFold` are not renamed.** Those *are* folds; their names are
  right, and are what makes the participant's old name wrong by contrast. Out of scope by the ticket.
- **The record docs are annotated, not rewritten.** A design doc records what shipped on its date. A
  find-and-replace through it would make it describe code that never existed under those names.

## Sequence

1. **Baseline.** `dotnet build` then `dotnet test` over the whole solution, *reading the exit code* —
   not the `Passed!` lines. Record every pre-existing failure before touching anything, so that any
   post-change red can be attributed.
2. **`git mv`** the two interface files, so the rename stays a rename in history rather than a
   delete-plus-add.
3. **Token substitution** across the 31 `.cs` files. `IAsyncOperationFold` does not contain
   `IOperationFold` as a substring, so the two substitutions are order-independent.
4. **The three doc summaries.** `IFoldableOperation`, `IAsyncFoldableOperation` and `EvaluationFold`
   each open with a sentence that existed to undo the old name. This is the point of the ticket, so
   the summaries are rewritten rather than left. Nothing else in any file changes.
5. **Prove the diff is behaviour-neutral by construction**, not by inference from a green suite:
   normalise both old and new names to one placeholder, diff, and confirm the only surviving lines are
   the three summaries.
6. **Annotate the record docs** — a note at the head of each doc that uses the old name, and a "landed
   since" note in each that forecast the rename as deferred work.
7. **Full solution suite**, compared against the baseline from step 1.
8. **The mandatory `code-simplifier` pass**, asked specifically whether the rename leaves a *residual*
   inconsistency in the files it already touches — `Frame.Node` is the candidate, and the ticket's
   rejection of `IOperationNode` is what makes it one. Ask it to say plainly if that is scope creep.

## Verification

The compiler is the test here, and it is a total one: both interfaces are `internal`, every
implementation is *explicit*, and an explicit implementation of an interface that no longer exists does
not compile. There is no runtime path a rename could break that the build would not have caught first.
So this slice writes no test and changes none — writing one would assert that the compiler resolved a
name, which it demonstrated by producing an assembly.

What is worth verifying is the claim that the diff is *only* a rename, and step 5 verifies exactly that.

## File structure

```
src/Motiv/Traversal/IOperationFold.cs      → IFoldableOperation.cs      (git mv + summary)
src/Motiv/Traversal/IAsyncOperationFold.cs → IAsyncFoldableOperation.cs (git mv + summary)
src/Motiv/Traversal/EvaluationFold.cs                                   (refs + summary)
src/Motiv/Traversal/AsyncEvaluationFold.cs                              (refs)
src/Motiv/{And,AndAlso,Not,Or,OrElse,XOr}/*.cs        (27 operators: refs only)
docs/superpowers/{plans,specs}/*3e*.md                (8 record docs: annotated, not rewritten)
```

## Out of scope

- **`EvaluationFold` / `AsyncEvaluationFold`.** Named correctly; see above.
- **`src/Motiv/Architecture.md`.** The ticket asks for it to be checked. It was: the file says nothing
  about the traversal subsystem at all — no `fold`, no `Traversal`, no `stack-safe`. Nothing to change,
  and the absence is worth recording so the next reader does not check it again.
- **[#219](https://github.com/karlssberg/Motiv/issues/219)**, an EF Core test flake the baseline run
  surfaced. Filed rather than fixed: different subsystem, and fixing it here would cost this diff the
  one property that makes it reviewable.
- **[#220](https://github.com/karlssberg/Motiv/issues/220)**, raised by the review: the repo never sets
  `GenerateDocumentationFile`, so a stale `<see cref>` fails no build. Closing that gate means first
  fixing pre-existing CS0419/CS1572 sites in unrelated files — a different change with a different
  blast radius.

## What changed against this plan

Step 8 returned six findings; three were acted on, three it argued against itself. The one that moved
scope was `Frame.Node` → `Frame.Operation`, accepted for the reasons in the design doc — briefly, that
`Node` reimports the exact noun the ticket rejected, in the same file whose summary was rewritten to
establish the replacement. It also caught a Markdown list this plan's sibling annotations had silently
broken, and one over-claim the *new* name introduced into the async summary.
