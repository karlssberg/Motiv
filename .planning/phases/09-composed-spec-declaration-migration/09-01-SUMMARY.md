---
phase: 09-composed-spec-declaration-migration
plan: 01
subsystem: codefix
tags: [roslyn, syntaxfactory, expressiontree, codefixprovider]

# Dependency graph
requires:
  - phase: 08-simple-spec-declaration-migration
    provides: SpecClassDeclaration abstract base, SpecFluentChainBuilder static helper
provides:
  - ExpressionSyntax-based composition pipeline for composed specs (no ParseExpression round-trips)
  - ReplaceNodes-based clause identifier substitution replacing string.Replace
affects: [10-expression-tree-support, 11-advanced-composition]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - SyntaxFactory.ReplaceNodes for structural identifier substitution over string.Replace
    - ExpressionSyntax composition tree built via SyntaxFactory nodes (IdentifierName, ParenthesizedExpression, PrefixUnaryExpression, InvocationExpression, BinaryExpression)
    - Eliminate ParseExpression round-trips by flowing ExpressionSyntax through the entire pipeline

key-files:
  created: []
  modified:
    - src/Motiv.CodeFix/ExpressionDecomposition.cs
    - src/Motiv.CodeFix/ExpressionDecomposer.cs
    - src/Motiv.CodeFix/Syntax/ClauseSet.cs
    - src/Motiv.CodeFix/Syntax/ComposedSpecClassDeclaration.cs

key-decisions:
  - "ExpressionSyntax flows end-to-end: leaf clauses use IdentifierName(clauseName) composition nodes instead of string names"
  - "ReplaceNodes with DescendantNodesAndSelf().OfType<IdentifierNameSyntax>() replaces string.Replace for structural correctness"
  - "OriginalText (string) retained in Clauses tuple for SpecFluentChainBuilder .Create(name) call"

patterns-established:
  - "Composition tree pattern: SyntaxFactory nodes at every level (leaf, not, paren, binary, method-call)"
  - "ReplaceNodes pattern: structural identifier renaming vs string replacement"

requirements-completed: [SFMC-01, SFMC-02, SFMC-04, SFMC-05]

# Metrics
duration: ~10min
completed: 2026-03-12
---

# Phase 09 Plan 01: Composed Spec Declaration Migration Summary

**Eliminated ParseExpression round-trips and string.Replace clause substitution from the composed spec pipeline by flowing ExpressionSyntax nodes end-to-end through ExpressionDecomposer, ClauseSet, and ComposedSpecClassDeclaration.**

## Performance

- **Duration:** ~10 min
- **Started:** 2026-03-12T21:40:00Z
- **Completed:** 2026-03-12T21:50:48Z
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments
- Changed `ExpressionDecomposition.CompositionExpression` from `string` to `ExpressionSyntax`, and `Clauses` tuple `TransformedText` (string) to `TransformedExpression` (ExpressionSyntax)
- Updated `ExpressionDecomposer` to build `SyntaxFactory` composition nodes: `IdentifierName` for leaf clause placeholders, `ParenthesizedExpression`, `PrefixUnaryExpression`, `InvocationExpression` (for `.AndAlso`/`.OrElse`), and `BinaryExpression` (for XOR)
- Updated `ClauseSet` constructor and `UniqueClauses` dictionary to hold `ExpressionSyntax` values; replaced `string.Replace` in `ResolveComposition` with `ReplaceNodes` on `IdentifierNameSyntax` nodes
- Removed both `ParseExpression` calls from `ComposedSpecClassDeclaration` — `AttachLambdaBody` and `GenerateClauseStatementSyntaxes` now use `ExpressionSyntax` directly

## Task Commits

Each task was committed atomically:

1. **Task 1: Migrate ExpressionDecomposition and ExpressionDecomposer to ExpressionSyntax** - `24d67b12` (feat)
2. **Task 2: Migrate ClauseSet and ComposedSpecClassDeclaration to consume ExpressionSyntax** - `a678bfa4` (feat)

**Plan metadata:** (docs commit follows)

## Files Created/Modified
- `src/Motiv.CodeFix/ExpressionDecomposition.cs` - Changed `string CompositionExpression` and `string TransformedText` to `ExpressionSyntax`
- `src/Motiv.CodeFix/ExpressionDecomposer.cs` - Build `SyntaxFactory` nodes for composition tree instead of string interpolation
- `src/Motiv.CodeFix/Syntax/ClauseSet.cs` - Updated constructor/dictionary types; `ResolveComposition` now returns `ExpressionSyntax` using `ReplaceNodes`
- `src/Motiv.CodeFix/Syntax/ComposedSpecClassDeclaration.cs` - Removed `ParseExpression` calls, use `ExpressionSyntax` directly

## Decisions Made
- `OriginalText` (string) retained in the `Clauses` tuple because `SpecFluentChainBuilder.Build` needs it for the `.Create("name")` call — it cannot be derived from the `ExpressionSyntax` without a ToString round-trip
- `transformedExpression.ToString()` used as deduplication key in `ClauseSet` dictionary (preserves existing semantics)
- Added `using Microsoft.CodeAnalysis;` to `ClauseSet.cs` to expose the `ReplaceNodes` extension method on `SyntaxNode`

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Added missing `using Microsoft.CodeAnalysis;` to ClauseSet**
- **Found during:** Task 2 (ClauseSet migration)
- **Issue:** `ReplaceNodes` is a `SyntaxNode` extension method defined in `Microsoft.CodeAnalysis` namespace, which was not imported in `ClauseSet.cs`
- **Fix:** Added `using Microsoft.CodeAnalysis;` alongside the existing `using Microsoft.CodeAnalysis.CSharp.Syntax;`
- **Files modified:** src/Motiv.CodeFix/Syntax/ClauseSet.cs
- **Verification:** Build succeeded, all 94 tests pass
- **Committed in:** a678bfa4 (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** Single missing using directive; no scope creep.

## Issues Encountered
- Transient MSB3492 cache file lock errors when running `dotnet test` immediately after `dotnet build` — resolved by running `--no-build` and pre-building separately. Not a code issue.

## Next Phase Readiness
- Composition pipeline fully SyntaxFactory-based; no string round-trips remain in composed spec generation
- Downstream phases can consume `ExpressionSyntax` from `ExpressionDecomposition` directly

---
*Phase: 09-composed-spec-declaration-migration*
*Completed: 2026-03-12*

## Self-Check: PASSED

- FOUND: src/Motiv.CodeFix/ExpressionDecomposition.cs
- FOUND: src/Motiv.CodeFix/ExpressionDecomposer.cs
- FOUND: src/Motiv.CodeFix/Syntax/ClauseSet.cs
- FOUND: src/Motiv.CodeFix/Syntax/ComposedSpecClassDeclaration.cs
- FOUND commit: 24d67b12 (Task 1)
- FOUND commit: a678bfa4 (Task 2)
