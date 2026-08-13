namespace Motiv.Serialization;

/// <summary>
/// A rule's current state as the store reports it — the <em>head</em>. Never appended: every store
/// derives this by projection from the highest <see cref="StoredRuleVersion.Version"/> in the log, so
/// head and history cannot drift apart.
/// </summary>
/// <param name="Name">The rule name, matching a <see cref="RuleBase.Name"/> registered in the set.</param>
/// <param name="Version">The highest version recorded for the name.</param>
/// <param name="DocumentJson">
/// The document at that version, or null meaning "on the compiled default at this version". Null is a
/// meaningful state and must never collapse to an absent row — a revert records that the rule went
/// back to code, which an absent row could not distinguish from never having been authored.
/// </param>
public sealed record StoredRule(string Name, int Version, string? DocumentJson);

/// <summary>
/// One immutable row of the append-only version log: what was published, by whom, when, and why.
/// The primary key is <c>(Name, Version)</c>, which is also the cross-process compare-and-set — two
/// replicas both computing "next = 6" race on the insert and the key lets exactly one win.
/// </summary>
/// <param name="Name">The rule name.</param>
/// <param name="Version">This row's version. Immutable: the number names this row forever.</param>
/// <param name="DocumentJson">The document published, or null for "reverted to the compiled default".</param>
/// <param name="Author">Who published it.</param>
/// <param name="TimestampUtc">When it was published.</param>
/// <param name="ChangeNote">An optional human-supplied reason.</param>
/// <param name="ApprovalRef">The change request this publish discharged, when governed.</param>
/// <param name="BuildId">
/// The build that was live at publish time. A compiled default cannot be fingerprinted — delegates
/// have no stable hash — so the build id is the only anchor identifying what a null document meant.
/// </param>
public sealed record StoredRuleVersion(
    string Name,
    int Version,
    string? DocumentJson,
    string Author,
    DateTimeOffset TimestampUtc,
    string? ChangeNote,
    string? ApprovalRef,
    string? BuildId);
