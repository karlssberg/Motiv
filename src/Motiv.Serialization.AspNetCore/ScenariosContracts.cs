namespace Motiv.Serialization.AspNetCore;

/// <summary>One scenario of a rule as the API lists it. <c>Model</c> is the text as stored, JSON or not.</summary>
public sealed record ScenarioEntry(
    string Id,
    string Name,
    string Model,
    bool? ExpectedSatisfied,
    string? SourceDecisionId,
    int Version,
    string Author,
    DateTimeOffset TimestampUtc);

/// <summary>A create (<c>BaseVersion</c> 0) or replace of one scenario.</summary>
public sealed record ScenarioPutRequest(
    string Name, string Model, bool? ExpectedSatisfied, string? SourceDecisionId, int BaseVersion);

/// <summary>The version a scenario is at after a write.</summary>
public sealed record ScenarioSaveResponse(int Version);

/// <summary>The version a scenario is actually at, when a write named a stale one.</summary>
public sealed record ScenarioConflictResponse(int CurrentVersion);
