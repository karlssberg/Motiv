namespace Motiv.FluentBuilder.Example
{
    public partial record Cuboid<T>
        where T : System.Numerics.INumber<T>
    {
        /// <summary>
        ///     <seealso cref="Motiv.FluentBuilder.Example.Cuboid{T}"/>
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static Step_0__Motiv_FluentBuilder_Example_Cuboid____<T> WithWidth(in T width)
        {
            return new Step_0__Motiv_FluentBuilder_Example_Cuboid____<T>(width);
        }
    }

    /// <summary>
    ///     <seealso cref="Motiv.FluentBuilder.Example.Cuboid{T}"/>
    /// </summary>
    public struct Step_0__Motiv_FluentBuilder_Example_Cuboid____<T> where T : System.Numerics.INumber<T>
    {
        private readonly T _width__parameter;
        internal Step_0__Motiv_FluentBuilder_Example_Cuboid____(in T width)
        {
            this._width__parameter = width;
        }

        /// <summary>
        ///     <seealso cref="Motiv.FluentBuilder.Example.Cuboid{T}"/>
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_1__Motiv_FluentBuilder_Example_Cuboid____<T> WithHeight(in T height)
        {
            return new Step_1__Motiv_FluentBuilder_Example_Cuboid____<T>(this._width__parameter, height);
        }
    }

    /// <summary>
    ///     <seealso cref="Motiv.FluentBuilder.Example.Cuboid{T}"/>
    /// </summary>
    public struct Step_1__Motiv_FluentBuilder_Example_Cuboid____<T> where T : System.Numerics.INumber<T>
    {
        private readonly T _width__parameter;
        private readonly T _height__parameter;
        internal Step_1__Motiv_FluentBuilder_Example_Cuboid____(in T width, in T height)
        {
            this._width__parameter = width;
            this._height__parameter = height;
        }

        /// <summary>
        ///     <seealso cref="Motiv.FluentBuilder.Example.Cuboid{T}"/>
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_2__Motiv_FluentBuilder_Example_Cuboid____<T> WithDepth(in T depth)
        {
            return new Step_2__Motiv_FluentBuilder_Example_Cuboid____<T>(this._width__parameter, this._height__parameter, depth);
        }
    }

    /// <summary>
    ///     <seealso cref="Motiv.FluentBuilder.Example.Cuboid{T}"/>
    /// </summary>
    public struct Step_2__Motiv_FluentBuilder_Example_Cuboid____<T> where T : System.Numerics.INumber<T>
    {
        private readonly T _width__parameter;
        private readonly T _height__parameter;
        private readonly T _depth__parameter;
        internal Step_2__Motiv_FluentBuilder_Example_Cuboid____(in T width, in T height, in T depth)
        {
            this._width__parameter = width;
            this._height__parameter = height;
            this._depth__parameter = depth;
        }

        /// <summary>
        /// Creates a new instance using constructor Motiv.FluentBuilder.Example.Cuboid<T>.Cuboid(T Width, T Height, T Depth).
        ///
        ///     <seealso cref="Motiv.FluentBuilder.Example.Cuboid{T}"/>
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Cuboid<T> Create()
        {
            return new Cuboid<T>(this._width__parameter, this._height__parameter, this._depth__parameter);
        }
    }
}