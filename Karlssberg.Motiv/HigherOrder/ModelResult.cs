namespace Karlssberg.Motiv.HigherOrder;

public record ModelResult<TModel>(TModel Model, bool Satisfied)
{
    public TModel Model { get; } = Model;
    public bool Satisfied { get; } = Satisfied;
}