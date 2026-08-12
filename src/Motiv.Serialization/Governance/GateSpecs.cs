namespace Motiv.Serialization;

/// <summary>
/// The built-in <c>change.*</c> gate catalogue: reusable governance predicates over a
/// <see cref="ChangeRequest"/>, registered under stable names so approval-gate rule documents can
/// compose them with logical operators (e.g. maker-checker as
/// <c>approver-count-at-least(1) &amp; !author-is-approver</c>) without any C# recompilation.
/// </summary>
/// <remarks>
/// Every registration here is an unnamed explanation proposition — <c>.WhenTrue(...).WhenFalse(...).Create()</c>
/// with no name — so the WhenTrue/WhenFalse strings themselves become the assertions. A gate's
/// refusal <c>Justification</c> therefore reads as prose rather than as a bare <c>== true</c> /
/// <c>== false</c> suffix, which is the point: the reasoning a rejected change request shows back
/// to its author is meant to be read, not decoded.
/// </remarks>
public static class GateSpecs
{
    /// <summary>Builds a registry populated with the ten built-in <c>change.*</c> gate specs.</summary>
    /// <returns>A new <see cref="SpecRegistry"/> containing the gate catalogue.</returns>
    public static SpecRegistry CreateRegistry()
    {
        var registry = new SpecRegistry();

        registry.RegisterParameterised(
            "change.in-namespace",
            [new RuleParameterDeclaration("prefix", RuleParameterType.String, false, null)],
            values =>
            {
                var prefix = (string)values["prefix"]!;
                return Spec.Build((ChangeRequest c) =>
                        c.ProposedChanges.Any(p => NamespacePrefix.Covers(prefix, p.Target.Name)))
                    .WhenTrue($"change touches namespace '{prefix}'")
                    .WhenFalse($"change does not touch namespace '{prefix}'")
                    .Create();
            },
            "Whether any proposed change's target falls under the given namespace prefix");

        registry.Register(
            "change.target-is-proposition",
            Spec.Build((ChangeRequest c) =>
                    c.ProposedChanges.Any(p => p.Target.Kind == ChangeTargetKind.Proposition))
                .WhenTrue("change targets a proposition")
                .WhenFalse("change targets no proposition")
                .Create(),
            "Whether any proposed change targets a proposition rather than a composed rule");

        registry.RegisterParameterised(
            "change.approver-count-at-least",
            [new RuleParameterDeclaration("n", RuleParameterType.Integer, false, null)],
            values =>
            {
                var n = (int)values["n"]!;
                return Spec.Build((ChangeRequest c) => c.Approvals.Count >= n)
                    .WhenTrue($"change has at least {n} approvals")
                    .WhenFalse($"change has fewer than {n} approvals")
                    .Create();
            },
            "Whether the change request has accumulated at least n approvals");

        registry.Register(
            "change.author-is-approver",
            Spec.Build((ChangeRequest c) => c.Approvals.Any(a => a.Approver == c.Author))
                .WhenTrue("the author approved their own change")
                .WhenFalse("no self-approval")
                .Create(),
            "Whether the change request's author is among the recorded approvers");

        registry.RegisterParameterised(
            "change.approver-has-role",
            [new RuleParameterDeclaration("role", RuleParameterType.String, false, null)],
            values =>
            {
                var role = (string)values["role"]!;
                return Spec.Build((ChangeRequest c) => c.Approvals.Any(a => a.Roles.Contains(role)))
                    .WhenTrue($"an approver holds role '{role}'")
                    .WhenFalse($"no approver holds role '{role}'")
                    .Create();
            },
            "Whether any approver held the given role at the time they approved");

        registry.Register(
            "change.is-rollback",
            Spec.Build((ChangeRequest c) => c.ProposedChanges.Any(p => p.Classification.IsRollback))
                .WhenTrue("change is a rollback")
                .WhenFalse("change is not a rollback")
                .Create(),
            "Whether any proposed change is classified as a rollback");

        registry.Register(
            "change.is-creation",
            Spec.Build((ChangeRequest c) => c.ProposedChanges.Any(p => p.Classification.IsCreation))
                .WhenTrue("change creates an artefact")
                .WhenFalse("change creates nothing")
                .Create(),
            "Whether any proposed change creates an artefact that did not previously exist");

        registry.Register(
            "change.is-deletion",
            Spec.Build((ChangeRequest c) => c.ProposedChanges.Any(p => p.Classification.IsDeletion))
                .WhenTrue("change deletes an artefact")
                .WhenFalse("change deletes nothing")
                .Create(),
            "Whether any proposed change deletes an artefact");

        registry.Register(
            "change.is-metadata-only",
            Spec.Build((ChangeRequest c) => c.ProposedChanges.All(p => p.Classification.IsMetadataOnly))
                .WhenTrue("change is metadata-only")
                .WhenFalse("change alters logic")
                .Create(),
            "Whether every proposed change alters only non-behavioral metadata");

        registry.Register(
            "change.touches-async-spec",
            Spec.Build((ChangeRequest c) => c.ProposedChanges.Any(p => p.Classification.TouchesAsyncSpec))
                .WhenTrue("change touches an async spec")
                .WhenFalse("change touches no async spec")
                .Create(),
            "Whether any proposed change affects an asynchronously-evaluated specification");

        return registry;
    }
}
