# Phase 10: Constructor Spec Declaration Migration - Research

**Researched:** 2026-03-13
**Domain:** Roslyn SyntaxFactory — constructor parameter injection via primary constructor, verification that SFMK requirements are met by existing code
**Confidence:** HIGH

## Summary

Phase 9 already completed the full SyntaxFactory migration of `ComposedSpecClassDeclaration`, including the `containingTypeName` parameter path that generates constructor-injected spec classes. The SFMK requirements were originally written referencing a legacy `CustomSpecDeclarationSyntax.CreateWithConstructorInternal()` method, but that monolithic method no longer exists — it was replaced by the `SpecClassDeclaration` class hierarchy in Phase 8.

**The "constructor spec" is not a separate code path.** It is `ComposedSpecClassDeclaration.Build()` called with a non-null `containingTypeName`. This produces `class Foo(ContainingType instance) : Spec<TModel>(() => { ... })` — a primary constructor parameter carrying the containing class instance. This path is exercised by three existing tests: single-variable with instance methods, multi-variable with instance methods, and the complex block-namespace variant.

Direct code inspection of `ComposedSpecClassDeclaration.cs` (post-Phase 9) confirms: no `ParseExpression`, no `ParseStatement`, no `ParseCompilationUnit`, no `StringBuilder`, no raw string interpolation used for code generation. The `BuildParameterList()` override uses `ParameterList(SingletonSeparatedList(Parameter(Identifier("instance")).WithType(ParseTypeName(containingTypeName))))` — a pure SyntaxFactory construction. `ParseTypeName` is retained per the established project convention for handling C# keyword types.

**Primary recommendation:** Phase 10 is a verification phase. Confirm SFMK-01 through SFMK-03 are satisfied by the existing implementation. The planner should create a single verification task: code-inspect `ComposedSpecClassDeclaration.BuildParameterList()` for the constructor path, run the 94 CodeFix tests, and close all three requirements.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| SFMK-01 | `CustomSpecDeclarationSyntax.CreateWithConstructorInternal()` uses SyntaxFactory (no StringBuilder or raw string code generation) | `ComposedSpecClassDeclaration.Build()` with non-null `containingTypeName` IS this path. Post-Phase 9, no string-based code generation remains anywhere in `ComposedSpecClassDeclaration`. |
| SFMK-02 | Constructor parameter and instance method injection via SyntaxFactory | `BuildParameterList()` override constructs `ParameterList(SingletonSeparatedList(Parameter(Identifier("instance")).WithType(ParseTypeName(containingTypeName))))`. The `instance.MethodName(...)` expressions in the block lambda body are produced by `ExpressionTransformer.PrefixInstanceMethods`, which operates on `ExpressionSyntax` nodes directly (not string manipulation). |
| SFMK-03 | All existing constructor spec tests pass unchanged | 94 CodeFix tests pass currently. Tests covering the constructor path: `Should_inject_instance_via_constructor_when_expression_calls_instance_methods`, `Should_convert_multiple_variables_with_instance_methods_and_generate_model`, `Should_convert_complex_inside_a_namespace_block_and_handle_multiple_clauses_with_instance_methods`, and the debug-output variant. |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.CodeAnalysis.CSharp | 4.x+ | Roslyn SyntaxFactory API | Official C# code generation API; established in Phases 7-9 |
| using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory | N/A | Static import to reduce verbosity | Project-wide convention in all `Syntax/` files |

**Installation:** All dependencies already installed — no new dependencies needed.

## Architecture Patterns

### Constructor Spec Path Through Existing Class Hierarchy

The constructor injection path flows entirely through `ComposedSpecClassDeclaration` with `containingTypeName != null`:

```
LogicalExpressionToSpecConverter.BuildSingleVarComposedSpec()
  └── new ComposedSpecClassDeclaration(
          syntaxContext, propositionName,
          innerLambdaModelType: variableTypeName,
          innerLambdaParameterName: variable.Name,
          decomposition,
          containingTypeName: containingTypeName)   // NON-NULL triggers constructor param
      .Build()

LogicalExpressionToSpecConverter.BuildMultiVarComposedSpec()
  └── new ComposedSpecClassDeclaration(
          syntaxContext, propositionName,
          innerLambdaModelType: defaultModelName,
          innerLambdaParameterName: "m",
          decomposition,
          resolvedContainingTypeName,               // NON-NULL if hasInstanceMethods
          nestedRecordName: defaultModelName,
          nestedRecordParameterList: recordParameterList)
      .Build()
```

### Pattern: Constructor Parameter via SyntaxFactory (ALREADY IMPLEMENTED)

`ComposedSpecClassDeclaration.BuildParameterList()` override:

```csharp
// Source: src/Motiv.CodeFix/Syntax/ComposedSpecClassDeclaration.cs (current post-Phase 9)
protected override ParameterListSyntax BuildParameterList() =>
    containingTypeName is not null
        ? ParameterList(SingletonSeparatedList(
            Parameter(Identifier("instance"))
                .WithType(ParseTypeName(containingTypeName))))
        : ParameterList();
```

This produces `(MyNamespace.Playground instance)` as a primary constructor parameter on the generated spec class. `ParseTypeName` is retained per project convention — it correctly handles both simple names (`Playground`) and qualified names (`MyNamespace.Playground`).

### Pattern: Instance Method Injection via ExpressionTransformer (ALREADY IMPLEMENTED)

Instance method calls like `IsGreen(text)` become `instance.IsGreen(text)` in the lambda body:

```csharp
// Source: src/Motiv.CodeFix/LogicalExpressionToSpecConverter.cs (current)
var decomposition = ExpressionDecomposer.Decompose(
    logicalExpressionSyntax,
    expr =>
    {
        var result = ExpressionTransformer.PrefixInstanceMethods(expr, instanceMethodNames);
        if (staticMethodNames.Count > 0 && containingTypeName != null)
            result = ExpressionTransformer.PrefixStaticMethods(result, staticMethodNames, containingTypeName);
        return result;
    });
```

`ExpressionTransformer.PrefixInstanceMethods` operates on `ExpressionSyntax` nodes — no string manipulation involved. The `instance.` prefix is added structurally via `MemberAccessExpression`.

### Generated Output Shape

A single-variable constructor spec (e.g., `string text` with `IsGreen(text)` instance method):

```
public class IsFeatureEnabledProposition(MyNamespace.Playground instance) : Spec<string>(() =>
{
    var isNullOrEmpty = Spec
        .Build((string text) => string.IsNullOrEmpty(text))
        .Create("string.IsNullOrEmpty(text)");

    var isGreen = Spec
        .Build((string text) => instance.IsGreen(text))
        .Create("IsGreen(text)");

    return isNullOrEmpty.AndAlso(isGreen);
});
```

A multi-variable constructor spec (with nested record):

```
public class IsFeatureEnabledProposition(MyNamespace.Playground instance) : Spec<IsFeatureEnabledProposition.Model>(() =>
{
    // ... clause declarations
    return (clause1.AndAlso(clause2)).AndAlso(clause3);
})
{
    public record Model(int ValueA, int ValueC, string Text);
}
```

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Primary constructor parameter with type | String format `"(ContainingType instance)"` then `ParseParameterList` | `ParameterList(SingletonSeparatedList(Parameter(Identifier("instance")).WithType(ParseTypeName(type))))` | Already done; type-safe, handles qualified names |
| Instance method prefix in expression | String replacement `"instance." + methodName` | `ExpressionTransformer.PrefixInstanceMethods(expr, methodNames)` | Already done; structural `MemberAccessExpression` prefixing |

## Common Pitfalls

### Pitfall 1: Mistaking Phase Scope
**What goes wrong:** Treating SFMK as requiring new implementation work when the migration is already complete.
**Why it happens:** Requirements reference `CreateWithConstructorInternal()` which no longer exists — replaced by Phase 8/9.
**How to avoid:** Verify by direct code inspection and test execution before writing implementation tasks.
**Warning signs:** If all 94 tests pass and no `ParseExpression`/`ParseCompilationUnit` calls exist in `ComposedSpecClassDeclaration.cs`, the requirements are met.

### Pitfall 2: ParseTypeName for Qualified Names
**What goes wrong:** Replacing `ParseTypeName(containingTypeName)` with `IdentifierName(containingTypeName)` for the instance parameter type.
**Why it happens:** `containingTypeName` may be a qualified name like `MyNamespace.Playground` (in file-scoped namespace contexts). `IdentifierName` only handles simple names.
**How to avoid:** Keep `ParseTypeName` for any type name that may contain dots (qualified names). This is the established Phase 7/8 convention.
**Warning signs:** Test failures for expressions inside file-scoped namespaces (`namespace X;` style).

### Pitfall 3: SpecInvocationReplacer.BuildConstructor is a Separate Concern
**What goes wrong:** Confusing the constructor that `SpecInvocationReplacer` builds in the **containing class** with the primary constructor parameter on the **generated spec class**.
**Why it happens:** Two different constructors are involved: (1) `public Playground() { _prop = new Prop(this); }` in the containing class — built by `SpecInvocationReplacer.BuildConstructor` (already uses SyntaxFactory); (2) `(ContainingType instance)` primary constructor on the generated spec class — built by `ComposedSpecClassDeclaration.BuildParameterList`.
**How to avoid:** SFMK-02 is about the spec class parameter. `SpecInvocationReplacer.BuildConstructor` is out of scope for Phase 10 (it's already SyntaxFactory-based; no migration needed).

## Code Examples

### BuildParameterList — Constructor Injection (Post-Phase 9, Current State)
```csharp
// Source: src/Motiv.CodeFix/Syntax/ComposedSpecClassDeclaration.cs
protected override ParameterListSyntax BuildParameterList() =>
    containingTypeName is not null
        ? ParameterList(SingletonSeparatedList(
            Parameter(Identifier("instance"))
                .WithType(ParseTypeName(containingTypeName))))
        : ParameterList();
```

### ExpressionTransformer Usage for Instance Method Prefixing (Current State)
```csharp
// Source: src/Motiv.CodeFix/LogicalExpressionToSpecConverter.cs
var result = ExpressionTransformer.PrefixInstanceMethods(expr, instanceMethodNames);
// Produces MemberAccessExpression(IdentifierName("instance"), IdentifierName(methodName))
// for each instance method invocation in the expression tree
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `CustomSpecDeclarationSyntax.CreateWithConstructorInternal()` with string templates | `ComposedSpecClassDeclaration.Build()` with `containingTypeName` parameter via SyntaxFactory | Phase 8 (class hierarchy refactor) | No string generation; constructor parameter is a `ParameterListSyntax` node |
| `ParseParameterList("(ContainingType instance)")` | `ParameterList(SingletonSeparatedList(Parameter(...).WithType(ParseTypeName(...))))` | Phase 8 | Typed, structural construction |

**Deprecated (already removed before Phase 10):**
- `CustomSpecDeclarationSyntax.CreateWithConstructorInternal()` — replaced by `ComposedSpecClassDeclaration`
- String-based constructor parameter list parsing — replaced by `ParameterList(SeparatedList(...))`

## Open Questions

None — the implementation is complete and verified. All questions about SyntaxFactory patterns for this feature were resolved in Phases 8/9.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit (via Roslyn code fix testing framework) |
| Config file | none (auto-discovered) |
| Quick run command | `dotnet test src/Motiv.CodeFix.Tests/Motiv.CodeFix.Tests.csproj -q` |
| Full suite command | `dotnet test /c/Dev/Motiv/Motiv.sln -q` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SFMK-01 | `ComposedSpecClassDeclaration.Build()` with `containingTypeName` uses SyntaxFactory only | unit (code inspection) + integration | `dotnet test src/Motiv.CodeFix.Tests -q` | Yes — compile-time enforcement |
| SFMK-02 | Constructor parameter + instance method injection via SyntaxFactory | integration | `dotnet test src/Motiv.CodeFix.Tests -q` | Yes — `Should_inject_instance_via_constructor_when_expression_calls_instance_methods` (line 775), `Should_convert_multiple_variables_with_instance_methods_and_generate_model` (line 865), `Should_convert_complex_inside_a_namespace_block_and_handle_multiple_clauses_with_instance_methods` (line 966), `Should_convert_instance_methods_with_debug_tap` (debug tests line 135) |
| SFMK-03 | All existing constructor spec tests pass unchanged | regression | `dotnet test src/Motiv.CodeFix.Tests -q` | Yes — 94 tests currently green (confirmed 2026-03-13) |

### Sampling Rate
- **Per task commit:** `dotnet test src/Motiv.CodeFix.Tests/Motiv.CodeFix.Tests.csproj -q`
- **Per wave merge:** `dotnet test /c/Dev/Motiv/Motiv.sln -q`
- **Phase gate:** Full solution suite (CodeFix.Tests + Motiv.Tests on net10.0) green before `/gsd:verify-work`

### Wave 0 Gaps
None — existing test infrastructure covers all phase requirements. The constructor spec path is already exercised by 4+ tests in `MotivConvertToSpecTests` and `MotivConvertToSpecWithDebugOutputTests`.

## Sources

### Primary (HIGH confidence)
- `src/Motiv.CodeFix/Syntax/ComposedSpecClassDeclaration.cs` — direct inspection confirms no string-based code generation in `BuildParameterList()`, `AttachLambdaBody()`, `AddClassBody()`, or `GenerateClauseStatementSyntaxes()`
- `src/Motiv.CodeFix/LogicalExpressionToSpecConverter.cs` — direct inspection of `BuildSingleVarComposedSpec()` and `BuildMultiVarComposedSpec()` confirming SyntaxFactory-only construction
- `src/Motiv.CodeFix/Syntax/SpecClassDeclaration.cs` — abstract base: confirmed no string generation
- `src/Motiv.CodeFix.Tests/MotivConvertToSpecTests.cs` lines 775-1065 — exact expected output for all constructor spec test cases
- `src/Motiv.CodeFix.Tests/MotivConvertToSpecWithDebugOutputTests.cs` lines 135-226 — debug output constructor spec test case
- Test run result: 94/94 passed (confirmed 2026-03-13)

### Secondary (MEDIUM confidence)
- `.planning/phases/08-simple-spec-declaration-migration/08-01-SUMMARY.md` — Phase 8 established class hierarchy; `containingTypeName` parameter was part of original design intent for Phase 10
- `.planning/phases/09-composed-spec-declaration-migration/09-RESEARCH.md` — Phase 9 research documented all remaining string-parse calls; `BuildParameterList()` with `containingTypeName` was already SyntaxFactory at that point

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new dependencies; established stack from Phases 7-9
- Architecture: HIGH — all code directly inspected post-Phase 9; constructor path confirmed fully migrated
- Pitfalls: HIGH — derived from direct code inspection and knowledge of how Phase 8 reorganized the class hierarchy

**Research date:** 2026-03-13
**Valid until:** 90 days (Roslyn SyntaxFactory API is stable; no external dependencies changed)
