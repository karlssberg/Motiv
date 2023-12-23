namespace Karlssberg.Motive;

public sealed class Specification<TModel, TMetadata>(
    string description,
    Func<TModel, bool> predicate, 
    Func<TModel, TMetadata> whenTrue, 
    Func<TModel, TMetadata> whenFalse) : SpecificationBase<TModel, TMetadata>
{
    public Specification(
        string description,
        Func<TModel, bool> predicate, 
        TMetadata whenTrue, 
        TMetadata whenFalse) : this(description, predicate, _ => whenTrue, _ => whenFalse)
    {
    }
    
    public Specification(
        string description,
        Func<TModel, bool> predicate, 
        Func<TModel, TMetadata> whenTrue, 
        TMetadata whenFalse) : this(description, predicate, whenTrue, _ => whenFalse)
    {
    }
    
    public Specification(
        string description,
        Func<TModel, bool> predicate, 
        TMetadata whenTrue, 
        Func<TModel, TMetadata> whenFalse) : this(description, predicate, _ => whenTrue, whenFalse)
    {
    }
    
    private readonly Func<TModel, TMetadata> _whenTrue = Throw.IfNull(whenTrue, nameof(whenTrue));
    private readonly Func<TModel, TMetadata> _whenFalse = Throw.IfNull(whenFalse, nameof(whenFalse));
    private readonly Func<TModel, bool> _predicate = Throw.IfNull(predicate, nameof(predicate));
    
    public override string Description { get; } = Throw.IfNullOrWhitespace(description, nameof(description));

    public override BooleanResultBase<TMetadata> Evaluate(TModel model) =>
        SpecificationException.WrapThrownExceptions(
            this,
            () =>
            {
                var isSatisfied = _predicate(model);
                var cause = isSatisfied
                    ? _whenTrue(model)
                    : _whenFalse(model);

                return new BooleanResult<TMetadata>(isSatisfied, cause, description);
            });
}

public sealed class Specification<TModel>(
    string description,
    Func<TModel, bool> predicate,
    Func<TModel, string> trueBecause,
    Func<TModel, string> falseBecause) : SpecificationBase<TModel, string>
{
    public Specification(
        string description,
        Func<TModel, bool> predicate, 
        string trueBecause, 
        string falseBecause) : this(description, predicate, _ => trueBecause, _ => falseBecause)
    {
        Throw.IfNullOrWhitespace(trueBecause, nameof(trueBecause));
        Throw.IfNullOrWhitespace(falseBecause, nameof(falseBecause));
    }
    
    public Specification(
        Func<TModel, bool> predicate, 
        string trueBecause, 
        string falseBecause) : this(trueBecause, predicate, _ => trueBecause, _ => falseBecause)
    {
        Throw.IfNullOrWhitespace(trueBecause, nameof(trueBecause));
        Throw.IfNullOrWhitespace(falseBecause, nameof(falseBecause));
    }
    
    public Specification(
        string description,
        Func<TModel, bool> predicate, 
        Func<TModel, string> trueBecause, 
        string falseBecause) : this(description, predicate, trueBecause, _ => falseBecause)
    {
        Throw.IfNullOrWhitespace(falseBecause, nameof(falseBecause));
    }
    
    public Specification(
        Func<TModel, bool> predicate, 
        string trueBecause, 
        Func<TModel, string> falseBecause) : this(trueBecause, predicate, _ => trueBecause, falseBecause)
    {
        Throw.IfNullOrWhitespace(trueBecause, nameof(trueBecause));
    }
    
    private readonly Func<TModel, bool> _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    private readonly Func<TModel, string> _trueBecause = trueBecause ?? throw new ArgumentNullException(nameof(trueBecause));
    private readonly Func<TModel, string> _falseBecause = falseBecause ?? throw new ArgumentNullException(nameof(falseBecause));

    public override BooleanResultBase<string> Evaluate(TModel model) =>
        SpecificationException.WrapThrownExceptions(
            this,
            () =>
            {
                _predicate(model);
                var isSatisfied = _predicate(model);
                var because = isSatisfied 
                    ? _trueBecause(model) 
                    : _falseBecause(model);
                
                return new BooleanResult(isSatisfied, because);
            });

    public override string Description => description;
}