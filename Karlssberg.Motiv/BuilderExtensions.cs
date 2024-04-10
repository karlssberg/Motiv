namespace Karlssberg.Motiv;

public static class BuilderExtensions
{
    
    public static Func<IEnumerable<TResult>> ToEnumerableReturn<TResult>(this Func<TResult> func) =>
        () => func().ToEnumerable();
    
    public static Func<T, IEnumerable<TResult>> ToEnumerableReturn<T, TResult>(this Func<T, TResult> func) =>
        argument => func(argument).ToEnumerable();
    
    
    public static Func<T1, T2, IEnumerable<TResult>> ToEnumerableReturn<T1, T2, TResult>(this Func<T1, T2, TResult> func) =>
        (argument1, argument2) => func(argument1, argument2).ToEnumerable();
    
    public static Func<T, TValue> ToFunc<T, TValue>(
        this TValue value) =>
        _ => value;
    
    public static Func<T1, T2, TValue> ToFunc<T1, T2, TValue>(
        this TValue value) =>
        (_, _) => value;
}