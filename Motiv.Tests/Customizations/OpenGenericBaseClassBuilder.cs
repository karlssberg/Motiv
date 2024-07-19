using AutoFixture.Kernel;

namespace Motiv.Tests.Customizations;

public class OpenGenericBaseClassBuilder(Type baseType) : ISpecimenBuilder
{
    public object Create(object request, ISpecimenContext context)
    {
        if (request is not Type { IsGenericType: true, IsGenericTypeDefinition: true } type || type.GetGenericTypeDefinition() != baseType)
        {
            return new NoSpecimen();
        }

        // Create an instance of the concrete type
        var concreteType = type.MakeGenericType(type.GetGenericArguments());
        return context.Resolve(concreteType);
    }
}
