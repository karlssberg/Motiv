# The doc comments nothing was compiling — Plan

**Date:** 2026-09-09
**Ticket:** [#220](https://github.com/karlssberg/Motiv/issues/220)
**Source:** none. #220 is a build-machinery ticket, not a bundle-spec slice, and is not a child of the
build map [#169](https://github.com/karlssberg/Motiv/issues/169). Its nearest kin is
[#173](https://github.com/karlssberg/Motiv/issues/173), the other follow-up here about the *machine*
rather than the tree. I checked all four bundle specs: spec 4 §2 owns the **npm** packages'
publishability gate and says nothing about the NuGet package's XML documentation, so claiming a
lineage would be an invention.
**Lineage:** [#211](https://github.com/karlssberg/Motiv/issues/211) (the `IFoldableOperation` rename,
whose `code-simplifier` pass had to *force the flag on* to check that 169 renamed references had not
broken a `<see cref>`) → this.

## The gap

`GenerateDocumentationFile` is set nowhere in the repo — no `.csproj`, no `.props`, no `.targets`.
Without it the C# compiler never runs its doc-comment binding pass, so doc comments are uncompiled
text: a `<see cref>` pointing at a renamed or deleted type fails no build, local or CI, and a
`<param>` tag naming a parameter that does not exist fails no build either.

That matters here more than in most repos, for two reasons. The `Motiv` XML docs are what ships to
consumers, and the Spec 3E follow-up series has repeatedly corrected them *as findings in their own
right* — while doing a great deal of renaming, which is exactly the change that breaks crefs.

## What I expect to find

The ticket reports that forcing the flag on for `src/Motiv/Motiv.csproj` produced **zero CS1574**
(broken cref) and some CS0419 / CS1572. I expect that to be true of `src/Motiv` and **not** of the
other four packable projects, which the ticket never built. If any CS1574 exists in the tree, the
defect class the gate is being built to catch is already present — which changes this from a
prophylactic to a repair.

## Approach

1. **Measure first.** Build all five packable projects with the flag forced on, and enumerate the
   diagnostics by ID and by site. Do not act on the ticket's counts.
2. **Split the diagnostics by what they refuse**, and let that decide which to enforce:
   - diagnostics that refuse a doc comment *contradicting the code* — CS1574 (cref does not resolve),
     CS1572 (`<param>` for a parameter that does not exist), CS0419 (cref is ambiguous across an
     overload group) — are the staleness the ticket exists to catch. **Fix these.**
   - diagnostics that refuse *absent prose* — CS1591 (public member has no doc), CS1573 (some params
     documented, not all) — are a completeness opinion, and this repo has a deliberate house style of
     documenting only the parameter that carries a non-obvious contract. **Suppress these, with the
     reason recorded in the props file where a reader will meet it.**
3. **Turn the flag on**, having established where MSBuild will actually honour it.
4. **Red-prove the gate by mutation.** A compile-time gate cannot be seen by a runtime test, so the
   only evidence it refuses anything is to break a cref deliberately and watch the build fail. Record
   the measurement; do not assert the gate works because the build is green.
5. **Guard the artifact with tests.** The XML file beside the assembly is the consumer-visible half
   (it is where IntelliSense comes from), and it is observable at runtime. State in the test itself
   what its green does *and does not* mean, so a later reader does not misread it as proof that crefs
   resolve.

## Scope

One PR. The ticket's steps 1–2 (fix the sites, set the property) and step 4 (ship the XML in the
package, which the SDK does for free once the property is on). Step 3 — whether CS1591 is wanted — is
decided here rather than deferred, because leaving it undecided is what would noise the build.

Explicitly out of scope: writing the missing doc comments CS1591 counts. That is a much larger piece
of work with a different character, and folding it in would bury the gate in prose.

## Risks

- **`TreatWarningsAsErrors` is already `true` repo-wide.** The flag cannot be turned on incrementally:
  every doc diagnostic becomes an error the same instant. There is no "warn for a while" mode
  available without also disabling the errors-as-errors discipline everywhere else.
- **MSBuild evaluation order.** `Directory.Build.props` is imported before the project body, so it
  cannot see `IsPackable`; `Directory.Build.targets` is imported after the SDK has already computed
  `DocumentationFile`. A property set in the wrong file is silently inert — the build stays green and
  produces nothing. Probe this rather than reason about it.
- **Widening past the shipping projects** would pull every test and example project into the gate.
  Measure the cost before assuming it is either free or prohibitive.
