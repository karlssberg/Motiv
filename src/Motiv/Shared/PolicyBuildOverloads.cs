using Motiv.Generator.Attributes;

namespace Motiv.Shared;

internal static class PolicyBuildOverloads
{

    [FluentMethodTemplate]
    public static PolicyBase<TModel, TMetadata> Build<TModel, TMetadata>(PolicyBase<TModel, TMetadata> policy) =>
        policy.ThrowIfNull(nameof(policy));

    [FluentMethodTemplate]
    public static PolicyBase<TModel, TMetadata> Build<TModel, TMetadata>(Func<PolicyBase<TModel, TMetadata>> policyFactory) =>
        policyFactory.ThrowIfNull(nameof(policyFactory)).Invoke();

    [FluentMethodTemplate]
    public static PolicyBase<TModel, string> Build<TModel>(PolicyBase<TModel, string> policy) =>
        policy.ThrowIfNull(nameof(policy));

    [FluentMethodTemplate]
    public static PolicyBase<TModel, string> Build<TModel>(Func<PolicyBase<TModel, string>> policyFactory) =>
        policyFactory.ThrowIfNull(nameof(policyFactory)).Invoke();
}
