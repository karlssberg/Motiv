﻿namespace Karlssberg.Motiv.And;

internal sealed class AndSpec<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> left,
    SpecBase<TModel, TMetadata> right) 
    : SpecBase<TModel, TMetadata>, IBinaryOperationSpec
{
    public override ISpecDescription Description =>
        new AndSpecDescription<TModel, TMetadata>(left, right);

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model)
    {
        var leftResult = left.IsSatisfiedBy(model);
        var rightResult = right.IsSatisfiedBy(model);

        return leftResult.And(rightResult);
    }
}