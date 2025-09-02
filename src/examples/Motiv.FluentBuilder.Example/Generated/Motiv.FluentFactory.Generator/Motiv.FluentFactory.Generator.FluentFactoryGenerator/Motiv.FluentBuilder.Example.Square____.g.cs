namespace Motiv.FluentBuilder.Example
{
    public partial record Square<T>
        where T : System.Numerics.INumber<T>
    {
        /// <summary>
        ///     <seealso cref="Motiv.FluentBuilder.Example.Square{T}"/>
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static Step_0__Motiv_FluentBuilder_Example_Square____<T> WithWidth(in T width)
        {
            return new Step_0__Motiv_FluentBuilder_Example_Square____<T>(width);
        }
    }

    /// <summary>
    ///     <seealso cref="Motiv.FluentBuilder.Example.Square{T}"/>
    /// </summary>
    public struct Step_0__Motiv_FluentBuilder_Example_Square____<T> where T : System.Numerics.INumber<T>
    {
        private readonly T _width__parameter;
        internal Step_0__Motiv_FluentBuilder_Example_Square____(in T width)
        {
            this._width__parameter = width;
        }

        /// <summary>
        /// Creates a new instance using constructor Motiv.FluentBuilder.Example.Square<T>.Square(T Width).
        ///
        ///     <seealso cref="Motiv.FluentBuilder.Example.Square{T}"/>
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Square<T> Create()
        {
            return new Square<T>(this._width__parameter);
        }
    }
}