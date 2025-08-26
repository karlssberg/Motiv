using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Motiv.Generator.FluentFactory.Analysis;
using Motiv.Generator.FluentFactory.Diagnostics;
using Motiv.Generator.FluentFactory.Generation;
using Motiv.Generator.FluentFactory.Generation.Shared;
using Motiv.Generator.FluentFactory.Model.Methods;
using Motiv.Generator.FluentFactory.Model.Steps;
using Motiv.Generator.FluentFactory.Model.Storage;
using static Motiv.Generator.FluentFactory.FluentFactoryGeneratorOptions;

namespace Motiv.Generator.FluentFactory.Model;

public class FluentModelFactory(Compilation compilation)
{
    private readonly DiagnosticList _diagnostics = [];
    private readonly OrderedDictionary<ParameterSequence, RegularFluentStep> _regularFluentSteps = new();
    private readonly UnreachableConstructorAnalyzer _unreachableConstructorAnalyzer = new();

    public FluentFactoryCompilationUnit CreateFluentFactoryCompilationUnit(
        INamedTypeSymbol rootType,
        ImmutableArray<FluentConstructorContext> fluentConstructorContexts)
    {
        _regularFluentSteps.Clear();
        _diagnostics.Clear();
        _unreachableConstructorAnalyzer.Clear();
        _unreachableConstructorAnalyzer.AddAllFluentConstructors(fluentConstructorContexts.Select(context => context.Constructor));

        var usings = GetUsingStatements(fluentConstructorContexts);

        var stepTrie = CreateFluentStepTrie(fluentConstructorContexts);

        var fluentRootMethods = ConvertNodeToFluentFluentMethods(rootType, stepTrie.Root, []);

        var childFluentSteps = fluentRootMethods
            .Select(m => m.Return)
            .OfType<IFluentStep>();

        var descendentFluentSteps = GetDescendentFluentSteps(childFluentSteps);
        var fluentBuilderSteps = descendentFluentSteps
            .DistinctBy(step => step.KnownConstructorParameters)
            .Select((step, index) =>
            {
                if (step is not RegularFluentStep regularFluentStep)
                    return step;

                regularFluentStep.Index = index;

                return step;
            })
            .ToImmutableArray();

        _diagnostics.AddRange(_unreachableConstructorAnalyzer.GetUnreachableConstructorsDiagnostics());
        var sampleConstructorContext = fluentConstructorContexts.First();
        return new FluentFactoryCompilationUnit(
            rootType,
            fluentRootMethods,
            fluentBuilderSteps,
            usings)
        {
            IsStatic = sampleConstructorContext.IsStatic,
            TypeKind = sampleConstructorContext.TypeKind,
            Accessibility = sampleConstructorContext.Accessibility,
            IsRecord = sampleConstructorContext.IsRecord,
            Diagnostics = _diagnostics
        };
    }

    private ImmutableArray<IFluentMethod> ConvertNodeToFluentFluentMethods(
        INamedTypeSymbol type,
        Trie<FluentMethodParameter, ConstructorMetadata>.Node node,
        OrderedDictionary<IParameterSymbol, IFluentValueStorage> valueStorages)
    {
        ImmutableArray<IFluentMethod> fluentMethods =
        [
            ..ConvertNodeToFluentMethods(type, node, valueStorages),
            ..ConvertNodeToCreationMethods(type, node, valueStorages)
        ];

        return fluentMethods;
    }

    private IEnumerable<IFluentMethod> ConvertNodeToFluentMethods(
        INamedTypeSymbol rootType,
        Trie<FluentMethodParameter, ConstructorMetadata>.Node node,
        OrderedDictionary<IParameterSymbol, IFluentValueStorage> valueStorages)
    {
        var candidateFluentMethods =
            node.Children.Values
                .SelectMany(child =>
                    CreateFluentMethods(
                        rootType,
                        node,
                        child.EncounteredKeyParts,
                        ConvertNodeToFluentStep(rootType, child),
                        child.Values,
                        valueStorages))
                .ToImmutableArray();

        var selectedAndIgnoredMethods = ChooseCandidateFluentMethod(candidateFluentMethods);

        var allIgnoredMethods = selectedAndIgnoredMethods
            .SelectMany(pair => pair.IgnoredMethods)
            .ToImmutableHashSet();


        var ignoredMultiMethodWarningFactory = new IgnoredMultiMethodWarningFactory(allIgnoredMethods);

        foreach (var (selectedMethod, ignoredMethods) in selectedAndIgnoredMethods)
        {
            _unreachableConstructorAnalyzer.AddReachableMethod(selectedMethod);
            _diagnostics.AddRange(
                [
                    ..ignoredMultiMethodWarningFactory
                        .Create(
                            selectedMethod,
                            [
                                ..ignoredMethods
                                .Distinct(FluentMethodSignatureEqualityComparer.Default)
                                .OfType<MultiMethod>()
                            ])
                ]);

            yield return selectedMethod;
        }
    }

    private static ImmutableArray<SelectedFluentMethod> ChooseCandidateFluentMethod(ImmutableArray<IFluentMethod> fluentMethods) =>
    [
        ..fluentMethods
            .Distinct()
            .GroupBy(m => m, FluentMethodSignatureEqualityComparer.Default)
            .Select(fluentMethodGroup =>
            {
                var regularMethod = fluentMethodGroup.OfType<RegularMethod>()
                    .OrderBy(m => m.SourceParameter.Name)
                    .FirstOrDefault();

                var selectedMethod = regularMethod ?? fluentMethodGroup.First();

                return new SelectedFluentMethod(
                    selectedMethod,
                    [
                        ..fluentMethodGroup
                            .Where(method => selectedMethod != method)
                    ]);
            })
    ];

    private IFluentStep? ConvertNodeToFluentStep(
        INamedTypeSymbol rootType,
        Trie<FluentMethodParameter, ConstructorMetadata>.Node node)
    {
        var knownConstructorParameters = new ParameterSequence(node.Key);
        var constructorMetadata = node.EndValues.FirstOrDefault();
        var useExistingTypeAsStep = UseExistingTypeAsStep();

        var valueStorages = GetValueStorages();

        var fluentMethods = ConvertNodeToFluentFluentMethods(rootType, node, valueStorages);
        return fluentMethods.Length > 0
            ? CreateStep(valueStorages)
            : null;

        bool UseExistingTypeAsStep()
        {
            if (constructorMetadata is null) return false;

            var containingType = constructorMetadata.Constructor.ContainingType;
            var doNotGenerateCreateMethod = constructorMetadata.Options.HasFlag(NoCreateMethod);

            // TODO: Create Analyzer to check if the target type needs to be partial and instantiatable.
            // the target type needs to be partial and instantiatable.  To is so avoid hiding other
            // constructors that begin with the same build steps, but have additional steps
            return containingType.CanBeCustomStep() && doNotGenerateCreateMethod;
        }

        IFluentStep CreateStep(OrderedDictionary<IParameterSymbol, IFluentValueStorage> storage)
        {
            return (useExistingTypeAsStep, constructorMetadata) switch
            {
                (true, { } metadata) =>
                    new ExistingTypeFluentStep(metadata)
                    {
                        KnownConstructorParameters = knownConstructorParameters,
                        FluentMethods = fluentMethods,
                        ValueStorage = storage,
                        CandidateConstructors =
                        [
                            ..node.Values
                                .SelectMany(value => value.CandidateConstructors)
                                .Distinct<IMethodSymbol>(SymbolEqualityComparer.Default)
                        ]
                    },
                _ =>
                    _regularFluentSteps.GetOrAdd(
                        knownConstructorParameters,
                        () =>
                            new RegularFluentStep(
                                rootType,
                                node.Values
                                    .SelectMany(metadata => metadata.CandidateConstructors)
                                    .Distinct(SymbolEqualityComparer.Default)
                                    .OfType<IMethodSymbol>())
                            {
                                KnownConstructorParameters = knownConstructorParameters,
                                FluentMethods = fluentMethods,
                                IsEndStep = node.IsEnd,
                                ValueStorage = storage
                            })
            };
        }

        OrderedDictionary<IParameterSymbol, IFluentValueStorage> GetValueStorages()
        {
            return (useExistingTypeAsStep, constructorMetadata) switch
            {
                (true, not null and var metadata) => metadata.ValueStorage,
                _ => CreateRegularStepValueStorage(rootType, knownConstructorParameters)
            };
        }
    }

    private static OrderedDictionary<IParameterSymbol, IFluentValueStorage> CreateRegularStepValueStorage(
        INamedTypeSymbol rootType,
        ParameterSequence knownConstructorParameters)
    {
        var parameterStoragePairs =
            from parameter in knownConstructorParameters
            let fieldStorage = new FieldStorage(parameter.Name.ToParameterFieldName(), parameter.Type,
                rootType.ContainingNamespace)
            select new KeyValuePair<IParameterSymbol, IFluentValueStorage>(parameter, fieldStorage);

        return new OrderedDictionary<IParameterSymbol, IFluentValueStorage>(parameterStoragePairs);
    }

    private IEnumerable<IFluentMethod> CreateFluentMethods(
        INamedTypeSymbol rootType,
        Trie<FluentMethodParameter, ConstructorMetadata>.Node node,
        ICollection<FluentMethodParameter> fluentParameterInstances,
        IFluentStep? nextStep,
        IList<ConstructorMetadata> constructorMetadataList,
        OrderedDictionary<IParameterSymbol, IFluentValueStorage> valueStorages)
    {
        var constructorMetadata = MergeConstructorMetadata(node, constructorMetadataList);
        IFluentReturn methodReturn = nextStep switch
        {
            null => new TargetTypeReturn(
                constructorMetadata.Constructor,
                [..constructorMetadata.CandidateConstructors],
                new ParameterSequence(node.Key.Select(p => p.ParameterSymbol))),
            _ => nextStep
        };

        foreach (var parameter in fluentParameterInstances)
        {
            var multipleFluentMethodInfo = compilation
                .GetMultipleFluentMethodSymbols(parameter.ParameterSymbol)
                .ToList();

            ValidateMultipleFluentMethodCompatibility(parameter, multipleFluentMethodInfo);

            var normalizedFluentMethodSymbols = multipleFluentMethodInfo
                .Where(methodInfo => methodInfo.Diagnostics.Count == 0)
                .Select(methodInfo => NormalizedConverterMethod(methodInfo.Method, parameter.ParameterSymbol.Type))
                .ToImmutableArray();

            foreach (var normalizedFluentMethodSymbol in normalizedFluentMethodSymbols)
                yield return new MultiMethod(
                    parameter.ParameterSymbol,
                    methodReturn,
                    rootType.ContainingNamespace,
                    normalizedFluentMethodSymbol,
                    node.Key,
                    valueStorages,
                    normalizedFluentMethodSymbols);

            var hasMultipleFluentMethodsAttribute = parameter.ParameterSymbol
                .GetAttribute(TypeName.MultipleFluentMethodsAttribute) is not null;

            var hasFluentMethodAttribute = parameter.ParameterSymbol
                .GetAttribute(TypeName.FluentMethodAttribute) is not null;

            if (!hasFluentMethodAttribute && hasMultipleFluentMethodsAttribute) continue;

            var fluentParameter = fluentParameterInstances.First();
            foreach (var name in fluentParameter.Names)
                yield return new RegularMethod(
                    name,
                    fluentParameter.ParameterSymbol,
                    methodReturn,
                    rootType.ContainingNamespace,
                    node.Key,
                    valueStorages);
        }
    }

    private void ValidateMultipleFluentMethodCompatibility(FluentMethodParameter parameter,
        List<(IMethodSymbol Method, ICollection<Diagnostic> Diagnostics)> multipleFluentMethodInfo)
    {
        if (multipleFluentMethodInfo.Any()
            && multipleFluentMethodInfo.All(info => info.Diagnostics.Count > 0))
            _diagnostics.AddRange(
            [
                Diagnostic.Create(
                    FluentFactoryGenerator.AllFluentMethodTemplatesIncompatible,
                    parameter.ParameterSymbol
                        .GetAttribute(TypeName.MultipleFluentMethodsAttribute)?
                        .GetLocationAtIndex(0),
                    parameter.ParameterSymbol.ToFullDisplayString()),
            ]);
        else
            _diagnostics.AddRange(multipleFluentMethodInfo
                .SelectMany(info => info.Diagnostics));
    }

    private static IEnumerable<IFluentStep> GetDescendentFluentSteps(IEnumerable<IFluentStep> fluentSteps)
    {
        foreach (var fluentStep in fluentSteps)
        {
            yield return fluentStep;

            var childSteps = fluentStep.FluentMethods
                .Select(m => m.Return)
                .OfType<IFluentStep>();

            foreach (var underlyingFluentStep in GetDescendentFluentSteps(childSteps))
                yield return underlyingFluentStep;
        }
    }

    private static ConstructorMetadata MergeConstructorMetadata(
        Trie<FluentMethodParameter, ConstructorMetadata>.Node node, IList<ConstructorMetadata> constructorMetadataList)
    {
        return constructorMetadataList.Skip(1).Aggregate(constructorMetadataList.First().Clone(), (merged, metadata) =>
        {
            var mergeableConstructors = metadata.CandidateConstructors
                .Except<IMethodSymbol>(merged.CandidateConstructors, SymbolEqualityComparer.Default);

            merged.CandidateConstructors.AddRange(mergeableConstructors);
            merged.Options |= metadata.Options;
            if (metadata.Constructor.Parameters.Length - 1 != node.Key.Length)
                return merged;

            merged.Constructor = metadata.Constructor;

            return merged;
        });
    }

    private IEnumerable<IFluentMethod> ConvertNodeToCreationMethods(INamedTypeSymbol rootType,
        Trie<FluentMethodParameter, ConstructorMetadata>.Node node,
        OrderedDictionary<IParameterSymbol, IFluentValueStorage> valueSources)
    {
        if (!node.IsEnd) yield break;

        var creationMethods =
            from value in node.Values
            where value.Constructor.Parameters.Length == node.Key.Length
            where !value.Options.HasFlag(NoCreateMethod)
            select new CreationMethod(
                rootType.ContainingNamespace,
                value,
                node.Key,
                valueSources);

        foreach (var createMethod in creationMethods)
        {
            _unreachableConstructorAnalyzer.AddReachableMethod(createMethod);
            yield return createMethod;
        }
    }

    private Trie<FluentMethodParameter, ConstructorMetadata> CreateFluentStepTrie(
        ImmutableArray<FluentConstructorContext> fluentConstructorContexts)
    {
        var trie = new Trie<FluentMethodParameter, ConstructorMetadata>();
        foreach (var constructorContext in fluentConstructorContexts)
        {
            var fluentParameters =
                constructorContext.Constructor.Parameters
                    .Select(parameter =>
                    {
                        var methodNames = compilation
                            .GetMultipleFluentMethodSymbols(parameter)
                            .Select(methodInfo => methodInfo.Method.Name)
                            .DefaultIfEmpty(parameter.GetFluentMethodName());

                        return new FluentMethodParameter(parameter, methodNames);
                    });

            trie.Insert(
                fluentParameters,
                new ConstructorMetadata(constructorContext));
        }

        return trie;
    }

    private static IMethodSymbol NormalizedConverterMethod(IMethodSymbol converter, ITypeSymbol targetType)
    {
        var mapping = TypeMapper.MapGenericArguments(converter.ReturnType, targetType);

        return converter.NormalizeMethodTypeParameters(mapping);
    }

    private static ImmutableArray<INamespaceSymbol> GetUsingStatements(
        ImmutableArray<FluentConstructorContext> fluentConstructorContexts)
    {
        return
        [
            ..fluentConstructorContexts
                .SelectMany(ctx => ctx.Constructor.Parameters)
                .Select(parameter => parameter.Type.ContainingNamespace)
                .Concat(fluentConstructorContexts.Select(ctx => ctx.Constructor.ContainingType.ContainingNamespace))
                .Select(namespaceSymbol => (namespaceSymbol, displayString: namespaceSymbol.ToDisplayString()))
                .DistinctBy(ns => ns.displayString)
                .OrderBy(ns => ns.displayString)
                .Select(ns => ns.namespaceSymbol)
        ];
    }

    private record SelectedFluentMethod(IFluentMethod SelectedMethod, ImmutableArray<IFluentMethod> IgnoredMethods)
    {
        public IFluentMethod SelectedMethod { get; } = SelectedMethod;
        public ImmutableArray<IFluentMethod> IgnoredMethods { get; } = IgnoredMethods;
    }
}
