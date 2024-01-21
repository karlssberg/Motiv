using Karlssberg.Motiv.All;
using Karlssberg.Motiv.And;
using Karlssberg.Motiv.Any;
using Karlssberg.Motiv.AtLeast;
using Karlssberg.Motiv.AtMost;
using Karlssberg.Motiv.ChangeMetadata;
using Karlssberg.Motiv.Not;
using Karlssberg.Motiv.Or;
using Karlssberg.Motiv.XOr;

namespace Karlssberg.Motiv;

/// <summary>Represents a default implementation of the insights visitor for a specific metadata type.</summary>
/// <typeparam name="TMetadata">The type of the metadata.</typeparam>
public class DefaultMetadataVisitor<TMetadata>
{
    /// <summary>
    ///     Visits a collection of <see cref="BooleanResultBase{TMetadata}" /> objects and returns a collection of
    ///     <typeparamref name="TMetadata" />.
    /// </summary>
    /// <param name="booleanResultBases">The collection of <see cref="BooleanResultBase{TMetadata}" /> objects to visit.</param>
    /// <returns>A collection of <typeparamref name="TMetadata" />.</returns>
    public IEnumerable<TMetadata> Visit(IEnumerable<BooleanResultBase<TMetadata>> booleanResultBases) =>
        booleanResultBases.SelectMany(Visit);

    /// <summary>
    ///     Visits a <see cref="BooleanResultBase{TMetadata}" /> and returns a collection of
    ///     <typeparamref name="TMetadata" />.
    /// </summary>
    /// <param name="booleanResultBase">The boolean result to visit.</param>
    /// <returns>A collection of <typeparamref name="TMetadata" />.</returns>
    public IEnumerable<TMetadata> Visit(BooleanResultBase<TMetadata> booleanResultBase)
    {
        return booleanResultBase switch
        {
            IAllSatisfiedBooleanResult<TMetadata> allBooleanResult => Visit(allBooleanResult),
            AndBooleanResult<TMetadata> andBooleanResult => Visit(andBooleanResult),
            IAnySatisfiedBooleanResult<TMetadata> anyBooleanResult => Visit(anyBooleanResult),
            IAtLeastNSatisfiedBooleanResult<TMetadata> atLeastBooleanResult => Visit(atLeastBooleanResult),
            AtMostNSatisfiedBooleanResult<TMetadata> atMostBooleanResult => Visit(atMostBooleanResult),
            BooleanResult<TMetadata> booleanResult => Visit(booleanResult),
            IChangeMetadataBooleanResult<TMetadata> changeMetadataTypeBooleanResult => Visit(changeMetadataTypeBooleanResult),
            NotBooleanResult<TMetadata> notBooleanResult => Visit(notBooleanResult),
            OrBooleanResult<TMetadata> orBooleanResult => Visit(orBooleanResult),
            XOrBooleanResult<TMetadata> xOrBooleanResult => Visit(xOrBooleanResult),
            IBinaryBooleanResult<TMetadata> compositeBooleanResult => Visit(compositeBooleanResult),
            ICompositeBooleanResult<TMetadata> compositeBooleanResult => Visit(compositeBooleanResult),
            _ => Enumerable.Empty<TMetadata>()
        };
    }

    public virtual IEnumerable<TMetadata> Visit(ICompositeBooleanResult<TMetadata> compositeBooleanResult) =>
        Visit(compositeBooleanResult.DeterminativeResults);

    public virtual IEnumerable<TMetadata> Visit(IBinaryBooleanResult<TMetadata> binaryBooleanResult) =>
        Visit(binaryBooleanResult.DeterminativeResults);

    /// <summary>Visits an <see cref="AllSatisfiedBooleanResult{TMetadata}" /> and returns a collection of metadata.</summary>
    /// <param name="allSatisfiedBooleanResult">The <see cref="AllSatisfiedBooleanResult{TMetadata}" /> to visit.</param>
    /// <returns>A collection of metadata.</returns>
    public virtual IEnumerable<TMetadata> Visit(IAllSatisfiedBooleanResult<TMetadata> allSatisfiedBooleanResult) =>
        allSatisfiedBooleanResult.SubstituteMetadata.IfEmptyThen(Visit(allSatisfiedBooleanResult.DeterminativeResults));

    /// <summary>
    ///     Visits an <see cref="AndBooleanResult{TMetadata}" /> and returns a collection of
    ///     <typeparamref name="TMetadata" />.
    /// </summary>
    /// <param name="andBooleanResult">The <see cref="AndBooleanResult{TMetadata}" /> to visit.</param>
    /// <returns>A collection of <typeparamref name="TMetadata" />.</returns>
    public virtual IEnumerable<TMetadata> Visit(AndBooleanResult<TMetadata> andBooleanResult) =>
        Visit(andBooleanResult.DeterminativeResults);

    /// <summary>
    ///     Visits an instance of <see cref="AnySatisfiedBooleanResult{TMetadata}" /> and returns a collection of
    ///     <typeparamref name="TMetadata" />.
    /// </summary>
    /// <param name="anySatisfiedBooleanResult">The instance of <see cref="AnySatisfiedBooleanResult{TMetadata}" /> to visit.</param>
    /// <returns>A collection of <typeparamref name="TMetadata" />.</returns>
    public virtual IEnumerable<TMetadata> Visit(IAnySatisfiedBooleanResult<TMetadata> anySatisfiedBooleanResult) =>
        anySatisfiedBooleanResult.SubstituteMetadata.IfEmptyThen(Visit(anySatisfiedBooleanResult.DeterminativeResults));

    /// <summary>
    ///     Visits an <see cref="AtLeastNSatisfiedBooleanResult{TMetadata}" /> and returns a collection of
    ///     <typeparamref name="TMetadata" />.
    /// </summary>
    /// <param name="atLeastNSatisfiedBooleanResult">The <see cref="AtLeastNSatisfiedBooleanResult{TMetadata}" /> to visit.</param>
    /// <returns>A collection of <typeparamref name="TMetadata" />.</returns>
    public virtual IEnumerable<TMetadata> Visit(IAtLeastNSatisfiedBooleanResult<TMetadata> atLeastNSatisfiedBooleanResult) =>
        atLeastNSatisfiedBooleanResult.SubstituteMetadata
            .IfEmptyThen(Visit(atLeastNSatisfiedBooleanResult.DeterminativeResults));

    /// <summary>
    ///     Visits an <see cref="AtMostNSatisfiedBooleanResult{TMetadata}" /> and returns a collection of
    ///     <typeparamref name="TMetadata" />.
    /// </summary>
    /// <param name="atMostNSatisfiedBooleanResult">The <see cref="AtMostNSatisfiedBooleanResult{TMetadata}" /> to visit.</param>
    /// <returns>A collection of <typeparamref name="TMetadata" />.</returns>
    public virtual IEnumerable<TMetadata> Visit(AtMostNSatisfiedBooleanResult<TMetadata> atMostNSatisfiedBooleanResult) =>
        atMostNSatisfiedBooleanResult.SubstituteMetadata
            .IfEmptyThen(Visit(atMostNSatisfiedBooleanResult.DeterminativeResults));

    /// <summary>Visits a BooleanResult and returns a collection of metadata.</summary>
    /// <param name="booleanResult">The BooleanResult to visit.</param>
    /// <returns>A collection of metadata.</returns>
    public virtual IEnumerable<TMetadata> Visit(BooleanResult<TMetadata> booleanResult) =>
        [booleanResult.Metadata];

    public virtual IEnumerable<TMetadata> Visit(IChangeMetadataBooleanResult<TMetadata> changeMetadataBooleanResult) =>
        [changeMetadataBooleanResult.Metadata];

    /// <summary>Visits a <see cref="NotBooleanResult{TMetadata}" /> and returns the visited metadata.</summary>
    /// <param name="notBooleanResult">The <see cref="NotBooleanResult{TMetadata}" /> to visit.</param>
    /// <returns>The visited metadata.</returns>
    public virtual IEnumerable<TMetadata> Visit(NotBooleanResult<TMetadata> notBooleanResult) =>
        Visit(notBooleanResult.OperandResult);

    /// <summary>
    ///     Visits an <see cref="OrBooleanResult{TMetadata}" /> and returns a collection of
    ///     <typeparamref name="TMetadata" />.
    /// </summary>
    /// <param name="orBooleanResult">The <see cref="OrBooleanResult{TMetadata}" /> to visit.</param>
    /// <returns>A collection of <typeparamref name="TMetadata" />.</returns>
    public virtual IEnumerable<TMetadata> Visit(OrBooleanResult<TMetadata> orBooleanResult) =>
        Visit(orBooleanResult.DeterminativeResults);

    /// <summary>Visits an XOrBooleanResult and returns a collection of metadata.</summary>
    /// <param name="xOrBooleanResult">The XOrBooleanResult to visit.</param>
    /// <returns>A collection of metadata.</returns>
    public virtual IEnumerable<TMetadata> Visit(XOrBooleanResult<TMetadata> xOrBooleanResult) =>
        Visit(xOrBooleanResult.DeterminativeResults);
}