namespace Karlssberg.Motiv.Proposition.NestedMetadataSpecBuilder;

public readonly struct TrueSpecBuilder<TModel, TUnderlyingMetadata>(
    SpecBase<TModel, TUnderlyingMetadata> spec)
{
    public FalseMetadataSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(TMetadata whenTrue) =>
        new(spec, _ => whenTrue);

    public FalseMetadataSpecBuilder<TModel, TMetadata, TUnderlyingMetadata> WhenTrue<TMetadata>(
        Func<TModel, TMetadata> whenTrue) =>
        new(spec, whenTrue);

    public FalseReasonsWithDescriptionSpecBuilder<TModel, TUnderlyingMetadata> WhenTrue(string trueBecause) =>
        new(spec, _ => trueBecause, trueBecause);

    public FalseReasonsSpecBuilder<TModel, TUnderlyingMetadata> WhenTrue(Func<TModel, string> trueBecause) =>
        new(spec, trueBecause);

    
    public FalseMetadataSpecBuilder<IEnumerable<TModel>, TMetadata, TUnderlyingMetadata> WhenAllTrue<TMetadata>(
        TMetadata whenTrue) =>
        new(spec.CreateAllSpec(), _ => whenTrue);

    public FalseMetadataSpecBuilder<IEnumerable<TModel>, TMetadata, TUnderlyingMetadata> WhenAllTrue<TMetadata>(
        Func<IEnumerable<TModel>, TMetadata> whenTrue) =>
        new(spec.CreateAllSpec(), whenTrue);

    public FalseReasonsWithDescriptionSpecBuilder<IEnumerable<TModel>, TUnderlyingMetadata> WhenAllTrue(
        string trueBecause) =>
        new(spec.CreateAllSpec(), _ => trueBecause, trueBecause);

    public FalseReasonsSpecBuilder<IEnumerable<TModel>, TUnderlyingMetadata> WhenAllTrue(
        Func<IEnumerable<TModel>, string> trueBecause) =>
        new(spec.CreateAllSpec(), trueBecause);
    
    
    public FalseMetadataSpecBuilder<IEnumerable<TModel>, TMetadata, TUnderlyingMetadata> WhenAnyTrue<TMetadata>(
        TMetadata whenTrue) =>
        new(spec.CreateAnySpec(), _ => whenTrue);

    public FalseMetadataSpecBuilder<IEnumerable<TModel>, TMetadata, TUnderlyingMetadata> WhenAnyTrue<TMetadata>(
        Func<IEnumerable<TModel>, TMetadata> whenTrue) =>
        new(spec.CreateAnySpec(), whenTrue);

    public FalseReasonsWithDescriptionSpecBuilder<IEnumerable<TModel>, TUnderlyingMetadata> WhenAnyTrue(
        string trueBecause) =>
        new(spec.CreateAnySpec(), _ => trueBecause, trueBecause);

    public FalseReasonsSpecBuilder<IEnumerable<TModel>, TUnderlyingMetadata> WhenAnyTrue(
        Func<IEnumerable<TModel>, string> trueBecause) =>
        new(spec.CreateAnySpec(), trueBecause);
    
    
    public FalseMetadataSpecBuilder<IEnumerable<TModel>, TMetadata, TUnderlyingMetadata> WhenAtMostNTrue<TMetadata>(
        int n, TMetadata whenTrue) =>
        new(spec.CreateAtMostSpec(n), _ => whenTrue);

    public FalseMetadataSpecBuilder<IEnumerable<TModel>, TMetadata, TUnderlyingMetadata> WhenAtMostNTrue<TMetadata>(
        int n, Func<IEnumerable<TModel>, TMetadata> whenTrue) =>
        new(spec.CreateAtMostSpec(n), whenTrue);

    public FalseReasonsWithDescriptionSpecBuilder<IEnumerable<TModel>, TUnderlyingMetadata> WhenAtMostNTrue(int n,
        string trueBecause) =>
        new(spec.CreateAtMostSpec(n), _ => trueBecause, trueBecause);

    public FalseReasonsSpecBuilder<IEnumerable<TModel>, TUnderlyingMetadata> WhenAtMostNTrue(int n,
        Func<IEnumerable<TModel>, string> trueBecause) =>
        new(spec.CreateAtMostSpec(n), trueBecause);

    
    public FalseMetadataSpecBuilder<IEnumerable<TModel>, TMetadata, TUnderlyingMetadata> WhenAtLeastNTrue<TMetadata>(
        int n, TMetadata whenTrue) =>
        new(spec.CreateAtMostSpec(n), _ => whenTrue);

    public FalseMetadataSpecBuilder<IEnumerable<TModel>, TMetadata, TUnderlyingMetadata> WhenAtLeastNTrue<TMetadata>(
        int n, Func<IEnumerable<TModel>, TMetadata> whenTrue) =>
        new(spec.CreateAtMostSpec(n), whenTrue);

    public FalseReasonsWithDescriptionSpecBuilder<IEnumerable<TModel>, TUnderlyingMetadata> WhenAtLeastNTrue(int n,
        string trueBecause) =>
        new(spec.CreateAtMostSpec(n), _ => trueBecause, trueBecause);

    public FalseReasonsSpecBuilder<IEnumerable<TModel>, TUnderlyingMetadata> WhenAtLeastNTrue(int n,
        Func<IEnumerable<TModel>, string> trueBecause) =>
        new(spec.CreateAtMostSpec(n), trueBecause);
    
    
    public FalseMetadataSpecBuilder<IEnumerable<TModel>, TMetadata, TUnderlyingMetadata> WhenExactlyNTrue<TMetadata>(
        int n, TMetadata whenTrue) =>
        new(spec.CreateExactlySpec(n), _ => whenTrue);

    public FalseMetadataSpecBuilder<IEnumerable<TModel>, TMetadata, TUnderlyingMetadata> WhenExactlyNTrue<TMetadata>(
        int n, Func<IEnumerable<TModel>, TMetadata> whenTrue) =>
        new(spec.CreateExactlySpec(n), whenTrue);

    public FalseReasonsWithDescriptionSpecBuilder<IEnumerable<TModel>, TUnderlyingMetadata> WhenExactlyNTrue(int n,
        string trueBecause) =>
        new(spec.CreateExactlySpec(n), _ => trueBecause, trueBecause);

    public FalseReasonsSpecBuilder<IEnumerable<TModel>, TUnderlyingMetadata> WhenExactlyNTrue(int n,
        Func<IEnumerable<TModel>, string> trueBecause) =>
        new(spec.CreateExactlySpec(n), trueBecause);
    
    
    public FalseMetadataSpecBuilder<IEnumerable<TModel>, TMetadata, TUnderlyingMetadata> WhenRangeTrue<TMetadata>(
        int min, int max, TMetadata whenTrue) =>
        new(spec.CreateRangeSpec(min, max), _ => whenTrue);

    public FalseMetadataSpecBuilder<IEnumerable<TModel>, TMetadata, TUnderlyingMetadata> WhenRangeTrue<TMetadata>(
        int min, int max, Func<IEnumerable<TModel>, TMetadata> whenTrue) =>
        new(spec.CreateRangeSpec(min, max), whenTrue);

    public FalseReasonsWithDescriptionSpecBuilder<IEnumerable<TModel>, TUnderlyingMetadata> WhenRangeTrue(
        int min, int max, string trueBecause) =>
        new(spec.CreateRangeSpec(min, max), _ => trueBecause, trueBecause);

    public FalseReasonsSpecBuilder<IEnumerable<TModel>, TUnderlyingMetadata> WhenRangeTrue(
        int min, int max, Func<IEnumerable<TModel>, string> trueBecause) =>
        new(spec.CreateRangeSpec(min, max), trueBecause);
}