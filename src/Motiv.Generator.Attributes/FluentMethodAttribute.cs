namespace Motiv.Generator.Attributes;

[AttributeUsage(AttributeTargets.Parameter)]
public class FluentMethodAttribute(string methodName) : Attribute
{
    public string MethodName { get; } = methodName;
    public int Priority { get; set; }
}
