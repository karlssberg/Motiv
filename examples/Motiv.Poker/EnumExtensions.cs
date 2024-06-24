using System.ComponentModel;
using System.Reflection;

namespace Motiv.Poker;

public static class EnumExtensions
{
    public static string GetDescription<TEnum>(this TEnum value) where TEnum : Enum
    {
        var attribute = value
            .GetType()
            .GetField(value.ToString())?
            .GetCustomAttribute<DescriptionAttribute>(false);

        return attribute?.Description ?? value.ToString();
    }

    public static T GetEnumFromDescription<T>(this string description) where T : Enum
    {
        var enumValues =
            from field in typeof(T).GetFields()
            let descriptionAttribute = field.GetCustomAttribute<DescriptionAttribute>()
            where description == (descriptionAttribute?.Description ?? field.Name)
            let value = field.GetValue(null)
            select value switch
            {
                T enumValue => enumValue,
                _ => default
            };

        return enumValues.Single();
    }
}