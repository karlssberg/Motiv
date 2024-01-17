namespace Karlssberg.Motiv.SpecBuilder;

internal class MetadataBuilder<TModel, TMetadata, TReturnType>(TReturnType returnValue)
{
    internal Func<TModel, TMetadata>? WhenTrue { get; private set; }
    internal Func<TModel, TMetadata>? WhenFalse { get; private set; }
    internal string? CandidateDescription { get; private set; }


    internal TReturnType SetTrueMetadata(Func<TModel, TMetadata> whenTrue)
    {
        WhenTrue = whenTrue.ThrowIfNull(nameof(whenTrue));
        return returnValue;
    }
    
    internal TReturnType SetTrueMetadata(Func< TMetadata> whenTrue)
    { 
        whenTrue.ThrowIfNull(nameof(whenTrue));
        WhenTrue = _ => whenTrue();
        return returnValue;
    }
    
    internal TReturnType SetTrueMetadata(TMetadata whenTrue)
    {
        if (whenTrue is string trueBecause)
            CandidateDescription = trueBecause.ThrowIfNullOrWhitespace(nameof(trueBecause));
        
        WhenTrue = _ => whenTrue;
        
        return returnValue;
    }
    
    internal TReturnType SetFalseMetadata(Func<TModel, TMetadata> whenFalse)
    {
        WhenFalse = whenFalse.ThrowIfNull(nameof(whenFalse));
        return returnValue;
    }
    
    internal TReturnType SetFalseMetadata(Func<TMetadata> whenFalse)
    {
        whenFalse.ThrowIfNull(nameof(whenFalse));
        WhenFalse = _ => whenFalse();
        return returnValue;
    }
    
    internal TReturnType SetFalseMetadata(TMetadata whenFalse)
    {
        WhenFalse = _ => whenFalse;
        return returnValue;
    }
}