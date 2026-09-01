# Spec 4K — The replacement hosted example for `src/examples/` — Design

**Date:** 2026-09-01
**Status:** Shipped
**Source:** bundle spec [4 — Surface Quality](https://github.com/karlssberg/Motiv/blob/wayfinder/enterprise-grade-product/.scratch/enterprise-grade-product/specs/4-surface-quality.md),
§2 (ticket [#108](https://github.com/karlssberg/Motiv/issues/108)), §3's .NET row, and §7's fourth
verification obligation. Tracked as [#171](https://github.com/karlssberg/Motiv/issues/171) under the
build map [#169](https://github.com/karlssberg/Motiv/issues/169).

> Written in the same commit as the implementation, per the `CLAUDE.md` rule the #169 retrofit
> backlog produced. The plan is
> [`2026-09-01-spec-4k-hosted-blazor-example.md`](../plans/2026-09-01-spec-4k-hosted-blazor-example.md);
> its inventory, TDD order and expected fallout are not repeated here.

## Summary

Ticket #108 accepted a debt in advance — rehoming the demo as `Motiv.Studio` would leave
`src/examples/` with no hosted rules-engine example — and §6 owed the replacement. The obvious
reading of that debt is "put a web app back". The slice declines that reading, and the reframing is
the design.

Spec 4E published a support-tier table. Its .NET row claims a runtime nobody in this repository was
exercising:

| **.NET, including Blazor** | Enabled | `Motiv.Serialization` | The C# parser, validator, binder and evaluator — no JavaScript involved |

4I established what this repository now does with a row like that. It did not ship a Vue adapter to
prove Motiv *can* do Vue; it shipped one because the tier table quoted a price nobody had paid, and
an artefact CI keeps green is worth more than an estimate. The .NET row had the same defect and a
worse one: it was the tier an enterprise buyer is *most* likely to take, and the only one with no
worked artefact at all.

So the replacement example is not "a web app, because we lost one". It is **the .NET tier's invoice,
paid**, which happens to be hosted.

## Decisions (locked)

### 1. Blazor WebAssembly, not Blazor Server

The spec says "Blazor WASM consumer" and "Blazor WebAssembly included". A server-rendered app would
have satisfied the letter of "hosted example" while proving the weaker claim — C# running on a
server was never in doubt. WASM puts the parser, the validator, the binder, the evaluator and both
explanations in the browser, which is precisely what the tier table asserts and what a JavaScript
adapter would otherwise be needed for.

This had to be checked before it could be chosen, because it is the one decision that could turn
every CI run red rather than fail in isolation: CI runs `dotnet test` over the whole `Motiv.slnx` on
`windows-latest` with the 8/9/10 SDKs and **no workloads installed**. Verified locally on a bare
10.0.302 SDK with an empty `dotnet workload list` — `Microsoft.NET.Sdk.BlazorWebAssembly` restores
and builds without `wasm-tools`, which is needed only for AOT and native relinking. Neither a debug
build nor a default publish does either.

### 2. The sample lives inside the documented boundary, not around it

`RuleDocument` and `RuleNode` are `internal sealed`, and `Motiv.Serialization`'s `InternalsVisibleTo`
names only its own tests and `Motiv.Serialization.AspNetCore`. The adoption page already stated the
consequence — a .NET authoring UI builds the JSON itself and leans on `Validate` — and the temptation
was to make the sample comfortable by widening that surface.

Widening it would have destroyed the artefact's only value. A sample that authors through a document
model no adopter can reach demonstrates a tier nobody is sold. So the sample brings its own
`DraftNode` tree and its own `RuleDocumentWriter`, and that cost — about a hundred lines — is now
measurable rather than described.

**Out of scope on purpose:** whether `Motiv.Serialization` *should* expose a document model. That is
a spec decision and a different slice. This sample is evidence about the boundary as it stands.

### 3. Paths are recorded by the walk that emits the JSON

This is the decision worth the most, and it is the part the adoption page's sentence understated.

"Leans on `Validate` to tell it what is wrong and where" sounds like a convenience. It is not: the
"where" is a JSON path (`$.rule.andAlso[0].spec`) produced by a parser reading bytes the sample
emitted, and an authoring UI has to turn it back into *the control the author is looking at*. A
second walk that re-derived paths from the draft would be the obvious implementation and would be
wrong in a specific, silent way — any disagreement between the two derivations puts an error beside
the wrong node, with nothing failing.

So `RuleDocumentWriter.Write` returns the JSON **and** the path→node map it built while writing.
One traversal, one source of truth, and the disagreement is unrepresentable rather than merely
untested.

### 4. Error resolution walks up, because a path can name a property

`$.rule.andAlso[0].spec` names a node's *property*. The nearest enclosing node is what the author is
editing, so `AuthoringSession` trims trailing segments until a path matches.

This is the one piece of logic that was written before its test — it went in with the session, and
the test added afterwards passed immediately, which proves nothing. It was verified the only way
that claim can be: the fallback was **removed**, the suite re-run, and exactly one test went red —
`Locates_an_error_reported_against_a_node_property_on_that_node`. Then it was restored. A test that
has never been observed to fail is a coverage number, not a guarantee.

### 5. The editor cannot author a shape no valid document exists for

`ChangeKindTo` tops operands up to `DraftNodeKinds.MinimumOperands` — one for `not`, two for
everything else, which comes from the schema's `nodeArray` (`minItems: 2`) — and `RemoveOperand`
refuses to go below it. Turning a proposition into an `and` and leaving it empty would put the author
in a state no amount of further editing rescues.

The single-operand case is still reachable through the API and still tested, because the sample's
job is to surface `Validate`'s refusal, not to assume the UI is the only caller.

### 6. Two gates, each refusing one specific regression

4I's clearest lesson generalises: *a gate is only worth what it refuses*, and a check reporting a
property it is not actually checking is worse than no check. Both gates were therefore verified by
breaking what they guard.

- **`References_nothing_beyond_Motiv_Serialization_and_the_Blazor_host`** reads the `.csproj` and
  asserts **set equality**, not `ShouldBeSubsetOf`. A subset assertion also passes when the XML read
  returns nothing, which is exactly the vacuous green 4I warned about.
- **`Ships_no_JavaScript_of_its_own`** refuses any `.js` under `wwwroot`. "No `rules-core`" is the
  §7 clause with the weakest natural enforcement — a C# project cannot accidentally `npm install`,
  but a sample can quietly acquire a script tag, and the artefact would then stop demonstrating its
  own claim with every job green.

`SampleProjectFile` throws rather than returning null when it cannot locate the sample, for the same
reason: a gate that cannot reach its target must fail, not pass.

**The first mutation attempt failed to test the gate, and that is worth recording.** Adding a
`ProjectReference` to `Motiv.Serialization.AspNetCore` was refused by the *compiler* first —
`NETSDK1082: no runtime pack for Microsoft.AspNetCore.App … for the specified RuntimeIdentifier
'browser-wasm'` — so the build died before any test ran. The gate was only actually exercised by
adding a reference that builds (`Motiv.csproj`), at which point both gates went red as intended. A
mutation that fails earlier than the check under test proves nothing about that check.

## What building it turned up

### The named document collapses `Reason` to its own name

The first evaluation assertion was written expecting
`"(is active == true) && (is adult == true)"`, and it failed with `"customer.can-checkout == true"`.

That is not a defect; it is `CLAUDE.md`'s suffix rule working. The document carries a top-level
`name`, a supplied name always outranks the composition as the source of explanation text, and so
the one-line summary is the name and nothing else. The operands that caused the outcome survive only
in `Justification`.

The consequence is a real hazard for the tier this sample represents, and no page had said it: **a
.NET authoring UI that rendered `Reason` alone would show its author a verdict with no reasons in
it** — boolean blindness, reintroduced by the explanation API. The sample therefore renders both and
explains why on the page itself, and the behaviour is pinned by two tests
(`Summarises_a_satisfied_composition_by_the_documents_own_name` and
`Keeps_the_contributing_operands_in_the_justification`) rather than left as prose.

Verified live in the browser, not only in tests: switching the sample customer to the 17-year-old
gives `customer.can-checkout == false` with a justification de-noised to the single causal operand,
`is adult == false`.

### The document is indented, and the tests say so

The JSON is written with `Indented = true`. The first implementation emitted compact JSON, which is
the right *wire* form and the wrong form for a panel a human is meant to read — the screenshot made
that obvious in a way the tests could not. Validation is indifferent to the whitespace, and the
tests assert the indented form, so the choice is pinned rather than incidental.

### The dev server's port is pinned by `launchSettings.json`, not by the launch config

The first run bound `http://localhost:5000` regardless of the configured port, because a Blazor WASM
project's dev server takes its URL from `Properties/launchSettings.json`. Worth recording only
because the failure looks like a tooling problem and is a missing file.

## The review round

One `code-simplifier` pass, ten findings. Two were worth more than polish, and both are worth
recording because neither is visible in the final diff.

### The gate did not check what its own comment said it checked

The strongest finding is the 4I lesson recurring **inside the slice that cites it**. The JavaScript
gate's remark said the risk was that the sample "can quietly acquire a script tag"; the assertion
enumerated `*.js` files under `wwwroot`. Two things escape that:

- a **collocated** `Pages/Author.razor.js` — Blazor's own JS-isolation shape — never appears in the
  source `wwwroot` at all, and is copied to the output at build;
- a `<script src="https://…">` in `index.html` adds no `.js` file to the tree whatsoever.

This was confirmed the same way the other gates were, by planting both and watching the suite stay
green at 19/19. The gate now scans the whole project directory (excluding `bin`/`obj`) and a second
test asserts that the only `<script src>` in `index.html` is `_framework/blazor.webassembly.js`.
Both planted escapes then failed, and were removed.

The lesson is narrower and more useful than "write better gates": **the doc comment was right and
the assertion was wrong, and prose is not executable.** A gate whose comment describes a stronger
property than its code checks reads as covered in review — the comment is what a reviewer believes.

### A suggestion was wrong, and the tests said so

The review proposed dropping the `Children.Count > minimum` guard in `ChangeKindTo` as redundant,
since `RemoveRange` with a count of zero is legal. It is not redundant, and
`Seeds_one_operand_when_a_spec_becomes_a_negation` went red immediately: a spec node has **zero**
children and `not` has a minimum of one, so `Children.Count - minimum` is `-1` and `RemoveRange`
throws. The guard was protecting the *under-full* direction, not the equal one.

The kind half of the condition was still worth changing — `Spec || IsUnary` was a coincidence, and
`IsFixedArity` names the actual rule — so the finding was half right, which is the useful half of the
story. The comment now says why both halves are load-bearing.

### Applied without incident

The remove button was offered enabled on operands the model refuses to drop, so it did nothing at
the minimum (`RemoveOperand`'s `bool` return had no production consumer, only a test) — the parent
now passes `Removable` down, mirroring the convention `add operand` already followed. `Operators`
stopped resting on `Dictionary.Keys` enumeration order and is listed explicitly, pinned by a new
`DraftNodeKindsTests` that checks no kind can go missing from the editor's dropdown. `SpecName` lost
a nullable annotation nothing honoured, `RuleDocumentWriter` swapped an undisposed `MemoryStream`
and its double copy for `ArrayBufferWriter<byte>`, `AuthoringOutcome` gained `Invalid`/`Evaluated`
factories in place of a bare `null, null, null`, and the registry is built once.

### Declined, with reasons

- **Collapsing three identical arrange blocks in `AuthoringSessionTests`.** Three tests asserting
  different facets of one scenario is right; under this repository's explicit "avoid over-DRYing"
  value, a visible arrange block earns its keep, and the reviewer ranked it a judgement call itself.
- **Rendering located errors only inline, leaving the summary panel for unplaceable ones.** The
  duplication is deliberate. The panel shows the full `$.rule.andAlso[2].spec` path, which is the
  thing a reader of this sample most needs to see — it is what `Validate` actually returns, and the
  inline placement is what the sample *did* with it. Showing one without the other would hide half
  the demonstration.

## The components ended up under test, and a failing check is why

The plan expected xunit and Shouldly only, on the reasoning that a framework-free authoring core is
testable without bUnit and the dependency surface should stay small. That held for the core. It did
not survive contact with `codecov/patch`, which came back at **59.32% of diff against an 89.47%
target**.

The useful thing was not the number but what the breakdown showed. Measured per file, the authoring
core was already at or near complete — `RuleDocumentWriter` 37/37, `DraftNode` 20/20,
`DraftNodeKinds` 27/27, `AuthoringSession` 23/24 — and **every one of the 94 uncovered lines was
`Program.cs` or one of the two `.razor` files.** The shortfall was precisely the part of the slice
whose behaviour had been verified *by hand in a browser* and nowhere else.

Put that way the decision makes itself: the manual pass was checking real properties — the remove
control disabled at an operator's minimum, an error rendered against the node it was located on and
no other, the verdict and justification changing together when the model changes — and a property
worth checking once by hand is worth checking on every run. bUnit turns exactly those into tests.
It is a test-project dependency, so the sample's own reference gate is untouched.

The result is **49 tests and 97.8%** on the sample, arrived at in two passes — and the second pass is
the more interesting one, because it is where the number stopped being the point. Covering the
components took the patch from 59.32% to 87.57%, still short of target. Reading the remainder again
named the editor's own event handlers, the document-rename handler, and one registered proposition
whose predicate no test had ever evaluated. All three are real behaviour:

- a kind change re-seeds operands; choosing a proposition records it; add and remove mutate the
  draft and notify the owner;
- renaming the document is the clearest demonstration of the suffix rule on the page — the summary
  follows the new name while the justification keeps naming the same operands;
- and *every name the picker offers* should bind, because a name that does not is a proposition the
  author can select and never author a valid document with. That one is a genuine gate, not a
  coverage artefact: it would catch a mistyped registration.

What is left is five lines: `Program.cs`'s four lines of WebAssembly host bootstrap, which cannot run
outside a browser, and `ResolveNode`'s `return null` for a path matching no node at all — reachable
only through a document-level error the sample's writer cannot emit, which is exactly why
`LocatedError.Node` is nullable. Neither is worth manufacturing a test for, and saying so is more
honest than a suppression.

`TestContext` and `RenderComponent` are both obsolete in bUnit 2.9; `BunitContext` and `Render<T>`
are the current spelling, and `TreatWarningsAsErrors` made that a build failure rather than a warning
to ignore.

Two small production changes came out of it. The kind and proposition dropdowns gained `class="kind"`
and `class="spec"`, because a test that reaches its target by index position is a test that breaks
when the markup is rearranged and says nothing about why. And the `Disables_remove…` test was
mutation-checked like the gates were — the `disabled` binding was removed, that test alone went red,
and it was restored.

**The general point, since it will recur:** a coverage check is not a target to satisfy. It was
useful here because reading *which* lines it named turned a number into a decision — and the answer
was not "write tests until it goes green", it was "the thing you only checked by hand is the thing
it is pointing at".

## Verification

- `dotnet test --configuration Release` over the whole solution: **17 test assemblies, 0 failures**,
  including the 49 new tests. A bare `dotnet build` is also clean, which is the check that covers the
  target frameworks the test run does not exercise.
- The app was **run and driven in a browser**, not merely built: authoring a valid document,
  switching the evaluated model to see the outcome and justification change, and clearing a
  proposition to confirm that `$.rule.andAlso[0].spec` resolves back to the operand the author is
  editing and renders inline beside it. Console clean; both colour schemes checked. Re-run after the
  review round to confirm the remove controls are disabled at an operator's minimum and enable again
  once an operand is added.

**Not run:** `pnpm e2e` and the `ui/` workspace suites. Nothing in this slice touches them — it adds
no workspace member, and the sample deliberately contains no JavaScript.

## Where this leaves spec 4

§7's fourth obligation — "the Blazor sample authors a valid rule document through
`Motiv.Serialization` alone (no `rules-core`)" — is met, and enforced rather than asserted. The
`src/examples/` gap ticket #108 opened is closed.

The remaining open child of #169 is **#172, spec 4L, the manual screen-reader pass**, which is
assigned to a human by design and is the last of §7's obligations without an artefact.

## What a later slice should know

- **The stale directories are gone from git but not from disk.**
  `src/examples/Motiv.RulesEngine.Sample` and `.Tests` have had zero tracked files since 4C; only
  local `bin`/`obj` remnants keep them present in a listing. Do not read their existence as evidence
  that a hosted example survived the rehoming.
- **If `Motiv.Serialization` ever grows a public document model, this sample is the thing to check.**
  Its whole shape — `DraftNode`, `RuleDocumentWriter`, the path map — exists because there is not
  one. It should shrink dramatically, and the adoption page's boundary paragraph should shrink with
  it.
- **The gates encode a claim about the artefact, so widening the sample means editing them
  deliberately.** That is the intent: a new reference should be a decision someone made, not one
  that slipped in.
