using System.Text.Json;

namespace Motiv.Serialization.AspNetCore;

/// <summary>A listing of one proposition in scope, compiled or authored.</summary>
/// <param name="Name">The dot-separated name.</param>
/// <param name="ModelType">The registered model-type id, or the CLR type name when not registered.</param>
/// <param name="MetadataType">The metadata type name (e.g. String).</param>
/// <param name="IsAsync">Whether the effective definition evaluates asynchronously.</param>
/// <param name="Origin">Compiled, Overridden, or Authored.</param>
/// <param name="Version">The authored document's version, or 0 for a purely compiled proposition.</param>
/// <param name="Description">An optional human-readable description.</param>
/// <param name="Quarantine">
/// Binding errors that excluded an authored document from the effective set; empty when it bound.
/// Orthogonal to <paramref name="Origin"/> — an overridden or authored proposition can be quarantined.
/// </param>
public sealed record PropositionListEntry(
    string Name, string ModelType, string MetadataType, bool IsAsync,
    string Origin, int Version, string? Description, IReadOnlyList<RuleError> Quarantine);

/// <summary>One proposition's authored document and version.</summary>
/// <param name="Document">The authored document, or null when the name is served by a compiled spec.</param>
/// <param name="Version">The version; pass it back as <c>baseVersion</c> when updating. 0 when compiled.</param>
/// <param name="Origin">Compiled, Overridden, or Authored.</param>
/// <param name="HasCompiledDefault">Whether a compiled spec lies beneath the name. When
/// <paramref name="Origin"/> is <c>Overridden</c> this means DELETE reverts rather than removes;
/// when it is <c>Compiled</c> there is nothing to delete and DELETE answers 404.</param>
public sealed record PropositionGetResponse(
    JsonElement? Document, int Version, string Origin, bool HasCompiledDefault);

/// <summary>A request to author a new proposition.</summary>
/// <param name="Name">The dot-separated name. A name already carrying an authored document conflicts.</param>
/// <param name="ModelType">A model-type id registered on the server.</param>
/// <param name="Document">The rule document defining the proposition.</param>
/// <param name="Description">An optional description.</param>
public sealed record PropositionCreateRequest(
    string Name, string ModelType, JsonElement Document, string? Description);

/// <summary>A request to replace an authored proposition's document.</summary>
/// <param name="Document">The replacement document.</param>
/// <param name="BaseVersion">The version the caller last observed; a stale value yields 409.</param>
public sealed record PropositionPutRequest(JsonElement Document, int BaseVersion);

/// <summary>A successful create, update, or withdrawal.</summary>
/// <param name="Version">The new version, or 0 after a withdrawal.</param>
public sealed record PropositionSaveResponse(int Version);

/// <summary>
/// A rejected write. <paramref name="Errors"/> holds faults in the submitted document itself;
/// <paramref name="BrokenDependents"/> holds the dependents the edit would have stopped binding.
/// The two are separate because a <see cref="RuleError"/>'s path points into *this* document and
/// cannot address a break somewhere else.
/// </summary>
public sealed record CascadeFailureResponse(
    IReadOnlyList<RuleError> Errors, IReadOnlyList<BrokenDependent> BrokenDependents);

/// <summary>A refused removal: the proposition is still referenced.</summary>
/// <param name="Referrers">The names that must stop referencing it first.</param>
public sealed record PropositionReferencedResponse(IReadOnlyList<string> Referrers);

/// <summary>One node that would be rebound by editing a proposition.</summary>
/// <param name="Name">The dependent's name.</param>
/// <param name="Kind">Either <c>rule</c> or <c>proposition</c>.</param>
public sealed record DependentEntry(string Name, string Kind);

/// <summary>The transitive blast radius of editing a proposition, in rebind order.</summary>
/// <param name="Dependents">Every affected rule and proposition.</param>
public sealed record DependentsResponse(IReadOnlyList<DependentEntry> Dependents);
