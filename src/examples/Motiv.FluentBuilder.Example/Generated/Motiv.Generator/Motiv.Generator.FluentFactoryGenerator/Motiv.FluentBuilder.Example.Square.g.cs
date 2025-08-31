using System;

namespace Motiv.FluentBuilder.Example
{
    public partial record Square
    {
        /// <summary>
        ///     <seealso cref="Motiv.FluentBuilder.Example.Square"/>
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static Step_0__Motiv_FluentBuilder_Example_Square WithWidth(in int width)
        {
            return new Step_0__Motiv_FluentBuilder_Example_Square(width);
        }
    }

    /// <summary>
    ///     <seealso cref="Motiv.FluentBuilder.Example.Square"/>
    /// </summary>
    public struct Step_0__Motiv_FluentBuilder_Example_Square
    {
        private readonly int _width__parameter;
        internal Step_0__Motiv_FluentBuilder_Example_Square(in int width)
        {
            this._width__parameter = width;
        }

        /// <summary>
        /// Creates a new instance using constructor Motiv.FluentBuilder.Example.Square.Square(int Width).
        ///
        ///     <seealso cref="Motiv.FluentBuilder.Example.Square"/>
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Square Create()
        {
            return new Square(this._width__parameter);
        }
    }
}