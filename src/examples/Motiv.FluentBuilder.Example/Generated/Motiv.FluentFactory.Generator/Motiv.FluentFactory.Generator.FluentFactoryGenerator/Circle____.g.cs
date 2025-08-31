public partial record Circle<T>
    where T : System.Numerics.INumber<T>
{
    /// <summary>
    ///     <seealso cref="Circle{T}"/>
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Step_0__Circle____<T> WithRadius(in T radius)
    {
        return new Step_0__Circle____<T>(radius);
    }
}

/// <summary>
///     <seealso cref="Circle{T}"/>
/// </summary>
public struct Step_0__Circle____<T> where T : System.Numerics.INumber<T>
{
    private readonly T _radius__parameter;
    internal Step_0__Circle____(in T radius)
    {
        this._radius__parameter = radius;
    }

    /// <summary>
    /// Creates a new instance using constructor Circle<T>.Circle(T Radius).
    ///
    ///     <seealso cref="Circle{T}"/>
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Circle<T> Create()
    {
        return new Circle<T>(this._radius__parameter);
    }
}