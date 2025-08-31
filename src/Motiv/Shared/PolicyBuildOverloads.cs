using Motiv.FluentFactory.Generator;

namespace Motiv.Shared;

internal static class PolicyBuildOverloads
{

    [FluentMethodTemplate]
    public static PolicyBase<TModel, TMetadata> Build<TModel, TMetadata>(PolicyBase<TModel, TMetadata> policy) =>
        policy.ThrowIfNull(nameof(policy));

    [FluentMethodTemplate]
    public static PolicyBase<TModel, TMetadata> Build<TModel, TMetadata>(Func<PolicyBase<TModel, TMetadata>> policyFactory) =>
        policyFactory.ThrowIfNull(nameof(policyFactory)).Invoke();
}
