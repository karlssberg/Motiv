# Motiv.RuleAuthoring.Blazor

A standalone **Blazor WebAssembly** app that authors a Motiv rule document, validates it, binds it
and evaluates it — entirely in the browser, entirely in C#, over `Motiv.Serialization` and nothing
else. There is no `@motiv-rules/core`, no `@motiv-rules/react`, and no JavaScript of our own.

It is the hosted example for `src/examples/`, and the worked artefact behind the **.NET, including
Blazor** row of [Runtimes and Support Tiers](../../../docs/adoption/index.md).

## Running it

```bash
dotnet run --project src/examples/Motiv.RuleAuthoring.Blazor
```

Then open <http://localhost:5200>.

## What it demonstrates

`Motiv.Serialization`'s document model (`RuleDocument`, `RuleNode`) is `internal`, so a .NET
authoring UI brings its own and emits the JSON itself. That boundary is the interesting part, and
this sample lives inside it rather than around it:

| Layer | Who owns it |
|---|---|
| `DraftNode` — the authoring tree the editor mutates | the sample |
| `RuleDocumentWriter` — draft → JSON, recording each node's `$.rule…` path as it writes | the sample |
| `AuthoringSession` — resolving a `RuleError.Path` back to the node the author is editing | the sample |
| Validation, binding, evaluation, `Reason` and `Justification` | `Motiv.Serialization` |

Two things are worth noticing on the page itself:

- The paths are recorded by the **same walk** that emits the JSON. A separately derived path could
  disagree with the one `Validate` reports, and would do so silently — putting an error beside the
  wrong control.
- The document is *named*, so Motiv's `== true` / `== false` suffix rule makes `Reason` the name
  alone. The causes live in `Justification`, which is why the page shows both.

## Tests

`Motiv.RuleAuthoring.Blazor.Tests` covers the authoring core with xunit and the two components with
[bUnit](https://bunit.dev) — the component tests pin the behaviours that are otherwise only visible
by clicking: the remove control disabled at an operator's minimum, an error rendered against the
node it was located on, and the verdict and justification changing together when the model does.

## The gates

`Motiv.RuleAuthoring.Blazor.Tests` refuses, on every CI run:

- any `ProjectReference` or `PackageReference` beyond `Motiv.Serialization` and the Blazor host,
- any `.js` file anywhere in the project (not just `wwwroot` — a collocated `Author.razor.js` is
  Blazor's own JS-isolation shape and never appears there), and
- any `<script src>` in `index.html` other than the Blazor runtime itself, which adds no file to the
  tree at all and so no file listing can see it.

A C# project cannot accidentally `npm install`, but a sample can quietly acquire a script tag — and
then it stops demonstrating what it exists to demonstrate while every job stays green.
