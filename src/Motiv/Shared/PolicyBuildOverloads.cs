using Motiv.Generator.Attributes;

namespace Motiv.Shared;

internal static class PolicyBuildOverloads
{

    [FluentMethodTemplate]
    public static PolicyBase<TModel, TMetadata> Build<TModel, TMetadata>(PolicyBase<TModel, TMetadata> policy) => policy;

    [FluentMethodTemplate]
    public static PolicyBase<TModel, TMetadata> Build<TModel, TMetadata>(Func<PolicyBase<TModel, TMetadata>> policyFactory) => policyFactory();

    [FluentMethodTemplate]
    public static PolicyBase<TModel, string> Build<TModel>(PolicyBase<TModel, string> policy) => policy;

    [FluentMethodTemplate]
    public static PolicyBase<TModel, string> Build<TModel>(Func<PolicyBase<TModel, string>> policyFactory) => policyFactory();
}
