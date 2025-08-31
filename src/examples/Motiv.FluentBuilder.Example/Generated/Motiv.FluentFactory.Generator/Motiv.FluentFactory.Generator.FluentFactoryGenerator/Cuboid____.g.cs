public partial record Cuboid<T>
{
    /// <summary>
    ///     <seealso cref="Cuboid{T}"/>
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Step_0__Cuboid____<T> WithWidth(in T width)
    {
        return new Step_0__Cuboid____<T>(width);
    }
}

/// <summary>
///     <seealso cref="Cuboid{T}"/>
/// </summary>
public struct Step_0__Cuboid____<T>
{
    private readonly T _width__parameter;
    internal Step_0__Cuboid____(in T width)
    {
        this._width__parameter = width;
    }

    /// <summary>
    ///     <seealso cref="Cuboid{T}"/>
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Step_1__Cuboid____<T> WithHeight(in T height)
    {
        return new Step_1__Cuboid____<T>(this._width__parameter, height);
    }
}

/// <summary>
///     <seealso cref="Cuboid{T}"/>
/// </summary>
public struct Step_1__Cuboid____<T>
{
    private readonly T _width__parameter;
    private readonly T _height__parameter;
    internal Step_1__Cuboid____(in T width, in T height)
    {
        this._width__parameter = width;
        this._height__parameter = height;
    }

    /// <summary>
    ///     <seealso cref="Cuboid{T}"/>
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Step_2__Cuboid____<T> WithDepth(in T depth)
    {
        return new Step_2__Cuboid____<T>(this._width__parameter, this._height__parameter, depth);
    }
}

/// <summary>
///     <seealso cref="Cuboid{T}"/>
/// </summary>
public struct Step_2__Cuboid____<T>
{
    private readonly T _width__parameter;
    private readonly T _height__parameter;
    private readonly T _depth__parameter;
    internal Step_2__Cuboid____(in T width, in T height, in T depth)
    {
        this._width__parameter = width;
        this._height__parameter = height;
        this._depth__parameter = depth;
    }

    /// <summary>
    /// Creates a new instance using constructor Cuboid<T>.Cuboid(T Width, T Height, T Depth).
    ///
    ///     <seealso cref="Cuboid{T}"/>
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Cuboid<T> Create()
    {
        return new Cuboid<T>(this._width__parameter, this._height__parameter, this._depth__parameter);
    }
}