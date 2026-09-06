namespace Motiv.Tap;

/// <summary>A <see cref="TapSpecBase{TModel,TMetadata}" /> whose callback fires on every evaluation.</summary>
internal sealed class TapSpec<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> operand,
    Action<TModel, BooleanResultBase<TMetadata>> callback)
    : TapSpecBase<TModel, TMetadata>(operand, callback)
{
    protected override bool ShouldInvokeCallback(BooleanResultBase<TMetadata> result) => true;
}
