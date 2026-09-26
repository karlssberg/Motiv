# Code fix expression contexts — Design

**Date:** 2026-09-26
**Status:** Implemented as a stack of PRs. Every convertible context in the matrix passes.
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

7. **The spec field precedes the member that reads it.** Static initializers run in textual order.
   A spec field appended after a `static readonly bool X = Spec.Matches(…)` initializer compiles,
   but it is still null when `X` is initialized. The field therefore goes after the last field, or
   before the containing member when that comes earlier. This is a runtime failure that "it
   compiles" cannot see. An exact-output test found it, not the matrix.
8. **The fixed document keeps the source's line endings and layout.** Each context is also run with
   CRLF line endings, and the output must contain no bare `\n`. `SyntaxIndentHelper` rejoined lines
   with `\n`, and three other places hard-coded it. A blank line separates the spec fields from the
   next member. A later conversion in the same type moves that blank line down, so the spec fields
   stay grouped together. `using Motiv;` gets its own blank line only when it is the first `using` in
   the file.

## Bugs the matrix found beyond the method rewrite

- **The provider converted the wrong node.** `FindNode(span)` returns the *outermost* node with
  that span. For an argument that node is the `ArgumentSyntax`, and the provider then climbed to
  the enclosing invocation: `Console.WriteLine(n > 0)` became a spec over `Console.WriteLine`. In
  `: base(…)` there is no enclosing expression, so no fix was offered. Fixed with
  `getInnermostNodeForTie: true`.
- **`using Motiv;` was followed by two blank lines** whenever the file already had a `using`.

## Follow-up layers

9. **Everything the expression reads is a model value** (`codefix/capture-model-values`).
   Properties, query range variables and pattern variables from an enclosing `case … when` are
   passed in the way fields already were, so the spec stays static and captures no `this`. A range
   variable's type comes from `GetTypeInfo` on the identifier that reads it, because
   `IRangeVariableSymbol` carries no type. A symbol the expression *declares* is never a model
   value, because the generated lambda declares it too. That covers `o is string s`, `out var n`
   and a lambda's parameter. The last one was a bug: `numbers.Any(x => x > 5)` made `x` a model
   value.

10. **A generic spec holds itself** (`codefix/generic-method-specs`). When the expression depends on
    the surrounding type parameters (a method's or the containing type's), the spec class declares
    them, with the constraints copied from their declaration. It also carries
    `public static readonly XProposition<T> Instance = new();`, and the call site reads
    `XProposition<T>.Instance`. A field on the containing type cannot have a method's type parameter
    in its type, but a static field on the generic class can, and the runtime keeps one per closed
    type. The spec is therefore still built once per type argument, not once per call. When the
    expression also calls instance methods there is no `this` to capture statically, so the fix is
    not offered.

11. **The spec decides as the expression did** (`codefix/composition-correctness`).
    `MotivConvertToSpecSemanticsTests` is the semantic oracle that "it compiles" cannot provide. It
    compiles each expression before and after the fix into collectible assemblies and runs both over
    every combination of inputs. It found that the composition used a negated or `^` operand as the
    receiver of `.AndAlso`/`.OrElse` without parentheses. `!a && b` became `!a.AndAlso(b)`, which is
    `!(a && b)`, and `a ^ b && c` changed meaning the same way. Both compiled. The composition now
    parenthesizes by its own precedence rules instead of copying the source's parentheses. A
    receiver is wrapped only when it is not a primary expression. `!` keeps parentheses around a
    compound operand so it reads clearly. The source's redundant parentheses are dropped.
    The oracle also found that two different clauses could derive the same variable name
    (`isDefaultEquals` twice). This happened because `ClauseSet` matched composition identifiers to
    clauses *by name*. Composition leaves are now positional placeholders, and a colliding name gets
    a numeric suffix. Only the composition's outermost chain is broken across lines. A chain inside
    one of its arguments stays on one line, so its indentation can't suggest it continues the outer
    chain.

## Out of scope, left as skipped rows

- **Constructors that `this`-capturing specs need** when the class already declares one. The old
  behaviour, which deleted a primary constructor's parameter list, is kept and not widened.
- **Two conversions in one member.** Both derive the same proposition name.
- **Names for inline positions.** A condition or argument has no assignment target or return to
  name it after, so the name comes from the expression. `if (n > 0 && n < 10)` becomes
  `NProposition`. Naming by the enclosing member, or by the clause's meaning, is its own change.
- **A corpus run** with Fix All over real repositories.
