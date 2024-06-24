namespace Motiv;

internal static class BuilderExtensions
{
    
    internal static Func<T, IEnumerable<TResult>> ToEnumerableReturn<T, TResult>(this Func<T, TResult> func) =>
        argument => func(argument).ToEnumerable();
    
    
    internal static Func<T1, T2, IEnumerable<TResult>> ToEnumerableReturn<T1, T2, TResult>(this Func<T1, T2, TResult> func) =>
        (argument1, argument2) => func(argument1, argument2).ToEnumerable();
    
    internal static Func<T, TValue> ToFunc<T, TValue>(
        this TValue value) =>
        _ => value;
    
    internal static Func<T1, T2, TValue> ToFunc<T1, T2, TValue>(
        this TValue value) =>
        (_, _) => value;
}