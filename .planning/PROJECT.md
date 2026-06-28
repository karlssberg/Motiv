# Motiv

## What This Is

A .NET library that solves "Boolean Blindness" by transforming boolean expressions into explainable propositions (Specification Pattern). Developers write boolean logic and get back not just true/false, but structured explanations of *why* — enabling decision transparency, debugging, and human-readable output. Ships with a Roslyn analyzer and code fix that detect boolean expressions and offer to convert them into Motiv specs.

## Core Value

Boolean expressions must produce meaningful, structured explanations — not just true/false. If users can't understand *why* a result occurred, the library has failed.

## Current Milestone: CodeFix SyntaxFactory Refactor

**Goal:** Refactor CodeFix from string-based code generation to pure SyntaxFactory construction, ensuring generated code integrates properly with target codebases.

**Target features:**
- Replace all string-based source code construction (StringBuilder, raw string interpolation) with SyntaxFactory API calls
- Maintain string interpolation only for runtime string literal values (WhenTrue/WhenFalse descriptions, identifiers)
- Preserve identical generated output — existing tests are the verification gate
- PropositionModelSyntax.cs already uses SyntaxFactory (reference implementation)

## Requirements

### Validated

<!-- Shipped and confirmed working. -->

- ✓ Expression tree support — full parity with BooleanPredicate path
- ✓ Analyzer (MOTIV0001) — detects comparison, logical, `is`/pattern-matching expressions
- ✓ CodeFix — converts boolean expressions to Motiv specs (return, local decl, assignment contexts)
- ✓ CodeFix derives names from context (not hard-coded)
- ✓ CodeFix does not inject Debug.WriteLine
- ✓ Multi-target — .NET 8, 9, 10, .NET Standard 2.0
- ✓ Analyzer and CodeFix bundled in NuGet package

### Active

<!-- Current scope. SyntaxFactory refactor prerequisite. -->

- [ ] CustomSpecDeclarationSyntax — replace StringBuilder/raw strings with SyntaxFactory
- [ ] ConvertToSpecCodeFix — replace raw string method/class generation with SyntaxFactory
- [ ] SpecInvocationSyntax — replace raw string expression generation with SyntaxFactory
- [ ] All existing codefix tests pass after refactor

### Out of Scope

<!-- Explicit boundaries. -->

- Full IDE integration beyond Roslyn analyzer/codefix — too broad for RC
- Performance optimization — no evidence of issues
- Additional NuGet package metadata/branding — cosmetic, post-RC

## Context

- Branch `feature/expression-trees-support` is 200+ commits ahead of main
- Expression tree support is functionally complete with full test coverage
- No TODOs, FIXMEs, NotImplementedExceptions, or skipped tests in codebase
- Analyzer currently detects: comparison operators (==, !=, <, >, <=, >=) and logical operators (&&, ||, !)
- CodeFix derives names from context (Phase 2 complete), Debug.WriteLine removed
- CodeFix handles return statements, local declarations, and assignments
- CodeFix currently builds source code as strings then parses — needs SyntaxFactory refactor
- Examples exist: ECommerce, Poker, SmartHome

## Constraints

- **Target frameworks**: net8.0, net9.0, net10.0, netstandard2.0 — must maintain all
- **Analyzer/CodeFix**: Must target netstandard2.0 (Roslyn requirement)
- **Backward compatibility**: Existing Motiv API surface must not break
- **Testing framework**: xUnit with AutoFixture/NSubstitute per CLAUDE.md

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Derive codefix names from context | Hard-coded names are a poor DX; context-derived names are more useful | ✓ Good |
| Remove Debug.WriteLine from codefix | Generated code should be clean; debugging is user's choice | ✓ Good |
| Ternary branches → WhenTrue/WhenFalse | Natural mapping; ternary already expresses true/false outcomes | — Pending |
| Analyzer: `is`/pattern only (not method calls) | Focused expansion; method calls deferred to avoid false positives | ✓ Good |
| XML docs: review quality, not coverage | Coverage already 100%; focus on accuracy and helpfulness | — Pending |
| SyntaxFactory over string building | String-based code gen doesn't integrate with target codebase formatting; SyntaxFactory is Roslyn best practice | — Pending |
| String interpolation OK for literal values | WhenTrue/WhenFalse descriptions and identifiers are runtime strings, not source code | — Pending |

---
*Last updated: 2026-02-08 after SyntaxFactory refactor milestone start*
