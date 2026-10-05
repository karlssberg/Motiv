using Converj.Attributes;

namespace Motiv.Shared;

internal static class PolicyBuildOverloads
{

    [FluentMethodTemplate]
    public static PolicyBase<TModel, TMetadata> Build<TModel, TMetadata>(PolicyBase<TModel, TMetadata> policy) =>
        policy.ThrowIfNull();

    [FluentMethodTemplate]
    public static PolicyBase<TModel, TMetadata> Build<TModel, TMetadata>(Func<PolicyBase<TModel, TMetadata>> policyFactory) =>
        policyFactory.ThrowIfNull().Invoke();
}
