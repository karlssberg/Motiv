---
phase: 09-composed-spec-declaration-migration
plan: 02
subsystem: codefix
tags: [roslyn, syntaxfactory, codefixprovider, parameterlist, record]

# Dependency graph
requires:
  - phase: 09-composed-spec-declaration-migration
    plan: 01
    provides: ComposedSpecClassDeclaration with class hierarchy and SpecClassDeclaration base
provides:
  - ComposedSpecClassDeclaration accepts ParameterListSyntax for nested record parameters
  - LogicalExpressionToSpecConverter builds ParameterListSyntax via SyntaxFactory
  - Zero ParseParameterList calls in ComposedSpecClassDeclaration
affects:
  - future composed-spec refactoring phases

# Tech tracking
tech-stack:
  added: []
  patterns:
    - ParameterList(SeparatedList(...)) for building typed record parameter lists without string parsing
    - SyntaxFactory.Parameter(Identifier).WithType(ParseTypeName) for typed parameter construction

key-files:
  created: []
  modified:
    - src/Motiv.CodeFix/Syntax/ComposedSpecClassDeclaration.cs
    - src/Motiv.CodeFix/LogicalExpressionToSpecConverter.cs

key-decisions:
  - "ParameterListSyntax passed through constructor instead of string: eliminates ParseParameterList from ComposedSpecClassDeclaration.AddClassBody"
  - "ParseTypeName retained for parameter types: handles keyword types (int, string, bool) correctly per Phase 7/8 convention"

patterns-established:
  - "SyntaxFactory.ParameterList(SeparatedList(symbols.Select(s => Parameter(Identifier(s.Name)).WithType(ParseTypeName(...))))) for record parameter list construction"

requirements-completed: [SFMC-03, SFMC-05]

# Metrics
duration: 6min
completed: 2026-03-12
---

# Phase 09 Plan 02: Composed Spec Declaration Migration (Part 2) Summary

**Nested record parameter list migrated from ParseParameterList(string) to SyntaxFactory ParameterList with typed ParameterSyntax nodes, eliminating the last string-parse call in ComposedSpecClassDeclaration**

## Performance

- **Duration:** 6 min
- **Started:** 2026-03-12T21:53:32Z
- **Completed:** 2026-03-12T21:59:52Z
- **Tasks:** 1
- **Files modified:** 2

## Accomplishments
- Eliminated the last `ParseParameterList` call from `ComposedSpecClassDeclaration.AddClassBody`
- Changed `nestedRecordParameters: string?` constructor parameter to `nestedRecordParameterList: ParameterListSyntax?`
- Updated `LogicalExpressionToSpecConverter.BuildMultiVarComposedSpec` to build `ParameterListSyntax` via `SyntaxFactory.ParameterList(SeparatedList(...))` instead of `string.Join`
- All 94 CodeFix tests pass unchanged

## Task Commits

Each task was committed atomically:

1. **Task 1: Migrate nested record parameter list to SyntaxFactory ParameterList** - `d96d282d` (feat)

**Plan metadata:** (docs commit follows)

## Files Created/Modified
- `src/Motiv.CodeFix/Syntax/ComposedSpecClassDeclaration.cs` - Changed constructor param from `string? nestedRecordParameters` to `ParameterListSyntax? nestedRecordParameterList`; updated `AddClassBody` to use `WithParameterList(nestedRecordParameterList)` directly
- `src/Motiv.CodeFix/LogicalExpressionToSpecConverter.cs` - Added `using Microsoft.CodeAnalysis.CSharp` and `using static SyntaxFactory`; replaced string-based `recordParameters` with `ParameterList(SeparatedList(...))` construction

## Decisions Made
- Retained `ParseTypeName` for parameter type construction per Phase 7/8 convention — it correctly handles C# keyword types (`int`, `string`, `bool`) that would otherwise require additional mapping
- Used `SyntaxFactory.ParameterList(SeparatedList(...))` matching the pattern established in Phase 8

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
- Pre-existing environment issues (net8.0/net9.0 build failures, Analyzer.Tests assembly version mismatch) prevent full solution suite verification. These are environment-level issues predating this plan, confirmed by reverting our changes and observing the same failures. All CodeFix tests (94) and Motiv.Tests (3273 on net10.0) pass.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Phase 09 Plan 02 complete. All ParseParameterList calls eliminated from ComposedSpecClassDeclaration.
- No blockers for subsequent composed-spec-declaration-migration phases.

---
*Phase: 09-composed-spec-declaration-migration*
*Completed: 2026-03-12*
