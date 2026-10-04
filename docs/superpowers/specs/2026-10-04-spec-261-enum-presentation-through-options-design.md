# Enum presentation through the model's JSON options — Design

**Date:** 2026-10-04
**Status:** Implemented
**Source spec:** issue #261, *Expression leaves — an enum whose JsonStringEnumConverter is registered
on JsonSerializerOptions, not as an attribute, types as a number while the catalog publishes it as a
string*. A `wayfinder:task` raised by the cleanup review of #249, not a bundle spec, so its body is
the source spec.
**Plan:** `docs/superpowers/plans/2026-10-04-spec-261-enum-presentation-through-options.md`

## Problem

`LeafScope` decided whether an enum member reads as a `string` by looking for a
`[JsonConverter(typeof(JsonStringEnumConverter))]` attribute. The catalog exports model schemas
over `MotivRulesOptions.JsonSerializerOptions`, which registers a `JsonStringEnumConverter` by
default. So for an unattributed enum the catalog published `{ "enum": ["Retail", "Wholesale"] }`,
Studio's checker accepted `kind == "Retail"`, and the server refused it with `comparing int with
string`. A naming policy on the converter widened the gap: the leaf accepted only the CLR names.

## Decisions

1. **Ask the serializer, do not recognise converters.** `LeafEnumNames.For` serializes each value
   of the enum with the options and keeps the result when every value is written as a JSON string.
   That counts a registered converter, an attribute, a naming policy and a per-value name override
   exactly as `JsonSchemaExporter` counts them over the same options. Inspecting the resolved
   converter was rejected: `JsonStringEnumConverter` resolves to an internal `EnumConverter<T>`
   for both string and numeric modes, so the mode is not observable without private reflection.
   The same code runs on `netstandard2.0`, so the structural converter-name match the old code
   needed there is gone.
2. **A member attribute outranks the options.** System.Text.Json's precedence is member attribute,
   then registered converters, then type attribute. The member's converter is inserted first into
   a copy of the options, which reproduces that order.
3. **`RuleSerializerOptions.ModelJsonOptions`, carried on the expression node.** The parser stamps
   it on each expression `RuleNode`, as the substituter already does for parameters. Threading it
   through `RuleBinder`'s static entry points would have touched every binder and `CollectionBinding`
   for one value only the leaf reads. `null` means `JsonSerializerOptions.Default`, where only an
   attribute names an enum. That is the old behaviour.
4. **The endpoints default it to the catalog's options.** `MotivRulesOptions.ResolvedSerializerOptions`
   copies `SerializerOptions` with `ModelJsonOptions = JsonSerializerOptions` unless the host set it.
   It feeds the endpoints' serializer and the DI-built `PropositionSet` and `RuleSet`, so validate,
   evaluate and rule updates agree. The host's instance is never modified.
5. **The compiled clause stays readable.** When every written name is the CLR name, the compiler
   still emits `model.Kind.ToString()`, so existing assertions are unchanged. Otherwise it emits
   `model.Priority.ToJsonName()`, an extension whose `LeafEnumNames` argument prints as empty, so the
   clause does not leak an internal type name.

## Consequences

- An enum value with no defined name (a flags combination, an out-of-range number) reads as its
  `ToString()`. It can equal none of the names, which are the only literals the checker accepts, so
  the comparison's outcome matches the JSON. Evaluation never serializes, so it cannot throw: a
  converter built with `allowIntegerValues: false` would throw on such a value.
- Serializing with the options makes them read-only, as System.Text.Json does on first use.
- Member *names* (`FindMember`) still resolve by attribute, camel case, then CLR name, not through
  the options' property naming policy. That is a separate gap and out of scope here.

## Verification

Corpus cases on a `shipment` scope pin both checkers: an enum named only by a registered converter,
and one under a camel-case policy. `LeafEnumPresentationTests` covers checking, compiling and
evaluating through `RuleSerializer`. `ExpressionEndpointTests` covers the endpoint default.
