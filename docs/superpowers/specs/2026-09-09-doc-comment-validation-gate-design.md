# The doc comments nothing was compiling — Design

**Date:** 2026-09-09
**Ticket:** [#220](https://github.com/karlssberg/Motiv/issues/220)
**Plan:** [`2026-09-09-doc-comment-validation-gate.md`](../plans/2026-09-09-doc-comment-validation-gate.md)
**Source:** none. #220 is a build-machinery ticket, not a bundle-spec slice, and is not a child of the
build map [#169](https://github.com/karlssberg/Motiv/issues/169). Its nearest kin is
[#173](https://github.com/karlssberg/Motiv/issues/173) — the other follow-up in that table about the
*machine* rather than the tree. All four bundle specs were checked: spec 4 §2 owns the **npm**
packages' publishability gate (`pnpm -C ui verify:publishable`) and is silent on the NuGet package's
XML documentation. Claiming a lineage to it would be an invention.
**Lineage:** [#211](https://github.com/karlssberg/Motiv/issues/211) → this. #211 renamed
`IOperationFold` to `IFoldableOperation` across 169 references; its `code-simplifier` pass had to pass
`-p:GenerateDocumentationFile=true` **by hand** to check that no `<see cref>` had been left dangling.
That the check had to be improvised is the ticket.

## What shipped

- `Directory.Build.props` — the policy statement: which doc-comment diagnostics are enforced, which
  are suppressed, and why.
- Five `.csproj` files (`Motiv`, `Motiv.Serialization`, `Motiv.Serialization.AspNetCore`,
  `Motiv.Serialization.EntityFrameworkCore`, `Motiv.Serialization.Sql`) — `GenerateDocumentationFile`.
- Ten source files — the **22** doc-comment defects the gate refuses, including **five broken crefs**.
  (85 diagnostics surface in total; the other 63 are the two suppressed IDs, below.)
- `src/Motiv.Tests/XmlDocumentationTests.cs` — three cases over the artifact the flag produces.

The NuGet package now carries `Motiv.xml`, so a consumer gets IntelliSense from the package. That was
the ticket's step 4, and once the property is on the SDK does it for free.

## The gap was that doc comments were not compiled at all

`GenerateDocumentationFile` reads like an output switch — emit an XML file, or don't. It is also the
switch for the compiler's **doc-comment binding pass**: the phase that resolves every `<see cref>`
against the semantic model and checks every `<param name>` against the real signature. With it off,
doc comments are trivia. Not under-checked — *unchecked*.

That is why a 169-reference rename could pass every build, local and CI, without anyone being able to
say whether it had broken a cross-reference.

## The ticket's headline claim was half wrong, and the measurement caught it

#220 reports:

> Building `src/Motiv/Motiv.csproj` with `-p:GenerateDocumentationFile=true` produced **zero CS1574**
> (broken cref) — the crefs are all currently sound

True of `src/Motiv`. The ticket built only `src/Motiv`. Across all five packable projects there are
**five broken crefs**:

| Site | Cref | Why it does not resolve |
|---|---|---|
| `PropositionSet.cs:21` | `RuleSet(SpecRegistry, IRuleStore, RuleSerializerOptions)` | the constructor gained a fourth `DecisionLog` parameter |
| `PropositionSet.cs:24` | `RuleSet(PropositionSet, IRuleStore, RuleSerializerOptions)` | same |
| `RuleSet.cs:27` | `RuleSet(PropositionSet, IRuleStore, RuleSerializerOptions)` | same |
| `ChangeRequestSet.cs:29` | `DirectWriteAsync` | the doc is on a record declared outside the class that owns the method |
| `MotivRulesServiceCollectionExtensions.cs:246` | `IHostedService` | `Microsoft.Extensions.Hosting` is not imported in that file |

So this slice is a **repair, not a prophylactic**. The defect class the gate is being built to catch
was already in the tree — and already shipping: the generated XML renders an unresolved cref as
`<see cref="!:DirectWriteAsync"/>`, which is a dead link in a consumer's IDE.

Three of the five are one defect: `RuleSet`'s constructors gained a `decisionLog` parameter during
Spec 3D (the durable decision sink) and three crefs elsewhere kept naming the three-parameter
signature. That is precisely the shape #220 predicted — *"a rename is exactly the change that breaks
crefs, and the series does a lot of renaming"* — arriving one ticket earlier than the ticket thought.

**Generalisable:** a ticket that reports a measurement reports the measurement it took, not the one
it describes. #220 says "the crefs are all currently sound" and shows a command scoped to one of five
projects. This is the fourth time in this series a ticket's own framing was half wrong and only a
re-measurement caught it, after [#192](https://github.com/karlssberg/Motiv/issues/192),
[#193](https://github.com/karlssberg/Motiv/issues/193) and
[#202](https://github.com/karlssberg/Motiv/issues/202) — and the first where the wrong half was a
*count of zero*, which is the reading least likely to be re-checked, because it asks for no work.

## Which diagnostics to enforce: contradiction, not absence

Turning the flag on surfaces 85 distinct diagnostics across the five projects. They are not one
population. Sorted by **what a diagnostic refuses**, they split cleanly:

| ID | Refuses | Count | Verdict |
|---|---|---|---|
| CS1574 | a cref that does not resolve | 5 | **enforce** |
| CS1572 | a `<param>` for a parameter that does not exist | 5 | **enforce** |
| CS0419 | a cref naming an overload group, so the link is arbitrary | 12 | **enforce** |
| CS1573 | some of a member's parameters documented, not all | 12 | suppress |
| CS1591 | a public member with no doc comment at all | 51 | suppress |

The first three refuse a doc comment that **contradicts the code**. They are the ones that go stale
when something is renamed, and they are the entire reason #220 exists. The last two refuse **absent
prose**. Neither can catch a doc that lies about the code; they count missing words.

That line also answers the ticket's open step 3 (*"decide whether CS1591 is wanted; it likely is
not"*) with a reason rather than a preference, and it decides CS1573 the same way — which the ticket
did not raise at all, because it never built `Motiv.Serialization`, where all twelve live.

CS1573's suppression has a second justification specific to this repo. It fires only where *some*
parameters are documented, and the three sites are `PropositionSet.Prepare`,
`PropositionSet.PrepareCreateCore` and `RuleParameterResolver.Resolve` — each documenting exactly the
one parameter carrying a non-obvious contract (`source`, `excluding`, `errorPath`) and leaving
`name`, `modelTypeId`, `documentJson` to speak for themselves. Three independent authors reaching the
same shape is a house style. Enforcing CS1573 would mean inventing prose for the obvious parameters,
which makes the docs worse. (The count is 12 rather than 3 because CS1573 fires once per
undocumented parameter, not once per member.)

The reasoning lives in `Directory.Build.props`, next to the `NoWarn` line, because that is where a
reader meets the decision. A suppression whose reason is only in a design doc is a suppression whose
reason will not be read.

## The same diagnostic, two opposite right answers

CS1572 fired at three sites for one mechanical reason: a `<param>` tag on a **type** declaration is
legal only when the type has a primary constructor, and all three types document constructor
parameters at type level while declaring explicit constructors. Same diagnostic, same cause.

The fixes are opposites.

- `BooleanResultsCollection`'s orphan `<param name="results">` is a **verbatim duplicate** of a tag
  the constructor already carries. Delete it.
- `HigherOrderPolicyPredicateOperation` and `HigherOrderSpecPredicateOperation` have orphan tags that
  are the **only** documentation those parameters have anywhere. Move them onto the public
  constructor.

Fixing on the warning ID — deleting every orphan tag, which is what a mechanical sweep does — would
have silently discarded two useful paragraphs while leaving the build green and the diagnostic count
at zero. **A diagnostic names a defect in the code's *form*; it does not tell you which of the
repairs preserves the code's *meaning*.**

## CS0419: fixed rather than suppressed, and why that was not obvious

Twelve crefs name an overload group (`AddPropositions`, `AddRuleStore`, `AsyncSpecBase.And`,
`PolicyBase.AndAlso`, `RuleSet.PrepareUpdateCore`). In every case the prose genuinely means *the
group* — "when `AddPropositions` was called" is true of either overload — so suppressing CS0419 with
"these deliberately refer to a method group" would have been defensible.

It was rejected because C# XML docs have no syntax for "the group": `cref="O:..."` is a
DocFX/Sandcastle convention the compiler does not implement, so the choice is a signature or a
`<c>code span</c>` with no link at all. Naming a signature costs the reader nothing — IntelliSense
and DocFX both render a cref as the member name regardless — and keeps CS0419 live to catch the case
where a cref became ambiguous by *accident*, which is the same staleness CS1574 catches one step
earlier.

`RuleSet.cs` settled it: two crefs in the same file already write
`<see cref="PrepareUpdateCore(string,string,int)"/>` and `<see cref="PrepareRevertCore(string,int)"/>`
in full. The bare one at line 810 was the outlier, not the convention.

## Where MSBuild will actually honour the property

This is the part that would have shipped as done, green, and inert.

The first design put the property in `Directory.Build.targets`, conditioned on `IsPackable`:

```xml
<PropertyGroup Condition="'$(IsPackable)' == 'true'">
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
</PropertyGroup>
```

A **derived** set beats a **listed** one — 4G's lesson — and `Directory.Build.props` cannot express
this, because it is imported *before* the project body and so cannot see `IsPackable` at all.

Measured: the build succeeds, reports `0 Error(s) 0 Warning(s)`, and produces **no XML file**.
`Directory.Build.targets` is imported after the SDK has already computed `DocumentationFile` from
`GenerateDocumentationFile`, so setting the property there is too late to be read. Nothing warns. The
property is present in the file, spelled correctly, and has no effect whatsoever.

**Generalisable, and it is 4I's lesson in a new place: a gate that is configured is not a gate that
runs.** The failure signature of a mis-placed MSBuild property is a *passing build*, identical to the
one you would get from a gate that ran and found nothing. Had the placement not been probed — had it
been reasoned about from import order, which is what the two candidate files' names invite — this
slice would have shipped a `Directory.Build.targets` entry, a green CI run, and no validation at all,
with the ledger recording it as done.

The shipped placement is therefore per-project, in the five `.csproj` files, beside the
`IsPackable` and `GeneratePackageOnBuild` they each already declare. It is a *listed* set, which is
the weaker form, and the reason it is acceptable rather than merely tolerated is below.

## What is not guarded, stated rather than glossed

A listed set can drift: a sixth packable project could be added without the property, and nothing
would fail.

`XmlDocumentationTests` closes the half that matters. It asserts `Motiv.xml` exists beside
`Motiv.dll`, names its assembly, and documents ``T:Motiv.Spec`2`` rather than being an empty stub — so
removing the property from `Motiv.csproj`, which is the realistic regression (someone silencing a
build error), goes red. `Motiv` is also the only package `release.yml` actually pushes, so it is the
only one where a missing XML file is user-visible today.

The other four are unguarded, deliberately. A test that derives the packable set by parsing every
`.csproj` under `src/` would catch the drift, but it would be the first test in this suite to reach
outside its own output directory into repo layout, and its own failure mode — cannot find the repo
root, therefore asserts nothing — is the "green while wrong" trap this ledger keeps recording. It is
filed rather than improvised.

**A compile-time gate cannot be seen by a runtime test at all.** `XmlDocumentationTests` asserts the
*artifact* the flag produces, not the *validation* the flag enables; no assertion in it distinguishes
a tree whose crefs resolve from one whose crefs are never bound. The test says so in its own summary,
so a later reader does not mistake its green for evidence about crefs — this is #208's *state the
limit where the green is read*, applied to a test whose limit is structural rather than incidental.

## Red-proving the gate

Since no test can witness the gate, the only evidence it refuses anything is a mutation.

The mutation is #211's own scenario, replayed: a `<see cref>` naming `IOperationFold`, the interface
that slice renamed away. One line in `src/Motiv/Spec.cs`:

```diff
-/// <summary>Represents a proposition that yields custom metadata based on the outcome of the underlying spec/predicate.</summary>
+/// <summary>See <see cref="IOperationFold"/> for the fold seam.</summary>
```

Built twice, `net10.0`, everything else identical:

| | Result |
|---|---|
| **A** — gate on (the shipped configuration) | `src/Motiv/Spec.cs(5,29): error CS1574: XML comment has cref attribute 'IOperationFold' that could not be resolved` — **1 Error(s)** |
| **B** — gate off (`-p:GenerateDocumentationFile=false`, the status quo ante) | **0 Error(s), 0 Warning(s)** |

B is the important row. It is not a control for form's sake: it is the only thing separating *the
gate refused this* from *something in the build refused this*. Without it, run A is equally consistent
with a gate that does nothing and a pre-existing check that was catching the defect all along — and
this ledger has now recorded three gates that were green while wrong ([#173](https://github.com/karlssberg/Motiv/issues/173))
and one that reported a property it was not checking ([#166](https://github.com/karlssberg/Motiv/pull/166)).
B also states the cost of *not* having done this slice, in the plainest available form: for the whole
history of the repository, that line compiled clean.

The package half was verified the same way — by looking rather than by inference:

```
$ unzip -l src/Motiv/bin/Debug/Motiv.9.1.1-alpha.0.62.nupkg | grep '\.xml'
  1350125  lib/net10.0/Motiv.xml
  1350125  lib/net8.0/Motiv.xml
  1350125  lib/net9.0/Motiv.xml
  1350125  lib/netstandard2.0/Motiv.xml
```

1.35 MB per target framework, in the `lib/` folder beside each assembly, which is where a consumer's
IDE looks. Four files that were not in the package before this slice.

## Verification

**Build.** `dotnet build Motiv.slnx` — 0 errors, 0 warnings, with the gate live on all five packable
projects. Before the fixes, the same five projects reported 85 distinct doc diagnostics.

**Tests.** `dotnet test Motiv.slnx` — **21,693 assertions passed, 0 failed**, across 17 assembly/TFM
combinations. `Motiv.Tests` reports 6,025 per TFM, three more than the 6,022 before this slice: the
three `XmlDocumentationTests` cases.

**Not run locally: the two `net472` combinations** (`Motiv.Tests` and `Motiv.Serialization.Tests`).
Both aborted with `Could not find 'mono' host` — no mono on this macOS host — which is a standing
property of this machine and not a consequence of the slice. CI runs them on `windows-latest`, where
they execute rather than merely compile. That is the whole of what could not be run.

**Red first.** The three `XmlDocumentationTests` cases were written before the property was set and
watched fail: all three red, each because `Motiv.xml` did not exist.

That reason is the same for all three, which is exactly what made the third case's own claim
unproven — see below.

**Environment note.** `dotnet build -t:Rebuild` crashed MSBuild worker nodes twice on this host
(`MSB4166`, memory pressure) — once on the whole solution, once on a single project at `-m:2`. It is
not a compile error, but its summary line reads `0 Warning(s) / 1 Error(s)`, and a crashed `Rebuild`
empties the *referenced* project's `bin` on its way out. Both figures above come from `-m:1` runs.

## The review found the vacuous assertion in the guard against vacuous assertions

The mandatory `code-simplifier` pass found a defect in `XmlDocumentationTests` itself. The third case
was written:

```csharp
document.Root?.Element("assembly")?.Element("name")?.Value.ShouldBe("Motiv");
```

The `?.` chain short-circuits the **whole expression, including the `ShouldBe` call at the end**. If
any link is null the expression evaluates to null and the assertion is never invoked — green. The
case could fail only on `XDocument.Load` throwing, which is what case 1 already asserts. It could not
fail on content at all.

The *Red first* run above did not catch this and could not have: all three cases went red for one
shared reason — the file did not exist — so the third case's own assertion was never among the things
that were proved to fail. **A suite that goes red for a reason common to every case has red-proved
that reason, not those cases.** This is the counterpart to #208's *state the limit where the green is
read*: here the limit was on what the **red** established.

The repair is also worth recording, because the first attempt did not compile:

```csharp
var assemblyName = document.Root?.Element("assembly")?.Element("name")?.Value;
assemblyName.ShouldBe("Motiv");   // error CS8604: possible null reference argument
```

The repo's own `ShouldlyLineEndingExtensions.ShouldBe(string actual, string expected)` takes a
non-nullable `actual`, so `TreatWarningsAsErrors` refused it — the nullable analysis catching, one
step later, the same "this can be absent" the `?.` chain had been silently swallowing. The shipped
form defaults with `??` to a named sentinel, so a missing element fails the assertion *and* says
which element was missing.

Red-proved three ways, since the point of the case is that it can now fail on content:

| Mutation | Result |
|---|---|
| none (as shipped) | 3 passed |
| `ShouldBe("NotMotiv")` | 1 **failed** |
| `Element("no-such-element")` | 1 **failed** |

The same pass also found **two of the CS0419-shaped edits fixed nothing**: `ChangeRequestSet.cs:720`
and `:728` sit *inside* `ChangeRequestSet`, where the bare `DirectWriteAsync` resolved and emitted no
diagnostic. They were reverted, leaving that file's diff at the single line the compiler actually
objected to. The tell was arithmetic — the diff made 14 disambiguation-shaped edits against a
measured population of 12 — which is worth more than it looks: **a fix count that exceeds the
diagnostic count means some edits are guesses**, and a sweep applied by pattern rather than by
diagnostic will always produce a few.

## Deferred

- **A drift guard for the packable set**, above.
- **Widening the gate past the shipping projects.** Setting `GenerateDocumentationFile` in
  `Directory.Build.props` for *every* project — default-on, opt-out — would derive the set by
  construction and validate crefs in test and example code too. The cost was **not measured**: the
  whole-solution rebuild needed to count the diagnostics crashed MSBuild worker nodes twice
  (`MSB4166`, memory pressure), and the question is outside #220's own scope, which is "the shipping
  projects". Recorded as unmeasured rather than as cheap or prohibitive.
- **A public cref pointing at an `internal` type.** `MotivRulesServiceCollectionExtensions.cs:246`
  crefs `MotivRefreshService`, which is `internal`, so it ships in the public XML as a link a
  consumer's IDE cannot resolve. No diagnostic fires — the cref *does* resolve, from inside the
  assembly — but it is the same class of dead link as the `!:DirectWriteAsync` above, reached by a
  different route. Not fixed here: it is pre-existing, and deciding what a public doc may reference
  is a policy question this ticket does not contain.
- **Writing the 51 doc comments CS1591 counts.** A different piece of work with a different
  character; folding it in would bury the gate in prose.
