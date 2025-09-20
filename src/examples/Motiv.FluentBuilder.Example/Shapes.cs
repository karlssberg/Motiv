using System.Numerics;
using Motiv.FluentFactory.Attributes;

namespace Motiv.FluentBuilder.Example;

[FluentFactory]
public partial class Shape<T> where T : INumber<T>;

[FluentFactory]
public partial class Shape;

[FluentFactory]
[FluentConstructor(typeof(Square<>))]
[FluentConstructor(typeof(Shape), CreateMethodName = "CreateSquare")]
[FluentConstructor(typeof(Shape<>), CreateMethodName = "CreateSquare")]
public partial record Square<T>(T Width) where T : INumber<T>;

[FluentFactory]
[FluentConstructor(typeof(Rectangle<>))]
[FluentConstructor(typeof(Shape), CreateMethodName = "CreateRectangle")]
[FluentConstructor(typeof(Shape<>), CreateMethodName = "CreateRectangle")]
public partial record Rectangle<T>(T Width, T Height) where T : INumber<T>;

[FluentFactory]
[FluentConstructor(typeof(Circle<>))]
[FluentConstructor(typeof(Shape), CreateMethodName = "CreateCircle")]
[FluentConstructor(typeof(Shape<>), CreateMethodName = "CreateCircle")]
public partial record Circle<T>(T Radius) where T : INumber<T>;

[FluentFactory]
[FluentConstructor(typeof(Diamond<>))]
[FluentConstructor(typeof(Shape), CreateMethodName = "CreateDiamond")]
[FluentConstructor(typeof(Shape<>), CreateMethodName = "CreateDiamond")]
public partial record Diamond<T>(T Width, T Height) where T : INumber<T>;

[FluentFactory]
[FluentConstructor(typeof(Cuboid<>))]
[FluentConstructor(typeof(Shape), CreateMethodName = "CreateCuboid")]
[FluentConstructor(typeof(Shape<>), CreateMethodName = "CreateCuboid")]
public partial record Cuboid<T>(T Width, T Height, T Depth) where T : INumber<T>;
