# Project Research Summary

**Project:** Motiv CodeFix SyntaxFactory Refactor
**Domain:** Roslyn CodeFix internal refactoring (string-based to SyntaxFactory code generation)
**Researched:** 2026-02-08
**Confidence:** HIGH

## Executive Summary

This milestone is a pure internal refactoring: replacing string-based C# code generation (StringBuilder, raw string interpolation `$$"""..."""`, then `ParseCompilationUnit()` / `ParseExpression()`) with direct SyntaxFactory API construction in the Motiv CodeFix. The external behavior -- the generated C# code inserted into the user's codebase -- must remain byte-identical. The existing 10+ tests in `MotivConvertToSpecTests.cs` verify exact string output and serve as the pass/fail gate. No new package dependencies are needed; `Microsoft.CodeAnalysis.CSharp` v5.0.0 already provides all required APIs including `PrimaryConstructorBaseTypeSyntax`, `RecordDeclaration`, and `ClassDeclaration` with primary constructor parameter lists.

The recommended approach is to migrate file-by-file in increasing complexity order: `SpecInvocationSyntax.cs` (simplest, single expression), then `CustomSpecDeclarationSyntax.cs` (three creation methods: simple, composed, constructor), then `ConvertToSpecCodeFix.ReplaceMultiVariableExpression` (orchestrator-level field/method/constructor injection). The existing `PropositionModelSyntax.cs` already uses pure SyntaxFactory and serves as the in-codebase reference implementation. Use `SyntaxFactory` directly -- not `SyntaxGenerator` -- because the project needs C#-specific constructs (records, primary constructors) that `SyntaxGenerator` does not support.

The primary risks are trivia/whitespace fidelity and the complexity of primary constructor base types in SyntaxFactory. `NormalizeWhitespace()` destroys custom trivia (comments, blank lines between clauses), so the trivia strategy must be decided upfront: normalize first, then attach comments afterward. Line endings must use `CarriageReturnLineFeed` consistently since tests expect `\r\n`. The composition expression refactoring (changing `ExpressionDecomposition.CompositionExpression` from `string` to `ExpressionSyntax`) is the most architecturally impactful decision and should use `ReplaceNodes` instead of `string.Replace` for clause name deduplication.

## Key Findings

### Recommended Stack

No new dependencies. The existing `Microsoft.CodeAnalysis.CSharp` v5.0.0 and `Microsoft.CodeAnalysis.CSharp.Workspaces` v5.0.0 packages provide the full SyntaxFactory surface area.

**Core API decisions:**
- **SyntaxFactory (direct):** All code generation -- chosen over `SyntaxGenerator` because records, primary constructors, and `PrimaryConstructorBaseTypeSyntax` are C#-specific and not available through the language-agnostic abstraction
- **`PrimaryConstructorBaseTypeSyntax`:** Required for `Spec<T>(lambda)` base type with constructor arguments -- confirmed available in v5.0.0
- **`RecordDeclaration`:** Required for `public record Model(int X, int Y);` nested types -- confirmed with 8 overloads in v5.0.0
- **`ParseTypeName()`:** Remains valid for converting `ITypeSymbol.ToDisplayString()` results to `TypeSyntax` nodes -- this is not "string-based code gen" but legitimate type name parsing
- **`NormalizeWhitespace()`:** Applied at the outermost node boundary for consistent formatting, matching `PropositionModelSyntax.cs` precedent

See: [STACK.md](STACK.md)

### Expected Features

All 14 table stakes features (TS-1 through TS-14) are required for the migration to succeed -- this is a refactoring with zero tolerance for output differences. There is no MVP subset.

**Must have (table stakes):**
- TS-1: Class declaration with primary constructor and `PrimaryConstructorBaseTypeSyntax` (HIGH complexity)
- TS-2: Record declaration with positional parameters (MEDIUM complexity)
- TS-3: Lambda expressions -- both expression-body `() => expr` and block-body `() => { ... }` (MEDIUM)
- TS-4: Fluent method chains `Spec.Build().WhenTrue().WhenFalse().Create()` via nested `InvocationExpression`/`MemberAccessExpression` (HIGH)
- TS-7: Object creation expressions `new SpecName()`, `new SpecName(this)`, `new SpecName.Model(a, b)` (LOW)
- TS-8: Spec invocation chain `new Spec().IsSatisfiedBy(model).Satisfied` (MEDIUM)
- TS-9: Local variable declarations in block lambdas (MEDIUM)
- TS-10: Field declarations and constructor injection (MEDIUM)
- TS-11: Method body replacement with comment trivia (MEDIUM)
- TS-12: String literals with automatic escaping via `SyntaxFactory.Literal()` (LOW)
- TS-14: Whitespace and formatting via `NormalizeWhitespace()` (MEDIUM)

**Improvements delivered by migration (differentiators):**
- DF-1: Automatic string escaping eliminates manual `EscapeDoubleQuotes()` utility
- DF-2: Compile-time structural validation prevents malformed code generation
- DF-3: Composable syntax node builders reduce duplication across three creation methods

**Explicitly out of scope (anti-features):**
- AF-1: Do NOT introduce `Formatter.Format()` workspace dependency
- AF-4: Do NOT change test expected outputs
- AF-5: Do NOT create a generic "Syntax Builder" abstraction layer
- AF-6: Do NOT mix `Parse*` with SyntaxFactory within a single method (hybrid approach)

See: [FEATURES.md](FEATURES.md)

### Architecture Approach

The architecture remains structurally identical. Three files change internally; everything else is untouched. The migration targets are `SpecInvocationSyntax.cs`, `CustomSpecDeclarationSyntax.cs`, and `ConvertToSpecCodeFix.cs` (specifically `ReplaceMultiVariableExpression`). Helper components (name derivers, walkers, symbol extensions) continue to produce string/data outputs that bridge to SyntaxFactory via `IdentifierName()` and `ParseTypeName()`. The most significant architectural decision is whether `ExpressionDecomposition.CompositionExpression` changes from `string` to `ExpressionSyntax` -- the recommendation is yes (Strategy B), using `ReplaceNodes` for clause name deduplication instead of fragile `string.Replace`.

**Components that change (migration targets):**
1. **`SpecInvocationSyntax.cs`** -- Replace `ParseExpression($$"""...""")` with SyntaxFactory chain (LOW complexity)
2. **`CustomSpecDeclarationSyntax.cs`** -- Replace `StringBuilder` + `ParseCompilationUnit()` with SyntaxFactory construction across three creation methods (HIGH complexity)
3. **`ConvertToSpecCodeFix.cs` (`ReplaceMultiVariableExpression`)** -- Replace "parse temp class, extract members" anti-pattern with direct SyntaxFactory construction (MEDIUM-HIGH complexity)

**Components unchanged:** `MotivCodeFixProvider`, `PropositionModelSyntax` (already SyntaxFactory), `ClauseNameDeriver`, `ExpressionNameDeriver`, `InstanceMethodDetector`, `VariableExtractorWalker`, `SymbolExtensions`

**Components partially obsoleted:** `StringExtensions.EscapeDoubleQuotes()` (potentially removable), `CustomSpecDeclarationSyntax.ToCamelCase()` private duplicate (remove)

See: [ARCHITECTURE.md](ARCHITECTURE.md)

### Critical Pitfalls

1. **NormalizeWhitespace() destroys custom trivia** -- Call `NormalizeWhitespace()` first, then attach comments/blank lines afterward. Never add trivia before normalizing. This is the most likely source of test failures.
2. **NormalizeWhitespace() removes intentional blank lines** -- Inter-clause blank lines in composed specs will be collapsed. Must manually insert `EndOfLineTrivia` between clause variable declarations after normalization.
3. **CRLF vs LF line ending mismatch** -- SyntaxFactory defaults to `\n`; tests expect `\r\n`. Use `SyntaxFactory.CarriageReturnLineFeed` consistently. Create a `NewLine` constant in the first migration step.
4. **Primary constructor syntax complexity** -- `ClassDeclaration` with `ParameterList` + `PrimaryConstructorBaseType` in `BaseList` is significantly more complex than the string representation suggests. Use Roslyn Quoter to verify tree structure before implementing.
5. **Missing spaces between tokens** -- SyntaxFactory nodes have zero trivia by default. Without `NormalizeWhitespace()` or explicit trivia, `public class Foo` becomes `publicclassFoo`.

See: [PITFALLS.md](PITFALLS.md)

## Implications for Roadmap

Based on research, the migration should follow a strict file-by-file, method-by-method order driven by complexity escalation and dependency chains.

### Phase 1: Foundation and SpecInvocationSyntax Migration
**Rationale:** Establish the trivia management strategy and SyntaxFactory patterns on the simplest target first. Every subsequent phase depends on conventions set here.
**Delivers:** Migrated `SpecInvocationSyntax.cs` (3 overloads, ~30 lines of string code replaced). CRLF constant. Validated trivia approach.
**Addresses:** TS-5 (member access), TS-7 (object creation), TS-8 (spec invocation chain), TS-14 (formatting)
**Avoids:** Pitfall 3 (CRLF), Pitfall 4 (missing spaces), Pitfall 14 (type name parsing)

### Phase 2: Simple Spec Declaration (CustomSpecDeclarationSyntax.Create)
**Rationale:** Introduces the most critical SyntaxFactory construct -- primary constructor with `PrimaryConstructorBaseTypeSyntax` -- in the simplest context (single clause, expression lambda, no nested record).
**Delivers:** Migrated `CreateInternal` method. Proven pattern for class + primary constructor + base type + lambda.
**Addresses:** TS-1 (class with primary constructor), TS-3 (expression lambda), TS-4 (fluent chain), TS-6 (generic types), TS-12 (string literals)
**Avoids:** Pitfall 5 (primary constructor complexity), Pitfall 7 (generic type names)

### Phase 3: Composed Spec Declaration (CustomSpecDeclarationSyntax.CreateComposed)
**Rationale:** Highest complexity code path. Adds block lambda, local variable declarations, composition expressions, and nested record type. Depends on patterns from Phases 1-2.
**Delivers:** Migrated `CreateComposedInternal` method. Record declaration. Block lambda with clause variables and return statement.
**Addresses:** TS-2 (record declaration), TS-3 (block lambda), TS-9 (local variable declarations), composition expression construction
**Avoids:** Pitfall 2 (blank lines removed), Pitfall 8 (nested record ordering), Pitfall 9 (fluent chain indentation)

### Phase 4: Constructor Spec Declaration (CustomSpecDeclarationSyntax.CreateWithConstructor)
**Rationale:** Shares 90% of Phase 3 patterns, adds constructor parameter and instance method reference injection. Lower marginal risk.
**Delivers:** Migrated `CreateWithConstructorInternal` method. Instance method handling via SyntaxFactory node transformation (replacing string-based `ReplaceInstanceMethodCalls`).
**Addresses:** TS-10 (field declarations), constructor parameter in primary constructor, instance method MemberAccess injection
**Avoids:** Pitfall 5 (constructor parameters)

### Phase 5: Orchestrator Cleanup (ConvertToSpecCodeFix.ReplaceMultiVariableExpression)
**Rationale:** This is the orchestrator that calls the Syntax layer methods migrated in Phases 1-4. Eliminates the "parse temporary class to extract members" anti-pattern. Depends on all prior phases.
**Delivers:** Migrated `ReplaceMultiVariableExpression`. Direct field, method, and constructor construction without the temp-class parse round-trip. Comment trivia for original expressions.
**Addresses:** TS-10 (field declarations), TS-11 (method body replacement with comment)
**Avoids:** Pitfall 6 (comment preservation), Pitfall 15 (WithTriviaFrom scope)

### Phase 6: Dead Code Removal and Polish
**Rationale:** Clean up after all migration is stable and all tests pass.
**Delivers:** Removed `CustomSpecDeclarationSyntax.ToCamelCase()` duplicate. Removed `ReplaceInstanceMethodCalls()` string manipulation. Removed `UpdateCompositionWithCamelCaseNames`/`UpdateCompositionWithDeduplicatedNames` string-replacement methods. Potential removal of `EscapeDoubleQuotes()` if no longer used.
**Addresses:** DF-1 (automatic escaping), DF-3 (composable builders extracted)
**Avoids:** N/A -- cleanup pass, tests catch regressions

### Phase Ordering Rationale

- **Complexity escalation:** Each phase introduces 1-2 new SyntaxFactory constructs on top of patterns proven in previous phases. Phase 1 (expression only) -> Phase 2 (class + expression lambda) -> Phase 3 (class + block lambda + record) -> Phase 4 (class + constructor param) -> Phase 5 (member injection)
- **Dependency chain:** Phases 3-4 depend on Phase 2's primary constructor pattern. Phase 5 depends on the Syntax layer being fully migrated. Phase 6 depends on all tests passing.
- **Test isolation:** Each phase has dedicated test coverage. Phase 1 is validated by single-variable tests. Phases 2-4 are validated by multi-variable and instance-method tests. Phase 5 is validated by all multi-variable tests.
- **Pitfall avoidance:** The trivia strategy (Pitfalls 1-3) is established in Phase 1 before complexity escalates. The primary constructor pattern (Pitfall 5) is tackled in Phase 2's simple context before Phase 3's complex context.

### Research Flags

Phases likely needing deeper research during planning:
- **Phase 2:** Verify `PrimaryConstructorBaseTypeSyntax` API surface in the exact Roslyn v5.0.0 assemblies referenced by the project. Use Roslyn Quoter to generate correct tree structure for `public class X() : Spec<T>(() => expr);`
- **Phase 3:** The `ExpressionDecomposition.CompositionExpression` type change (string to ExpressionSyntax) has ripple effects through `DecomposeExpression`, `DeduplicateClauses`, and `UpdateCompositionWithCamelCaseNames`. Needs careful interface analysis before implementation.

Phases with standard patterns (skip research):
- **Phase 1:** Well-documented SyntaxFactory patterns (ObjectCreationExpression, MemberAccessExpression, InvocationExpression). `PropositionModelSyntax.cs` is the local reference.
- **Phase 4:** Incremental over Phase 3 -- adds one constructor parameter. Standard pattern.
- **Phase 5:** Field/method/constructor declarations are well-documented. `PropositionModelSyntax.cs` demonstrates constructor and property patterns.
- **Phase 6:** Pure cleanup -- remove dead code, extract helpers.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | No new dependencies. All APIs confirmed available in referenced v5.0.0 packages via official Microsoft Learn docs. `PrimaryConstructorBaseTypeSyntax` verified. |
| Features | HIGH | All 14 table stakes features have verified SyntaxFactory patterns. Existing `PropositionModelSyntax.cs` demonstrates 5 of them in-codebase. |
| Architecture | HIGH | Structural changes are minimal. Three files change internally. Migration targets and untouched files clearly identified from codebase analysis. |
| Pitfalls | HIGH | 15 pitfalls identified from official docs, Roslyn GitHub issues, and community reports. Cross-referenced with actual codebase vulnerabilities (e.g., CRLF hack on line 421 of ConvertToSpecCodeFix.cs). |

**Overall confidence:** HIGH

### Gaps to Address

- **Trivia strategy decision:** The research identifies two viable approaches (NormalizeWhitespace-then-trivia vs. manual trivia throughout). The final choice should be validated by implementing Phase 1 and comparing test output before committing to the approach for all phases.
- **ExpressionDecomposition type change:** Strategy B (carrying `ExpressionSyntax` instead of `string` for composition) is recommended but has not been prototyped. If the ripple effects through `DecomposeExpression` are too large, Strategy A (keep strings, convert at boundary) is a viable fallback.
- **Blank line preservation:** `NormalizeWhitespace()` collapses blank lines between clause declarations. The exact trivia sequence needed to restore them must be determined empirically by comparing test output during Phase 3.
- **Fluent chain formatting:** Whether `NormalizeWhitespace()` produces acceptable fluent chain indentation (`.WhenTrue()` on next line, indented) needs empirical validation. If not, manual line-break trivia on each `.` token may be needed.

## Sources

### Primary (HIGH confidence)
- [SyntaxFactory.PrimaryConstructorBaseType -- Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntaxfactory.primaryconstructorbasetype?view=roslyn-dotnet-5.0.0) -- Primary constructor base type API
- [SyntaxFactory.ClassDeclaration -- Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntaxfactory.classdeclaration?view=roslyn-dotnet-4.9.0) -- Class declaration overloads with parameterList
- [SyntaxFactory.RecordDeclaration -- Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntaxfactory.recorddeclaration?view=roslyn-dotnet-4.3.0) -- Record declaration API
- [SyntaxFactory.Literal -- Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntaxfactory.literal?view=roslyn-dotnet-4.9.0) -- Automatic string escaping
- [Syntax Transformation Guide -- Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/get-started/syntax-transformation) -- Official SyntaxFactory patterns and trivia handling
- Existing codebase: `PropositionModelSyntax.cs` -- In-project SyntaxFactory reference implementation
- Existing codebase: `MotivConvertToSpecTests.cs` -- 10+ tests defining exact expected output

### Secondary (MEDIUM confidence)
- [Generating C# code with Roslyn APIs -- Jeremy Davis (2024)](https://blog.jermdavis.dev/posts/2024/csharp-code-with-roslyn) -- Practical SyntaxFactory gotchas
- [NormalizeWhitespace line feed behavior -- Roslyn #24827](https://github.com/dotnet/roslyn/issues/24827) -- Blank line removal
- [Roslyn CSharpSyntaxGenerator source](https://github.com/dotnet/roslyn/blob/main/src/Workspaces/CSharp/Portable/CodeGeneration/CSharpSyntaxGenerator.cs) -- Roslyn's own SyntaxFactory patterns
- [Roslyn Quoter](https://roslynquoter.azurewebsites.net/) -- Tool for generating SyntaxFactory calls from target C# code

### Tertiary (LOW confidence)
- [Getting whitespace right with Roslyn CSharpSyntaxRewriter -- criticalhittech.com](https://criticalhittech.com/2019/03/19/getting-whitespace-right-with-roslyn-csharpsyntaxrewriter/) -- NormalizeWhitespace timing (2019, may be outdated)

---
*Research completed: 2026-02-08*
*Ready for roadmap: yes*
