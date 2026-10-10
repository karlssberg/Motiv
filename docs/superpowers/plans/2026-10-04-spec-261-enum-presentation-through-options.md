# Enum presentation through the model's JSON options — Plan

**Source spec:** issue #261.
**Design:** `docs/superpowers/specs/2026-10-04-spec-261-enum-presentation-through-options-design.md`

**Goal:** an enum string-converted only through `JsonSerializerOptions.Converters`, with or without a
naming policy, checks, compiles and evaluates the same on the server and in the editor.

## Steps

1. **Red: the leaf.** `LeafEnumPresentationTests` checks, compiles and evaluates leaves through
   `RuleSerializer` with `RuleSerializerOptions.ModelJsonOptions`. The property was added empty
   first, so the six behavioural cases failed on behaviour, not compilation.
2. **Green: `LeafEnumNames`.** Read the written names by serializing each value. `LeafScope` becomes
   options-aware (`For(…, modelJson)`, instance `EnumNames`/`LeafMemberType`, cached per member).
   `LeafType` carries the names instead of the enum type, and `LeafAnalysis` carries its scope for
   the compiler. The parser stamps the options on each expression node.
3. **Readable clauses.** Assert the decomposed assertion for both CLR-name and policy-name enums.
   The first draft printed `Motiv.Serialization.Expressions.LeafEnumNames.NameOf(model.Priority)`,
   which the `ToJsonName()` extension replaces.
4. **Red: the endpoints.** `ExpressionEndpointTests` mounts an unattributed enum model with default
   `MotivRulesOptions`. The test was confirmed red against the unresolved options.
5. **Green: `ResolvedSerializerOptions`.** Used by the endpoints' serializer and the DI-built
   proposition and rule sets.
6. **Corpus.** Add a `shipment` scope on both sides (`CorpusFixtures.Shipment` with `ShipmentJson`,
   `fixture-schema.ts`'s `SHIPMENT`), with four cases.
7. **Review.** The code-simplifier review found that the first draft re-serialized undefined values
   at evaluation time, which throws under `allowIntegerValues: false`. A red test pinned that, and
   the fix falls back to `ToString()`. The review also led to `MemberwiseClone` for the options copy
   and to a test for member-attribute precedence.
8. **Docs.** `docs/live-rules/expressions.md`'s enum paragraph and the explicit-`RuleSet` remark on
   `MapMotivRules`.

## Verification (2026-10-04, Linux cloud container, SDK 10.0.112 from Ubuntu apt)

The `ns20asset` alias is rejected by this SDK, so the multi-targeting test projects were narrowed to
`net10.0` locally (not committed). The `net8.0`, `net9.0`, `net472` and `ns20asset` legs run in CI only.
