
using Motiv;
using System.Diagnostics;

public class Playground
{
    private readonly SpecBase<IsFeatureEnabledProposition.Model, string> _isFeatureEnabledProposition = new IsFeatureEnabledProposition()
        .Tap((model, result) =>
            Debug.WriteLine($"[Motiv] IsFeatureEnabledProposition | Model: {model} | Satisfied: {result.Satisfied} | Reason: {result.Reason}"));
    public bool IsFeatureEnabled(int valueA, int valueB, int valueC, string text)
    {
        // (valueA >= 0 && 1 < valueC) ||
        //     valueB >= 0 && 1 < valueC && (string.IsNullOrEmpty(text) && IsGreen(text))
        var result = _isFeatureEnabledProposition.Evaluate(new IsFeatureEnabledProposition.Model(valueA, valueC, valueB, text));
        return result.Satisfied;
    }

    public static bool IsGreen(string text)
    {
        return text == "green";
    }
}


public class IsFeatureEnabledProposition() : Spec<IsFeatureEnabledProposition.Model>(() =>
{
    var isValueANonNegative = Spec
        .Build((Model m) => m.ValueA >= 0)
        .Create("valueA >= 0");

    var is1LessThanValueC = Spec
        .Build((Model m) => 1 < m.ValueC)
        .Create("1 < valueC");

    var isValueBNonNegative = Spec
        .Build((Model m) => m.ValueB >= 0)
        .Create("valueB >= 0");

    var isNullOrEmpty = Spec
        .Build((Model m) => string.IsNullOrEmpty(m.Text))
        .Create("string.IsNullOrEmpty(text)");

    var isGreen = Spec
        .Build((Model m) => IsGreen(m.Text))
        .Create("IsGreen(text)");

    return (isValueANonNegative.AndAlso(is1LessThanValueC)).OrElse(isValueBNonNegative.AndAlso(is1LessThanValueC)
        .AndAlso((isNullOrEmpty.AndAlso(isGreen))));
})
{
    public record Model(int ValueA, int ValueC, int ValueB, string Text);
}
