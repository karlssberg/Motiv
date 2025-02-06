using System;

public partial record Cuboid
{
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Step_0__Cuboid WithWidth(in int width)
    {
        return new Step_0__Cuboid(width);
    }
}

public struct Step_0__Cuboid
{
    private readonly int _width__parameter;
    public Step_0__Cuboid(in int width)
    {
        this._width__parameter = width;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Step_1__Cuboid WithHeight(in int height)
    {
        return new Step_1__Cuboid(this._width__parameter, height);
    }
}

public struct Step_1__Cuboid
{
    private readonly int _width__parameter;
    private readonly int _height__parameter;
    public Step_1__Cuboid(in int width, in int height)
    {
        this._width__parameter = width;
        this._height__parameter = height;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Step_2__Cuboid WithDepth(in int depth)
    {
        return new Step_2__Cuboid(this._width__parameter, this._height__parameter, depth);
    }
}

public struct Step_2__Cuboid
{
    private readonly int _width__parameter;
    private readonly int _height__parameter;
    private readonly int _depth__parameter;
    public Step_2__Cuboid(in int width, in int height, in int depth)
    {
        this._width__parameter = width;
        this._height__parameter = height;
        this._depth__parameter = depth;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Cuboid Create()
    {
        return new Cuboid(this._width__parameter, this._height__parameter, this._depth__parameter);
    }
}