using System.Collections.Immutable;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Motiv.Generator.FluentFactory.Generation;
using Motiv.Generator.FluentFactory.Model.Steps;
using Motiv.Generator.FluentFactory.Model.Storage;

namespace Motiv.Generator.FluentFactory.Model.Methods;

public class MultiMethod : IFluentMethod
{
    private readonly Lazy<ImmutableArray<FluentTypeParameter>> _lazyTypeParameters;

    private readonly Lazy<ImmutableArray<FluentMethodParameter>> _lazyMethodParameters;

    public MultiMethod(
        IParameterSymbol sourceParameterSymbol,
        IFluentReturn fluentReturn,
        INamespaceSymbol rootNamespace,
        IMethodSymbol parameterConverter,
        ImmutableArray<FluentMethodParameter> availableParameterFields,
        OrderedDictionary<IParameterSymbol, IFluentValueStorage> valueStorages,
        ImmutableArray<IMethodSymbol> siblingMultiMethods)
    {
        _lazyMethodParameters = new Lazy<ImmutableArray<FluentMethodParameter>>(GetMethodParameters);
        _lazyTypeParameters = new Lazy<ImmutableArray<FluentTypeParameter>>(GetTypeParameters);
        _lazyResolvedTypes = new Lazy<ImmutableHashSet<ITypeSymbol>>(GetResolvedTypeParameters);

        Name = parameterConverter.Name;
        ValueSources = valueStorages;
        RootNamespace = rootNamespace;
        SourceParameter = sourceParameterSymbol;
        Return = fluentReturn;
        ParameterConverter = parameterConverter;
        AvailableParameterFields = availableParameterFields;
        SiblingMultiMethods = siblingMultiMethods.ToImmutableHashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
        DocumentationSummary = GetDocumentationSummary(sourceParameterSymbol, parameterConverter);
        ParameterDocumentation = GetParameterDocumentation(parameterConverter);
    }

    public ImmutableHashSet<IMethodSymbol> SiblingMultiMethods { get; }

    private readonly Lazy<ImmutableHashSet<ITypeSymbol>> _lazyResolvedTypes;

    public IMethodSymbol ParameterConverter { get; }

    public string Name { get; }

    public ImmutableArray<FluentMethodParameter> MethodParameters => _lazyMethodParameters.Value;

    public OrderedDictionary<IParameterSymbol, IFluentValueStorage> ValueSources { get; }
    public string? DocumentationSummary { get; }
    public Dictionary<string, string>? ParameterDocumentation { get; }

    public IParameterSymbol SourceParameter { get; }

    public ImmutableArray<FluentMethodParameter> AvailableParameterFields { get; }

    public IFluentReturn Return { get; }

    public ImmutableArray<FluentTypeParameter> TypeParameters => _lazyTypeParameters.Value;

    public INamespaceSymbol RootNamespace { get; }

    public override string ToString() => $"MultiMethod: {ParameterConverter.ToFullDisplayString()}";

    private static string? GetDocumentationSummary(IParameterSymbol sourceParameterSymbol, IMethodSymbol parameterConverter)
    {
        // First, try to get documentation from the template method (for MultipleFluentMethods)
        var templateMethodDoc = ExtractSummaryFromDocumentation(parameterConverter.GetDocumentationCommentXml());
        if (!string.IsNullOrWhiteSpace(templateMethodDoc))
        {
            return templateMethodDoc;
        }

        // Second, try to get parameter-specific documentation
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

    private static Dictionary<string, string>? GetParameterDocumentation(IMethodSymbol templateMethod)
    {
        var templateMethodDoc = templateMethod.GetDocumentationCommentXml();
        if (string.IsNullOrWhiteSpace(templateMethodDoc))
            return null;

        try
        {
            // Parse XML to extract parameter documentation
            var doc = XDocument.Parse(templateMethodDoc);
            var paramElements = doc.Descendants("param");

            var paramDocs = new Dictionary<string, string>();
            foreach (var paramElement in paramElements)
            {
                var name = paramElement.Attribute("name")?.Value;
                var description = paramElement.Value.Trim();

                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(description))
                {
                    paramDocs[name!] = description;
                }
            }

            return paramDocs.Count > 0 ? paramDocs : null;
        }
        catch
        {
            // If XML parsing fails, return null
            return null;
        }
    }

    private ImmutableArray<FluentMethodParameter> GetMethodParameters()
    {
        return
        [
            ..ParameterConverter.Parameters
                .Select(p => new FluentMethodParameter(p, Name))
        ];
    }

    private ImmutableArray<FluentTypeParameter> GetTypeParameters()
    {
        return
        [
            ..ParameterConverter.TypeArguments
                .SelectMany(arg => arg.GetGenericTypeParameters())
                .Except(_lazyResolvedTypes.Value, FluentTypeSymbolEqualityComparer.Default)
                .OfType<ITypeParameterSymbol>()
                .Select(typeParameter => new FluentTypeParameter(typeParameter))
        ];
    }


    private ImmutableHashSet<ITypeSymbol> GetResolvedTypeParameters()
    {
        return ValueSources
            .SelectMany(source => source.Value.Type.GetGenericTypeParameters())
            .ToImmutableHashSet(FluentTypeSymbolEqualityComparer.Default);
    }
}
