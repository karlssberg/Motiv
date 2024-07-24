using System.Reflection;
using AutoFixture;
using AutoFixture.Kernel;
using MethodInvoker = AutoFixture.Kernel.MethodInvoker;

namespace Motiv.Tests.Customizations;

public class InternalConstructorCustomization : ICustomization
{
    private readonly Type _openGenericType;

    public InternalConstructorCustomization(Type openGenericType)
    {
        if (openGenericType == null || !openGenericType.IsGenericTypeDefinition)
        {
            throw new ArgumentException("Must be an open generic type", nameof(openGenericType));
        }
        _openGenericType = openGenericType;
    }

    public void Customize(IFixture fixture)
    {
        fixture.Customizations.Add(new InternalSpecimenBuilder(_openGenericType));
    }
}

public class InternalConstructorQuery : IMethodQuery
{
    public IEnumerable<IMethod> SelectMethods(Type type)
    {
        if (type == null) { throw new ArgumentNullException(nameof(type)); }

        return from ci in type.GetTypeInfo().GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
               where ci.IsAssembly || ci.IsPublic // This ensures we only get internal or public constructors
               select new ConstructorMethod(ci) as IMethod;
    }
}

public class InternalConstructorInvoker() : MethodInvoker(new InternalConstructorQuery());

public class InternalSpecimenBuilder(Type openGenericType) : ISpecimenBuilder
{
    private readonly Type _openGenericType = openGenericType ?? throw new ArgumentNullException(nameof(openGenericType));
    private readonly InternalConstructorInvoker _invoker = new();

    public object Create(object request, ISpecimenContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));

        if (request is Type requestedType)
        {
            if (IsAssignableToGenericType(requestedType, _openGenericType))
            {
                var specimen = _invoker.Create(requestedType, context);
                if (specimen != null)
                {
                    return specimen;
                }
            }
        }

        return new NoSpecimen();
    }

    private bool IsAssignableToGenericType(Type givenType, Type genericType)
    {
        var interfaceTypes = givenType.GetInterfaces();

        if (interfaceTypes.Any(it => it.IsGenericType && it.GetGenericTypeDefinition() == genericType))
        {
            return true;
        }

        if (givenType.IsGenericType && givenType.GetGenericTypeDefinition() == genericType)
            return true;

        return givenType.BaseType != null && IsAssignableToGenericType(givenType.BaseType, genericType);
    }
}
