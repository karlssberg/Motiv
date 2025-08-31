public partial record Rectangle<T>
{
    /// <summary>
    ///     <seealso cref="Rectangle{T}"/>
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Step_0__Rectangle____<T> WithWidth(in T width)
    {
        return new Step_0__Rectangle____<T>(width);
    }
}

/// <summary>
///     <seealso cref="Rectangle{T}"/>
/// </summary>
public struct Step_0__Rectangle____<T>
{
    private readonly T _width__parameter;
    internal Step_0__Rectangle____(in T width)
    {
        this._width__parameter = width;
    }

    /// <summary>
    ///     <seealso cref="Rectangle{T}"/>
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Step_1__Rectangle____<T> WithHeight(in T height)
    {
        return new Step_1__Rectangle____<T>(this._width__parameter, height);
    }
}

/// <summary>
///     <seealso cref="Rectangle{T}"/>
/// </summary>
public struct Step_1__Rectangle____<T>
{
    private readonly T _width__parameter;
    private readonly T _height__parameter;
    internal Step_1__Rectangle____(in T width, in T height)
    {
        this._width__parameter = width;
        this._height__parameter = height;
    }

    /// <summary>
    /// Creates a new instance using constructor Rectangle<T>.Rectangle(T Width, T Height).
    ///
    ///     <seealso cref="Rectangle{T}"/>
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Rectangle<T> Create()
    {
        return new Rectangle<T>(this._width__parameter, this._height__parameter);
    }
}