
using Motiv.Generator.Attributes;

Console.WriteLine("Hello World!");

var rectangle = Rectangle.WithWidth(10).WithHeight(20).Create();
var square = Square.WithWidth(10).Create();
var circle = Circle.WithRadius(5).Create();
var cuboid = Cuboid.WithWidth(10).WithHeight(20).WithDepth(30).Create();

var rectangleFromShape = Shape.WithWidth(10).WithHeight(20).Create();
var squareFromShape = Shape.WithWidth(10).Create();
var circleFromShape = Shape.WithRadius(5).Create();
var cuboidFromShape = Shape.WithWidth(10).WithHeight(20).WithDepth(30).Create();

[FluentFactory]
public partial class Shape;

[FluentFactory]
[FluentConstructor(typeof(Square))]
[FluentConstructor(typeof(Shape))]
public partial record Square(int Width);

[FluentFactory]
[FluentConstructor(typeof(Rectangle))]
[FluentConstructor(typeof(Shape))]
public partial record Rectangle(int Width, int Height);

[FluentFactory]
[FluentConstructor(typeof(Circle))]
[FluentConstructor(typeof(Shape))]
public partial record Circle(int Radius);

[FluentFactory]
[FluentConstructor(typeof(Cuboid))]
[FluentConstructor(typeof(Shape))]
public partial record Cuboid(int Width, int Height, int Depth);
