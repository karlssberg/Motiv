# Domain Docs

How the engineering skills should consume this repo's domain documentation when exploring the codebase.

This repo is **single-context**: one `CONTEXT.md` and one `docs/adr/` at the root, shared across the C#
(`Motiv`, `Motiv.Serialization`, …) and TypeScript (`ui/packages/*`) sides. The two implementations
deliberately share one ubiquitous language — so there is one glossary, not one per package.

## Before exploring, read these

- **`CONTEXT.md`** at the repo root.
- **`docs/adr/`** — read ADRs that touch the area you're about to work in.

If any of these files don't exist, **proceed silently**. Don't flag their absence; don't suggest creating them upfront. The `/domain-modeling` skill (reached via `/grill-with-docs` and `/improve-codebase-architecture`) creates them lazily when terms or decisions actually get resolved.

## File structure

Single-context repo (this repo):

```
/
├── CONTEXT.md
├── docs/adr/
│   ├── 0001-approval-gate-is-a-motiv-rule.md
│   └── ...
├── src/            ← the .NET packages
└── ui/             ← the TypeScript packages (same domain, shared vocabulary)
```

For reference, a multi-context repo (signalled by a `CONTEXT-MAP.md` at the root) would instead point at one `CONTEXT.md` per context under `src/<context>/` with context-scoped `docs/adr/`. This repo is not that.

## Use the glossary's vocabulary

When your output names a domain concept (in an issue title, a refactor proposal, a hypothesis, a test name), use the term as defined in `CONTEXT.md`. Don't drift to synonyms the glossary explicitly avoids — e.g. `CONTEXT.md` reserves **Policy** for Motiv's single-value result type, so call the governance concept an **Approval Gate**, not an "approval policy".

If the concept you need isn't in the glossary yet, that's a signal — either you're inventing language the project doesn't use (reconsider) or there's a real gap (note it for `/domain-modeling`).

## Flag ADR conflicts

If your output contradicts an existing ADR, surface it explicitly rather than silently overriding:

> _Contradicts ADR-0001 (the approval gate is a Motiv rule over ChangeRequest) — but worth reopening because…_
