using Motiv.Serialization;

namespace Motiv.RuleAuthoring.Blazor.Authoring;

/// <summary>A validation error, paired with the draft node it was reported against.</summary>
/// <param name="Error">The error as Motiv.Serialization reported it.</param>
/// <param name="Node">The draft node the error's path resolves to, or <c>null</c> if it names no node.</param>
public sealed record LocatedError(RuleError Error, DraftNode? Node);
