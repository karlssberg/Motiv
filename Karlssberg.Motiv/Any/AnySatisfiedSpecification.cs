﻿using static Karlssberg.Motiv.SpecificationException;

namespace Karlssberg.Motiv.Any;

internal sealed class AnySatisfiedSpecification<TModel, TMetadata>(
    SpecificationBase<TModel, TMetadata> underlyingSpecification,
    Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> metadataFactory)
    : SpecificationBase<IEnumerable<TModel>, TMetadata>
{
    internal AnySatisfiedSpecification(SpecificationBase<TModel, TMetadata> specification)
        : this(
            specification,
            (_, _) => Enumerable.Empty<TMetadata>())
    {
    }

    internal AnySatisfiedSpecification(
        SpecificationBase<TModel, TMetadata> underlyingSpecification,
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenAnyTrue)
        : this(underlyingSpecification, CreateMetadataFactory(whenAnyTrue))
    {
    }

    internal AnySatisfiedSpecification(
        SpecificationBase<TModel, TMetadata> underlyingSpecification,
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenAnyTrue,
        Func<IEnumerable<TModel>, TMetadata> whenAllFalse)
        : this(underlyingSpecification, CreateMetadataFactory(whenAnyTrue, whenAllFalse))
    {
    }

    internal AnySatisfiedSpecification(
        SpecificationBase<TModel, TMetadata> underlyingSpecification,
        Func<IEnumerable<TModel>, TMetadata> whenAllTrue,
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenSomeTrue,
        Func<IEnumerable<TModel>, TMetadata> whenAllFalse)
        : this(underlyingSpecification, CreateMetadataFactory(whenAllTrue, whenSomeTrue, whenAllFalse))
    {
    }

    public override string Description => $"ANY({underlyingSpecification})";

    public override BooleanResultBase<TMetadata> IsSatisfiedBy(IEnumerable<TModel> models)
    {
        var resultsWithModel = models
            .Select(model =>
                WrapException.IfIsSatisfiedByInvocationFails(this, underlyingSpecification,
                    () =>
                    {
                        var underlyingResult = underlyingSpecification.IsSatisfiedBy(model);
                        return new BooleanResultWithModel<TModel, TMetadata>(model, underlyingResult);
                    }))
            .ToList();

        return new AnySatisfiedBooleanResult<TMetadata>(
            isSatisfied => metadataFactory(isSatisfied, resultsWithModel), 
            resultsWithModel.Select(result => result.UnderlyingResult));
    }

    private static Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> CreateMetadataFactory(
        Func<IEnumerable<TModel>, TMetadata> whenAllTrue,
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenAnyTrue,
        Func<IEnumerable<TModel>, TMetadata> whenAllFalse)
    {
        return (isSatisfied, results) =>
        {
            if (!isSatisfied)
                return [whenAllFalse(results.Select(result => result.Model))];

            var resultsList = results.ToList();
            return resultsList.All(result => result.IsSatisfied)
                ? [whenAllTrue(resultsList.Select(result => result.Model))]
                : resultsList.Where(result => result.IsSatisfied).Select(whenAnyTrue);
        };
    }

    private static Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> CreateMetadataFactory(
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenAnyTrue,
        Func<IEnumerable<TModel>, TMetadata> whenAllFalse)
    {
        return (isSatisfied, results) => isSatisfied
            ? results
                .Where(result => result.IsSatisfied)
                .Select(whenAnyTrue)
            : [whenAllFalse(results.Select(result => result.Model))];
    }

    private static Func<bool, IEnumerable<BooleanResultWithModel<TModel, TMetadata>>, IEnumerable<TMetadata>> CreateMetadataFactory(
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenAnyTrue)
    {
        return (isSatisfied, results) => isSatisfied
            ? results
                .Where(result => result.IsSatisfied)
                .Select(whenAnyTrue)
            : results.SelectMany(result => result.GetInsights());
    }
}