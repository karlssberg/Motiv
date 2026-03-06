
using Motiv;

public class Playground
{
    private readonly IsFeatureEnabledProposition _isFeatureEnabledProposition;
    public Playground()
    {
        _isFeatureEnabledProposition = new IsFeatureEnabledProposition(this);
    }
    public bool IsFeatureEnabled(int valueA, int valueB, int valueC, string text)
    {
        // (valueA >= 0 && 1 < valueC) ||
        //     valueB >= 0 && 1 < valueC && (string.IsNullOrEmpty(text) && IsGreen(text))
        var result = _isFeatureEnabledProposition.IsSatisfiedBy(new IsFeatureEnabledProposition.Model(valueA, valueC, valueB, text));
        return result.Satisfied;
    }

    public bool IsGreen(string text)
    {
        return text == "green";
    }
}


public class IsFeatureEnabledProposition(Playground instance) : Spec<IsFeatureEnabledProposition.Model>(() =>
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

    var clause4 = Spec
        .Build((Model m) => string.IsNullOrEmpty(m.Text))
        .Create("string.IsNullOrEmpty(text)");

    var clause5 = Spec
        .Build((Model m) => instance.IsGreen(m.Text))
        .Create("IsGreen(text)");

    return (isValueANonNegative.AndAlso(is1LessThanValueC)).OrElse(isValueBNonNegative.AndAlso(is1LessThanValueC)
        .AndAlso((clause4.AndAlso(clause5))));
})
{
    public record Model(int ValueA, int ValueC, int ValueB, string Text);
}
