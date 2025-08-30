using System.Collections.Immutable;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Motiv.Generator.FluentFactory.Generation;
using Motiv.Generator.FluentFactory.Model.Steps;
using Motiv.Generator.FluentFactory.Model.Storage;

namespace Motiv.Generator.FluentFactory.Model.Methods;

public class RegularMethod : IFluentMethod
{
    private readonly Lazy<ImmutableArray<FluentTypeParameter>> _lazyTypeParameters;

    public RegularMethod(
        string name,
        IParameterSymbol sourceParameterSymbol,
        IFluentReturn fluentReturn,
        INamespaceSymbol rootNamespace,
        ImmutableArray<FluentMethodParameter> availableParameterFields,
        OrderedDictionary<IParameterSymbol, IFluentValueStorage> valueStorages)
    {
        _lazyTypeParameters = new Lazy<ImmutableArray<FluentTypeParameter>>(GetTypeParameters);

        Name = name;
        SourceParameter = sourceParameterSymbol;
        MethodParameters = GetMethodParameters(name, sourceParameterSymbol);
        RootNamespace = rootNamespace;
        ValueSources = valueStorages;
        AvailableParameterFields = availableParameterFields;
        Return = fluentReturn;
        DocumentationSummary = GetDocumentationSummary(sourceParameterSymbol);
        ParameterDocumentation = null; // Regular methods don't use template methods
    }

    public string Name { get; }

    public ImmutableArray<FluentMethodParameter> MethodParameters { get; }

    public OrderedDictionary<IParameterSymbol, IFluentValueStorage> ValueSources { get; }

    public string? DocumentationSummary { get; }

    public Dictionary<string, string>? ParameterDocumentation { get; }

    public IParameterSymbol SourceParameter { get; }

    public ImmutableArray<FluentMethodParameter> AvailableParameterFields { get; }

    public IFluentReturn Return { get; }

    public ImmutableArray<FluentTypeParameter> TypeParameters => _lazyTypeParameters.Value;

    public INamespaceSymbol RootNamespace { get; }

    public override string ToString() => $"RegularMethod: {Name}({string.Join(", ", MethodParameters.Select(p => p.ParameterSymbol.ToFullDisplayString()))})";

    private static string? GetDocumentationSummary(IParameterSymbol sourceParameterSymbol)
    {
        // For regular methods, extract parameter-specific documentation if available
        // First, try to get documentation from the parameter itself
        var parameterDoc = ExtractParameterDocumentation(sourceParameterSymbol);
        if (!string.IsNullOrWhiteSpace(parameterDoc))
        {
            return parameterDoc;
        }

        // Fallback to constructor documentation summary
        return ExtractSummaryFromDocumentation(sourceParameterSymbol.ContainingSymbol.GetDocumentationCommentXml());
    }

    private static string? ExtractParameterDocumentation(IParameterSymbol parameterSymbol)
    {
        // Get the XML documentation from the containing method/constructor
        var containingSymbolDoc = parameterSymbol.ContainingSymbol.GetDocumentationCommentXml();
        if (string.IsNullOrWhiteSpace(containingSymbolDoc))
            return null;

        try
        {
            // Parse XML to extract parameter-specific documentation
            var xmlDoc = XDocument.Parse(containingSymbolDoc);
            var paramElement = xmlDoc.Descendants("param")
                .FirstOrDefault(p => p.Attribute("name")?.Value == parameterSymbol.Name);

            return paramElement?.Value.Trim();
        }
        catch
        {
            // If XML parsing fails, return null to fall back to constructor documentation
            return null;
        }
    }

    private static string? ExtractSummaryFromDocumentation(string? xmlDoc)
    {
        if (string.IsNullOrWhiteSpace(xmlDoc))
            return null;

        try
        {
            // Parse XML to extract summary documentation
            var doc = XDocument.Parse(xmlDoc);
            var summaryElement = doc.Descendants("summary").FirstOrDefault();
            return summaryElement?.Value.Trim();
        }
        catch
        {
            // If XML parsing fails, return the raw documentation
            return xmlDoc;
        }
    }

    private static ImmutableArray<FluentMethodParameter> GetMethodParameters(string methodName,
        IParameterSymbol sourceParameterSymbol)
    {
        return [new FluentMethodParameter(sourceParameterSymbol, methodName)];
    }

    private ImmutableArray<FluentTypeParameter> GetTypeParameters()
    {
        return
        [
            ..SourceParameter.Type
                .GetGenericTypeParameters()
                .Select(genericTypeParameter => new FluentTypeParameter(genericTypeParameter))
        ];
    }
}
