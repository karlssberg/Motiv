
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
        // (valueA >= 0 && 1 < valueC) || (valueB >= 0 && 1 < valueC) && (string.IsNullOrEmpty(text) && IsGreen(text))
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
    var isValueANonNegative = Spec.Build((IsFeatureEnabledProposition.Model m) => m.ValueA >= 0)
        .WhenTrue("valueA >= 0 == true")
        .WhenFalse("valueA >= 0 == false")
        .Create();

    var is1LessThanValueC = Spec.Build((IsFeatureEnabledProposition.Model m) => 1 < m.ValueC)
        .WhenTrue("1 < valueC == true")
        .WhenFalse("1 < valueC == false")
        .Create();

    var isValueBNonNegative = Spec.Build((IsFeatureEnabledProposition.Model m) => m.ValueB >= 0)
        .WhenTrue("valueB >= 0 == true")
        .WhenFalse("valueB >= 0 == false")
        .Create();

    var clause4 = Spec.Build((IsFeatureEnabledProposition.Model m) => string.IsNullOrEmpty(m.Text))
        .WhenTrue("string.IsNullOrEmpty(text) == true")
        .WhenFalse("string.IsNullOrEmpty(text) == false")
        .Create();

    var clause5 = Spec.Build((IsFeatureEnabledProposition.Model m) => instance.IsGreen(m.Text))
        .WhenTrue("IsGreen(text) == true")
        .WhenFalse("IsGreen(text) == false")
        .Create();

    return (isValueANonNegative.AndAlso(is1LessThanValueC)).OrElse((isValueBNonNegative.AndAlso(is1LessThanValueC)).AndAlso((clause4.AndAlso(clause5))));
})
{
    public record Model(int ValueA, int ValueC, int ValueB, string Text);
}
