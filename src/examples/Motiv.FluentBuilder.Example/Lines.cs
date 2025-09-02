using System.Numerics;
using Motiv.FluentFactory.Generator;

namespace Motiv.FluentBuilder.Example;

[FluentFactory]
public static partial class Line;

[FluentConstructor(typeof(Line))]
public partial record Line2D<T>(
    [FluentMethod("X")]T X,
    [FluentMethod("Y")]T Y)
    where T : INumber<T>;

[FluentConstructor(typeof(Line))]
public partial record Line3D<T>(
    [FluentMethod("X")]T X,
    [FluentMethod("Y")]T Y,
    [FluentMethod("Z")]T Z)
    where T : INumber<T>;
