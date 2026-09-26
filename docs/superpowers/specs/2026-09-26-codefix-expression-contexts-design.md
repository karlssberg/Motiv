# Code fix expression contexts — Design

**Date:** 2026-09-26
**Status:** In progress. The layers ship as a stack of four PRs.
**Plan:** `docs/superpowers/plans/2026-09-26-codefix-expression-contexts.md`

## Problem

`MOTIV0001` flags any boolean expression, wherever it sits. The fix that answers the diagnostic was
written against one shape: a class method whose body is a single `return`, `var x = …;` or
assignment. `SpecInvocationReplacer` rebuilt the *whole method* around a two-statement body, and it
found that method, and the class around it, with `.First()`. The result:

- **Silent data loss.** In a method with more than one statement, every statement except the
  flagged one was deleted, along with the method's attributes. An `if`, `while`, `for`, ternary,
  lambda, `yield return` or `switch` arm lost its body and gained `var isSatisfied = …`.
- **Exceptions.** Property getters, field initializers, constructors, operators, records and
  structs have no enclosing class method, so `.First()` threw out of `GetOperationsAsync`.
- **Fixes that could not compile.** `const` fields and locals, attribute arguments, default
  parameter values, constant `case` labels and expression-tree lambdas were flagged, although no
  evaluation of a spec can appear there.

The suite never noticed, because every test used a single-statement method.

## How the bugs were found, and how the next ones will be

The research compared four ways to find position-dependent bugs in a code fix:

- **Hand-written tests per position.** Roslyn's own IDE tests do this (`IntroduceVariableTests`:
  `TestInAttribute`, `TestInSwitchSection`, …).
- **A context-template matrix.** One template per syntactic slot, run through a shared oracle.
  Skeletal program enumeration (PLDI 2017) is the formal version.
- **Corpus runs.** Clippy's `lintcheck --fix`, Black's diff-shades and StyleCopTester `/fixall`
  apply fixes across real code and rebuild it.
- **Generative fuzzing.** ASTGen, JDolly/SafeRefactor and Fuzzlyn generate programs.

The failures here share one cause: a fix that rewrote more than the expression it was asked to
convert. Every position fails for that same reason, so fuzzing would rediscover it many times over.
The matrix finds all of them for the price of one template each, and it is deterministic. Corpus
runs and generative fuzzing are deferred. They find the long tail, but only once the obvious slots
are sound.

## Decisions

1. **The matrix asserts what the verifier cannot.** `Microsoft.CodeAnalysis.Testing` compares exact
   text, so it cannot express "any output that compiles". `CodeFixHarness` builds an ad hoc
   workspace against the same `Net100` reference assemblies. It first checks that the template
   compiles, which guards against a broken template. It then applies the fix to the expression
   marked `[|…|]` and reports compiler errors in the fixed document. Exact output stays the
   business of the `MotivConvertToSpec*` suites.
2. **Surviving code is asserted, not inferred.** "It compiles" misses a fix that deletes code and
   still compiles. A template therefore ends each line that must outlive the fix with
   `// must survive`, and the harness reports any such line missing from the fixed document.
3. **Broken slots are skipped rows with named reasons, not absent rows.** xUnit 2.9 supports `Skip`
   per `InlineData` row. Each reason is a shared constant naming the cause (`MethodRewrite`,
   `RequiresMethod`, `UncapturedSymbol`), so every later layer's red test is an un-skip.
   `Should_cover_every_context_with_a_test_case` fails if a template has no row.
4. **Constant contexts are the analyzer's problem.** A position that must hold a compile-time
   constant, or sits in a lambda that becomes an expression tree, is never flagged. There is
   nothing the fix could write there.
5. **Specs are hoisted to `static readonly` unless they capture `this`.** A spec is an immutable
   decision tree built once. An instance field rebuilt it for every instance of the containing
   type. A static field also works where an instance field cannot be referenced: instance and
   static field initializers, constructor initializers, static local functions and structs. The
   field keeps the proposition's PascalCase name, as static-method conversions already did.
   The multi-variable `Model` that is created on every evaluation becomes a
   `readonly record struct`, so a conversion adds no heap allocation to the call site. The output
   already needs C# 12 for primary constructors, so C# 10's record structs cost nothing.
6. **The fix replaces the expression, not the method.** An expression that is the whole value of a
   `return`, a single-variable `var` or an assignment statement in a block keeps today's
   provenance-preserving form: a comment, `var xResult = Spec.Evaluate(…);`, then the original
   statement reading `xResult.Satisfied`. A bool expression-bodied method gets the same form in a
   block. Every other position replaces the expression in place with a check the field customizer
   chooses:
   - `Spec.Matches(…)` by default, the allocation-free fast path;
   - `Spec.Evaluate(…).Satisfied` for the debug action, because `Matches` deliberately skips a
     `Tap` callback (`TapSpecBase`).

   The spec field goes on the nearest `TypeDeclarationSyntax`, whether class, struct or record. An
   expression with no containing type (top-level statements) is offered no fix.

## Out of scope, left as skipped rows

- **Symbols the generated spec cannot see:** range variables in query clauses, pattern-introduced
  variables in `when` clauses, method type parameters, and instance properties. Each needs its own
  capture strategy.
- **Constructors that `this`-capturing specs need** when the class already declares one. The old
  behaviour, which deleted a primary constructor's parameter list, is kept and not widened.
- **Two conversions in one member.** Both derive the same proposition name.
- **A corpus run** with Fix All over real repositories, and a semantic property test that the
  original boolean equals `Satisfied`.
