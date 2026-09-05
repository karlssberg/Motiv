namespace Motiv.Tap;

/// <summary>A <see cref="TapSpecBase{TModel,TMetadata}" /> whose callback fires only when unsatisfied.</summary>
internal sealed class TapWhenFalseSpec<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> operand,
    Action<TModel, BooleanResultBase<TMetadata>> callback)
    : TapSpecBase<TModel, TMetadata>(operand, callback)
{
    protected override bool ShouldInvokeCallback(BooleanResultBase<TMetadata> result) => !result.Satisfied;
}
