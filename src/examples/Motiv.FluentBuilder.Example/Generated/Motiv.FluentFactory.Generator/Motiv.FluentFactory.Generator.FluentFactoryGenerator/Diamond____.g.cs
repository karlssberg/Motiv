public partial record Diamond<T>
    where T : System.Numerics.INumber<T>
{
    /// <summary>
    ///     <seealso cref="Diamond{T}"/>
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Step_0__Diamond____<T> WithWidth(in T width)
    {
        return new Step_0__Diamond____<T>(width);
    }
}

/// <summary>
///     <seealso cref="Diamond{T}"/>
/// </summary>
public struct Step_0__Diamond____<T> where T : System.Numerics.INumber<T>
{
    private readonly T _width__parameter;
    internal Step_0__Diamond____(in T width)
    {
        this._width__parameter = width;
    }

    /// <summary>
    ///     <seealso cref="Diamond{T}"/>
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Step_1__Diamond____<T> WithHeight(in T height)
    {
        return new Step_1__Diamond____<T>(this._width__parameter, height);
    }
}

/// <summary>
///     <seealso cref="Diamond{T}"/>
/// </summary>
public struct Step_1__Diamond____<T> where T : System.Numerics.INumber<T>
{
    private readonly T _width__parameter;
    private readonly T _height__parameter;
    internal Step_1__Diamond____(in T width, in T height)
    {
        this._width__parameter = width;
        this._height__parameter = height;
    }

    /// <summary>
    /// Creates a new instance using constructor Diamond<T>.Diamond(T Width, T Height).
    ///
    ///     <seealso cref="Diamond{T}"/>
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Diamond<T> Create()
    {
        return new Diamond<T>(this._width__parameter, this._height__parameter);
    }
}