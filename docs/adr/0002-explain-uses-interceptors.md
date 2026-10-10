# Compile-time explanations use interceptors, not source rewriting

Status: accepted

`Motiv.Explain` makes boolean methods marked `[Explain]` log a decomposed Motiv justification in non-prod
builds and leaves prod builds untouched. C# offers no supported way to rewrite existing source during a build:
source generators are additive only. We use C# interceptors (stable from the .NET 9.0.2xx SDK), emitted by a
source generator that only runs when `MotivExplain=true`. The interceptor replaces each direct call with one
that evaluates a decomposed spec. The spec itself is generated inside the method's own partial type, so its
clauses can read the same private members the method does.

## Considered options

- **Stryker-style mutant switching.** Rejected: it ships every variant in one binary and chooses at run
  time, which is exactly the prod cost this feature exists to avoid, and it compiles outside the normal build.
- **Fody / IL weaving.** Rejected: it would have to rebuild boolean clauses from IL branches, and the clause
  text that makes a justification readable is gone by then.
- **An MSBuild step that compiles a rewritten source tree.** Rejected for now: it is the most capable option,
  but it means owning a custom task, design-time build behaviour and `#line` mapping.
- **Metalama.** Not chosen: it replaces method bodies and so would reach every caller, but it adds a
  third-party compiler, and we did not confirm an aspect can read the original expression's syntax.
- **Interceptors placed in the user's namespace.** Rejected: the user would have to list their namespaces in
  `InterceptorsNamespaces`, and the interceptors design asks components not to insert interceptors into
  namespaces they don't own. The interceptors live in `Motiv.Explain.Generated`, which the package's targets
  allow by themselves.

## Consequences

- Only direct calls in the declaring project are explained. Delegates, method groups, other projects,
  interface calls and calls from generated code are not. The analyzer warns about the first two.
- Explainable types must be `partial`. Instance interceptors are extension methods, because the compiler
  rejects a static interceptor that takes the receiver as an ordinary first parameter (CS9144).
- Clauses are delegates rather than expression trees, so `is` patterns, switch expressions and `?.` work.
