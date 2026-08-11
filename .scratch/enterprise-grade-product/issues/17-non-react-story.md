# Do the packages owe a non-React story?

Type: grilling
Status: resolved
Blocked by: 07

## Question

`@motiv/rules-core` is framework-free by construction — 2,079 lines of schema, DSL, validation,
document manipulation, and client with no UI dependency. `@motiv/rules-react` is the only adapter,
at 218 lines.

An enterprise evaluating this asks: *we are an Angular shop / a Blazor shop — can we use it?*

**Is React the supported target, or does the SDK owe an answer to everyone else?**

The session must resolve:

1. **Is `rules-core` genuinely framework-free, or accidentally React-shaped?** `RuleEditorStore` and
   `editor.ts` are the places to look — a store designed around `useSyncExternalStore` semantics is
   React-shaped even without importing React. Verify rather than assume; this is checkable.
2. **What would a second adapter cost?** If `rules-react` is 218 lines, a `rules-vue` might be
   comparable — which would be a cheap and highly credible signal. But the cost is not the adapter,
   it is everything ticket 07 decides to promote *above* it. A batteries-included React package has
   no cheap Vue equivalent.
3. **Is Blazor the more interesting answer?** This is a .NET product with a .NET audience. A Blazor
   consumer would need the contracts and the DSL in C#, not TypeScript — which is a very different
   proposition from another JS adapter, and arguably a better fit for the actual buyer. Consider
   whether the DSL parser and validation could be shared rather than reimplemented.
4. **Or is "React only" simply the honest answer?** Stating a supported target clearly and declining
   the rest is a legitimate enterprise posture — what is *not* legitimate is leaving it undocumented
   and letting adopters discover it.
5. **Web components as a hedge.** If 07 promotes UI into packages, shipping them as custom elements
   would serve every framework at once, at a real cost in ergonomics for the React consumer who is
   the actual current user.

Feeds the fog patch "docs, adoption, and the upgrade path".

## Inherited from ticket 07

Sub-question 1 is now partly answered by decision rather than investigation: **everything
framework-free goes into `@motiv/rules-core`** — path arithmetic, insertion rules, the accordion state
machine, DSL sync, completion, lint, token runs, vocabulary. That is deliberately the maximum
possible surface, precisely so this ticket has something to build on.

So the question sharpens to: with a genuinely large framework-free core, is a second adapter cheap?
`@motiv/rules-react` after promotion is bindings only — which is the best possible case for a
`rules-vue` or a Blazor consumer. Still verify sub-question 1 empirically (is `RuleEditorStore`
React-shaped in its semantics, even without importing React?) rather than assuming.

## Grounded — sub-1 verified: the core is genuinely framework-free

`editor.ts` exposes `subscribe(listener) => unsubscribe` (the universal observable contract) and a
`getState()` that **composes current fields fresh each call** — it does *not* implement
`useSyncExternalStore`'s cached-snapshot semantics. A React-shaped store would cache a referentially
stable snapshot for tearing-avoidance; this one makes no caching assumption, pushing the React-specific
adaptation into the 218-line adapter. No `react` import in `rules-core`; `validation.ts` consumes
`subscribe` framework-agnostically. **More neutral than a React-shaped store, not accidentally
React-shaped.**

## Answer

**The non-React story is a *two-runtime* story, and both cores already exist. React is the supported JS
adapter; other JS frameworks are cheap DIY on the verified-neutral core; and .NET/Blazor uses the C#
`Motiv.Serialization` stack directly — the better fit for the actual buyer. Web components rejected.**

### The framework-free core exists twice, by necessity

The DSL/schema/validation exists in **both** TypeScript (`rules-core`, for the browser editor) and C#
(`Motiv.Serialization`, for the server) — and they must already agree on one JSON rule-document schema
(ticket 06's pinned `$id`). So "who else can use this" is answered per *runtime*, not per framework, and
the cores are already built:

- **JS world → `rules-core` + a thin adapter.**
- **.NET world → `Motiv.Serialization` directly.**

### Support tiers (sub-4: the illegitimate thing is leaving it undocumented)

- **React — supported.** The one adapter Motiv maintains and tests.
- **Vue / Svelte / vanilla — enabled, not supported.** The verified-neutral core makes an adapter
  ~200 bindings-only lines (ticket 07 put *all* logic in core). Shipping *one* second adapter (Vue) is a
  cheap, highly credible signal **if resourced** — but optional; the neutral core is the deliverable,
  the adapter is not.
- **Blazor / .NET — the strategically better answer, nearly free.** A Blazor WASM consumer runs .NET in
  the browser and uses `Motiv.Serialization` (the same C# parser/validation the backend uses)
  **directly — it does not need `rules-core` at all.** The DSL is already shared across the TS/C#
  boundary. For the .NET buyer this fits better than any JS adapter (sub-3).

### Web components rejected (sub-5)

Ticket 07 made packages headless — there are no components to ship as custom elements — and web
components would tax the React consumer who is the current user. Headless-core + per-runtime-adapter
already serves everyone without that ergonomic cost.

## Downstream

- **To the "docs, adoption, upgrade path" fog:** the support-tier table above is the documentation
  deliverable — React supported, other JS enabled-DIY, .NET via `Motiv.Serialization`, web components
  declined. Stating it is the point (sub-4).
- **To ticket 06:** the shared JSON schema `$id` is what keeps the TS and C# cores in sync; it is load-
  bearing for the Blazor story, not just versioning hygiene.
