using System;

public partial record Rectangle
{
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static Step_0__Rectangle WithWidth(in int width)
    {
        return new Step_0__Rectangle(width);
    }
}

public struct Step_0__Rectangle
{
    private readonly int _width__parameter;
    public Step_0__Rectangle(in int width)
    {
        this._width__parameter = width;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Step_1__Rectangle WithHeight(in int height)
    {
        return new Step_1__Rectangle(this._width__parameter, height);
    }
}

public struct Step_1__Rectangle
{
    private readonly int _width__parameter;
    private readonly int _height__parameter;
    public Step_1__Rectangle(in int width, in int height)
    {
        this._width__parameter = width;
        this._height__parameter = height;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Rectangle Create()
    {
        return new Rectangle(this._width__parameter, this._height__parameter);
    }
}