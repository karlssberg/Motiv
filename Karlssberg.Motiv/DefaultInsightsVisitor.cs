using Karlssberg.Motiv.All;
using Karlssberg.Motiv.And;
using Karlssberg.Motiv.Any;
using Karlssberg.Motiv.AtLeast;
using Karlssberg.Motiv.AtMost;
using Karlssberg.Motiv.ChangeMetadataType;
using Karlssberg.Motiv.Not;
using Karlssberg.Motiv.Or;
using Karlssberg.Motiv.SubstituteMetadata;
using Karlssberg.Motiv.XOr;

namespace Karlssberg.Motiv;

/// <summary>
/// Represents a default implementation of the insights visitor for a specific metadata type.
/// </summary>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
public class DefaultInsightsVisitor<TMetadata>
{
    /// <summary>
    /// Visits a collection of <see cref="BooleanResultBase{TMetadata}"/> objects and returns a collection of <typeparamref name="TMetadata"/>.
    /// </summary>
    /// <param name="booleanResultBases">The collection of <see cref="BooleanResultBase{TMetadata}"/> objects to visit.</param>
    /// <returns>A collection of <typeparamref name="TMetadata"/>.</returns>
    protected IEnumerable<TMetadata> Visit(IEnumerable<BooleanResultBase<TMetadata>> booleanResultBases) =>
        booleanResultBases.SelectMany(Visit);

    /// <summary>
    /// Visits a <see cref="BooleanResultBase{TMetadata}"/> and returns a collection of <typeparamref name="TMetadata"/>.
    /// </summary>
    /// <param name="booleanResultBase">The boolean result to visit.</param>
    /// <returns>A collection of <typeparamref name="TMetadata"/>.</returns>
    public IEnumerable<TMetadata> Visit(BooleanResultBase<TMetadata> booleanResultBase)
    {
        return booleanResultBase switch
        {
            AllSatisfiedBooleanResult<TMetadata> allBooleanResult => Visit(allBooleanResult),
            AndBooleanResult<TMetadata> andBooleanResult => Visit(andBooleanResult),
            AnySatisfiedBooleanResult<TMetadata> anyBooleanResult => Visit(anyBooleanResult),
            AtLeastNSatisfiedBooleanResult<TMetadata> atLeastBooleanResult => Visit(atLeastBooleanResult),
            AtMostNSatisfiedBooleanResult<TMetadata> atMostBooleanResult => Visit(atMostBooleanResult),
            BooleanResult<TMetadata> booleanResult => Visit(booleanResult),
            IChangeMetadataTypeBooleanResult<TMetadata> changeMetadataTypeBooleanResult => Visit(changeMetadataTypeBooleanResult),
            NotBooleanResult<TMetadata> notBooleanResult => Visit(notBooleanResult),
            OrBooleanResult<TMetadata> orBooleanResult => Visit(orBooleanResult),
            SubstituteMetadataBooleanResult<TMetadata> substituteMetadataBooleanResult => Visit(substituteMetadataBooleanResult),
            XOrBooleanResult<TMetadata> xOrBooleanResult => Visit(xOrBooleanResult),
            _ => Enumerable.Empty<TMetadata>()
        };
    }

    /// <summary>
    /// Visits an <see cref="AllSatisfiedBooleanResult{TMetadata}"/> and returns a collection of metadata.
    /// </summary>
    /// <param name="allSatisfiedBooleanResult">The <see cref="AllSatisfiedBooleanResult{TMetadata}"/> to visit.</param>
    /// <returns>A collection of metadata.</returns>
    protected virtual IEnumerable<TMetadata> Visit(AllSatisfiedBooleanResult<TMetadata> allSatisfiedBooleanResult) =>
        allSatisfiedBooleanResult.SubstituteMetadata.IfEmptyThen(Visit(allSatisfiedBooleanResult.DeterminativeOperandResults));

    /// <summary>
    /// Visits an <see cref="AndBooleanResult{TMetadata}"/> and returns a collection of <typeparamref name="TMetadata"/>.
    /// </summary>
    /// <param name="andBooleanResult">The <see cref="AndBooleanResult{TMetadata}"/> to visit.</param>
    /// <returns>A collection of <typeparamref name="TMetadata"/>.</returns>
    protected virtual IEnumerable<TMetadata> Visit(AndBooleanResult<TMetadata> andBooleanResult) =>
        Visit(andBooleanResult.DeterminativeOperandResults);

    /// <summary>
    /// Visits an instance of <see cref="AnySatisfiedBooleanResult{TMetadata}"/> and returns a collection of <typeparamref name="TMetadata"/>.
    /// </summary>
    /// <param name="anySatisfiedBooleanResult">The instance of <see cref="AnySatisfiedBooleanResult{TMetadata}"/> to visit.</param>
    /// <returns>A collection of <typeparamref name="TMetadata"/>.</returns>
    protected virtual IEnumerable<TMetadata> Visit(AnySatisfiedBooleanResult<TMetadata> anySatisfiedBooleanResult) =>
        anySatisfiedBooleanResult.SubstituteMetadata.IfEmptyThen(Visit(anySatisfiedBooleanResult.DeterminativeOperandResults));

    /// <summary>
    /// Visits an <see cref="AtLeastNSatisfiedBooleanResult{TMetadata}"/> and returns a collection of <typeparamref name="TMetadata"/>.
    /// </summary>
    /// <param name="atLeastNSatisfiedBooleanResult">The <see cref="AtLeastNSatisfiedBooleanResult{TMetadata}"/> to visit.</param>
    /// <returns>A collection of <typeparamref name="TMetadata"/>.</returns>
    protected virtual IEnumerable<TMetadata> Visit(AtLeastNSatisfiedBooleanResult<TMetadata> atLeastNSatisfiedBooleanResult) =>
        atLeastNSatisfiedBooleanResult.SubstituteMetadata
            .IfEmptyThen(Visit(atLeastNSatisfiedBooleanResult.DeterminativeOperandResults));

    /// <summary>
    /// Visits an <see cref="AtMostNSatisfiedBooleanResult{TMetadata}"/> and returns a collection of <typeparamref name="TMetadata"/>.
    /// </summary>
    /// <param name="atMostNSatisfiedBooleanResult">The <see cref="AtMostNSatisfiedBooleanResult{TMetadata}"/> to visit.</param>
    /// <returns>A collection of <typeparamref name="TMetadata"/>.</returns>
    protected virtual IEnumerable<TMetadata> Visit(AtMostNSatisfiedBooleanResult<TMetadata> atMostNSatisfiedBooleanResult) =>
        atMostNSatisfiedBooleanResult.SubstituteMetadata
            .IfEmptyThen(Visit(atMostNSatisfiedBooleanResult.DeterminativeOperandResults));

    /// <summary>
    /// Visits a BooleanResult and returns a collection of metadata.
    /// </summary>
    /// <param name="booleanResult">The BooleanResult to visit.</param>
    /// <returns>A collection of metadata.</returns>
    protected virtual IEnumerable<TMetadata> Visit(BooleanResult<TMetadata> booleanResult) =>
        [booleanResult.Metadata];

    protected virtual IEnumerable<TMetadata> Visit(IChangeMetadataTypeBooleanResult<TMetadata> changeMetadataTypeBooleanResult) =>
        [changeMetadataTypeBooleanResult.Metadata];

    /// <summary>
    /// Visits a <see cref="NotBooleanResult{TMetadata}"/> and returns the visited metadata.
    /// </summary>
    /// <param name="notBooleanResult">The <see cref="NotBooleanResult{TMetadata}"/> to visit.</param>
    /// <returns>The visited metadata.</returns>
    protected virtual IEnumerable<TMetadata> Visit(NotBooleanResult<TMetadata> notBooleanResult) =>
        Visit(notBooleanResult.OperandResult);

    /// <summary>
    /// Visits an <see cref="OrBooleanResult{TMetadata}"/> and returns a collection of <typeparamref name="TMetadata"/>.
    /// </summary>
    /// <param name="orBooleanResult">The <see cref="OrBooleanResult{TMetadata}"/> to visit.</param>
    /// <returns>A collection of <typeparamref name="TMetadata"/>.</returns>
    protected virtual IEnumerable<TMetadata> Visit(OrBooleanResult<TMetadata> orBooleanResult) =>
        Visit(orBooleanResult.DeterminativeOperandResults);

    /// <summary>
    /// Visits a <see cref="SubstituteMetadataBooleanResult{TMetadata}"/> and returns a collection of <typeparamref name="TMetadata"/>.
    /// </summary>
    /// <param name="substituteMetadataBooleanResult">The <see cref="SubstituteMetadataBooleanResult{TMetadata}"/> to visit.</param>
    /// <returns>A collection of <typeparamref name="TMetadata"/>.</returns>
    protected virtual IEnumerable<TMetadata> Visit(SubstituteMetadataBooleanResult<TMetadata> substituteMetadataBooleanResult) =>
        [substituteMetadataBooleanResult.SubstituteMetadata];

    /// <summary>
    /// Visits an XOrBooleanResult and returns a collection of metadata.
    /// </summary>
    /// <param name="xOrBooleanResult">The XOrBooleanResult to visit.</param>
    /// <returns>A collection of metadata.</returns>
    protected virtual IEnumerable<TMetadata> Visit(XOrBooleanResult<TMetadata> xOrBooleanResult) =>
        Visit(xOrBooleanResult.DeterminativeOperandResults);
}