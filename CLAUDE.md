# Motiv Project Guidelines

## What Is Motiv?

Motiv is a .NET library that solves the **Boolean Blindness Problem** — when a boolean expression evaluates, you lose all context about *why* the value is true or false. Motiv preserves this reasoning by implementing the **Specification Pattern** with a fluent builder API, turning boolean expressions into composable, explainable propositions.

## Core semantics — summary

The worked detail lives in the **`motiv-semantics` skill**; load it before changing a proposition,
an assertion or metadata payload, a `Create()` call, an operator composition, or anything that
alters justification output. What must hold without looking it up:

- **Three proposition types.** *Minimal* (`Build(pred).Create("name")`), *explanation*
  (`WhenTrue("t").WhenFalse("f")`), *metadata* (non-string payloads, which **require** a name).
- **A supplied name always outranks WhenTrue/WhenFalse as the source of explanation text.** With
  `Create("name")` the assertions become `"name == true"` / `"name == false"` and the payloads —
  strings included — demote to `Values`. Only unnamed `Create()` lets the strings *be* the
  assertions. Getting this backwards compiles, passes a loose test, and silently produces the wrong
  text. **Exception:** `Spec.From(...)` expression trees, where only `Reason` takes the suffix and
  `Assertions` stay the decomposed clauses.
- **Policy vs Spec is a type-level guarantee.** A *Policy* resolves to a single `Value`; a *Spec*
  accumulates. `WhenTrue`/`WhenFalse` (singular) give a policy, `WhenTrueYield`/`WhenFalseYield` a
  spec. `Value` is a *selection*, `Values` the full causal set — they are not the same thing, and
  for an unsatisfied `OrElse` chain `Value != Values.Single()`.
- **Results are de-noised**: only assertions that influenced the outcome surface.

## Policy Preservation
- `!policy` returns a policy
- `policy.OrElse(policy)` and `policy.AndAlso(policy)` return a policy
- All other logical *combinators* return a spec

`ChangeModelTo` and `ToAsyncSpec` also preserve policy-ness — they re-target or lift a policy rather
than combining two, so they are not combinators and the rule above does not apply to them.

Policy preservation follows **short-circuiting**. A short-circuiting combinator always has a
well-defined last-evaluated operand, so a single `Value` is a total function of the evaluation path:
- `OrElse` — the first operand that matched, else the final fallback. This is `??`.
- `AndAlso` — the first operand that failed, else the final success. This is `Result`-chaining: "which gate stopped me?"
- `Not` — one operand in, one out.
- Eager `Or` / `And` / `XOr` have no last-evaluated operand, since both always evaluate. No operand is canonical, so they correctly return specs.

`OrElse`, `AndAlso` and `Not` all implement this. `policy.AndAlso(policy)` returns a policy across the
sync, async, expression-tree and result surfaces, as `OrElse` does.

Preservation is a **static-type property**. `policy.OrElse(spec)` returns a spec, and declaring
policies as `IEnumerable<SpecBase<TModel, TMetadata>>` before calling `OrElseTogether()` is the same
act — it returns an `OrElseSpec`, not an `OrElsePolicy`. Declare a non-policy and you abandon
preservation. This is by design, not a covariance defect.

**Operator overloads cannot carry policy preservation — do not re-propose this.** C# cannot overload
`||` directly: `x || y` compiles to `T.true(x) ? x : T.|(x, y)`, and the selected `operator |` must
take *and* return exactly `T`. A policy-preserving `||` therefore forces a policy-preserving `|` —
but `|` is eager `Or` with no canonical operand, so `satisfiedPolicy | unsatisfiedPolicy` would
report `Satisfied == true` while returning the *unsatisfied* operand's value. Two further blockers:
`x || y` short-circuits by returning `x` itself, unwrapped, so no `OrElse` node appears in the
justification tree; and an `operator |` on `PolicyBase` that meant `OrElse` would make `|` eager on
specs and lazy on policies, so widening a variable's declared type would silently change evaluation
semantics.

## Architecture Notes

- **Avoid over-DRYing**: The codebase intentionally has some duplication between proposition types. Each builder path has nuanced differences. Explicit code is preferred over complex abstractions with branching logic.
- **Results are composable**: `BooleanResultBase<TMetadata>` instances from different model types can be combined with operators, enabling cross-domain reasoning.
- **De-noising**: Results only surface assertions that influenced the final outcome, filtering out irrelevant branches.
- **Batch refactoring verification**: When refactoring multiple files with the same pattern, verify all files are modified before moving to the next phase — use `git status` or `git diff --stat` to confirm the expected set of changed files matches the plan.
- **Constructor signature changes**: When changing the signature of an `internal` type's constructor, search for all call sites across both production and test code before editing — test files often construct internal types directly via `[InternalsVisibleTo]` and will break if missed.
- **Example project tests**: When changing behavior that affects justification output, assertion text, or result formatting, run the full solution test suite — not just `Motiv.Tests`. The example projects (`src/examples/Motiv.Poker.Tests`, `src/examples/Motiv.ECommerce.Tests`, `src/examples/Motiv.SmartHome.Tests`) also contain integration-level assertions on justification strings and will break if not updated.
- **npm package publishability**: `@motiv-rules/core` and `@motiv-rules/react` are published from
  `ui/`, and changes to either package's `exports` map, `files` field, dependencies or version must
  keep `pnpm -C ui verify:publishable` green — it packs each package the way a publish would and
  checks the tarball, which is the only place the exports map, `files` and `workspace:` rewriting are
  actually read. Both packages carry one version and release together on a `motiv-rules-v*` tag; a
  `v*` tag is the NuGet train and must not be used for them. Because the packages are
  `"type": "module"`, every entry point names its declarations *per condition* (`.d.ts` under
  `import`, `.d.cts` under `require`) — a shared `types` makes the package unimportable from
  CommonJS. See `docs/adoption/index.md`.
- **The second adapter is evidence, not a package**: `ui/examples/vue-adapter` is a worked Vue
  adapter over `@motiv-rules/core` offering the React surface symbol for symbol. It is
  `private: true`, so `pnpm -r publish` and `pnpm -C ui verify:publishable` skip it while
  `pnpm -r build`/`typecheck`/`test` do not — Motiv maintains **one** adapter, so never add it to
  the release train or to `release-npm.yml`'s manifest list. Its purpose is to make the price the
  tier table publishes checkable: `test/price.test.ts` measures *both* adapters' source trees and
  fails when they drift from the two marked tables in `docs/adoption/index.md`
  (`<!-- react-adapter-price -->`, `<!-- vue-adapter-price -->`). Touching either adapter means
  editing that page in the same commit; `test/bindings-only.test.ts` additionally refuses any
  import in the example beyond `vue` and the core.
- **Studio accessibility**: changes to Studio's colour tokens, ARIA attributes or component
  structure must keep `pnpm -C ui/apps/studio a11y` green — `axe-core` over every view and every
  open surface in both colour schemes, gated in CI. It needs no .NET host. Two conventions it
  enforces: an `aria-controls`/`aria-activedescendant` IDREF is dropped whenever its target is
  unmounted (a reference to an absent element is invalid, not harmless), and the accessible name of
  a composition is the text Motiv generates for it (`accessibleExpression`), never a hand-written
  restatement that could drift from what is on screen. See `docs/accessibility/index.md`.
- **The conformance report is generated, not written**: `docs/accessibility/vpat.md` is rendered
  from `ui/apps/studio/a11y/conformance.ts` — never edit it by hand; run
  `pnpm --filter @motiv-rules/studio a11y:report`. A record row claims a *kind* of evidence
  (`axe`, the keyboard suite, a structural argument, an owed manual pass) and never names an axe
  rule; which rules an `axe` claim resolves to is read from axe's own tags at check and render time.
  `test/a11y/conformance.test.ts` refuses a record that claims axe coverage for a criterion axe has
  no rule for, omits coverage the sweep does run, cites a keyboard test that does not exist, or
  rests a *Supports* on nothing but an owed manual pass. Adding a criterion, changing a verdict or
  upgrading `axe-core` all mean regenerating the report in the same commit.
- **Documentation**: CLAUDE.md is for AI guidance and project conventions — not user-facing feature documentation. When asked to document a feature, add it to `README.md` (brief example under Core Features) and `docs/` (detailed pages following the existing structure: `docs/{feature}/index.md`, individual method pages, `toc.yml`, plus entries in `docs/toc.yml` and `docs/Overview.md`).
- **Performance refactoring**: When replacing LINQ with manual loops or caching computed values, verify that short-circuiting and lazy evaluation semantics are preserved. Moving a call from a `when` guard or lazy context to eager evaluation is a common regression — the original code may have intentionally deferred work that is only needed in some branches.
- **`Evaluate` vs `IsSatisfiedBy`**: `Evaluate` is the current public API for rich evaluation (returns `BooleanResultBase`). `IsSatisfiedBy` is retained as an `[Obsolete]` shim for backwards compatibility. `Matches` is the lightweight boolean-only evaluation. Internal overrides use `EvaluateSpec` / `EvaluatePolicy`.

## Test-Driven Development

Follow TDD strictly when developing features or fixing bugs:

1. **Write a failing test first** — define the expected behavior before writing implementation code
2. **Run it to confirm it fails** — verify it fails for the right reason
3. **Write the minimum code to pass** — only enough to make the test green
4. **Run it to confirm it passes** — verify correctness
5. **Refactor if needed** — clean up while keeping tests green

Never write implementation code without a corresponding test. If fixing a bug, first write a test that reproduces it. Run the full test suite before considering work complete.

## Post-Implementation Code Review

After applying changes and confirming tests pass, **always** spawn a `code-simplifier` agent to review the changed code. The agent should focus on:

- **Code duplication** — identify semantically identical code that should be consolidated
- **Convoluted design** — simplify overly complex class hierarchies, unnecessary indirection, or tangled dependencies
- **Procedural code** — refactor imperative step-by-step logic into more declarative, composable patterns where appropriate
- **Long methods** — break down methods that do too much into smaller, well-named, single-responsibility methods
- **Other anti-patterns** — god classes, feature envy, primitive obsession, deep nesting, poor naming, etc.

This step is mandatory — do not skip it. If the agent identifies improvements, apply them and re-run the affected tests before considering the task complete.

## Roslyn CodeFix Conventions

Moved to `src/Motiv.CodeFix/CLAUDE.md` — a nested `CLAUDE.md` loads when working in that subtree,
so the conventions cost nothing in sessions that never touch the Roslyn projects.

## Agent skills

### Issue tracker

Issues and PRDs live as **GitHub issues** on `karlssberg/Motiv`, via the `gh` CLI. See `docs/agents/issue-tracker.md`.
`gh` is **not installed in cloud containers** — use the GitHub MCP tools there; that doc carries the mapping.

### Domain docs

**Single-context** — `CONTEXT.md` + `docs/adr/` at the repo root. See `docs/agents/domain.md`.

### Bundle specs — the build phase

The four implementable bundle specs (Trust & Control, Durability & Data, Operability & Evidence,
Surface Quality) live **on branch `wayfinder/enterprise-grade-product`**, under
`.scratch/enterprise-grade-product/specs/` — deliberately off `main`. Read one with
`git show origin/wayfinder/enterprise-grade-product:.scratch/…`; the design docs under
`docs/superpowers/specs/` cite them by that path.

- **The ledger is the build map, issue #169.** Its open children are the remaining slices; its table
  is the shipped ones. Discovery map #100 is closed history — all 22 of its children are locked
  *decisions*, not work, so it says nothing about build progress. Never derive "what is next" by
  pattern-matching slice letters against filenames in `docs/superpowers/`: that listing is
  incomplete, and the letters are not spec-§6 step numbers.
- **`/next-spec` runs the frontier query** over #169 and claims the winner. Prefer it over
  reconstructing the state by hand.
- **Every slice writes its plan and design doc in the same commit as the implementation** —
  `docs/superpowers/plans/YYYY-MM-DD-spec-<slice>-<name>.md` and the matching
  `…-design.md` under `docs/superpowers/specs/`. Ten of the first nineteen slices skipped this, which
  is how the series became unreadable from the docs alone. A slice with green tests and no docs is
  not done.

### Cloud containers cannot build the .NET side

There is **no .NET SDK** in a Claude Code cloud container for this repo, and egress to the .NET
distribution is blocked, so one cannot be installed (issue #173). `Motiv.Tests`, the
`src/examples/*.Tests` suites, `pnpm e2e` and `Motiv.Studio` are all unrunnable there; only the
`ui/` workspace builds. The instruction above to run the full solution suite therefore cannot be
followed in a cloud session — when that applies, **say which suites you could not run and why**
rather than reporting the UI suite green as if it were the whole. A slice that touches C# needs a
local session.
