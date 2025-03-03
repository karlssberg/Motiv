using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Motiv.Generator.FluentBuilder.Analysis;
using Motiv.Generator.FluentBuilder.Generation;
using Motiv.Generator.FluentBuilder.Generation.Shared;
using Motiv.Generator.FluentBuilder.Model.Methods;
using Motiv.Generator.FluentBuilder.Model.Steps;
using Motiv.Generator.FluentBuilder.Model.Storage;
using static Motiv.Generator.FluentBuilder.FluentFactoryGeneratorOptions;

namespace Motiv.Generator.FluentBuilder.Model;

public class FluentModelFactory(Compilation compilation)
{
    private readonly OrderedDictionary<ParameterSequence, RegularFluentStep> _regularFluentSteps = new();

    public FluentFactoryCompilationUnit CreateFluentFactoryCompilationUnit(
        INamedTypeSymbol rootType,
        ImmutableArray<FluentConstructorContext> fluentConstructorContexts)
    {
        _regularFluentSteps.Clear();

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
                if (step is not RegularFluentStep regularFluentStep) return step;

                regularFluentStep.Index = index;

                return step;
            })
            .ToImmutableArray();

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
            IsRecord = sampleConstructorContext.IsRecord
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
        IFluentMethod[] fluentMethods =
        [
            ..
            from child in node.Children.Values
            let nextStep = ConvertNodeToFluentStep(rootType, child)
            let fluentParameters = child.EncounteredKeyParts
            from fluentMethod in CreateFluentMethods(rootType, node, fluentParameters, nextStep, child.Values, valueStorages)
            select fluentMethod
        ];

        var fluentMethodGroups = fluentMethods
            .GroupBy(m => m, FluentMethodSignatureEqualityComparer.Default);

        foreach (var fluentMethodGroup in fluentMethodGroups)
        {
            var nonRegularMethod = fluentMethodGroup.OfType<RegularMethod>()
                .OrderBy(m => m.SourceParameter.Name)
                .FirstOrDefault();

            yield return nonRegularMethod ?? fluentMethodGroup.First();
        }
    }

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

        IFluentStep? CreateStep(OrderedDictionary<IParameterSymbol,IFluentValueStorage> storage)
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
            let fieldStorage = new FieldStorage(parameter.Name.ToParameterFieldName(), parameter.Type, rootType.ContainingNamespace)
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
                new ParameterSequence(node.Key.Select(p => p.ParameterSymbol))),
            _ => nextStep
        };

        foreach (var parameter in fluentParameterInstances)
        {
            var multipleFluentMethodSymbols = compilation
                .GetMultipleFluentMethodSymbols(parameter.ParameterSymbol)
                .Select(method => NormalizedConverterMethod(method, parameter.ParameterSymbol.Type));

            foreach (var multipleFluentMethodSymbol in multipleFluentMethodSymbols)
                yield return new MultiMethod(
                    parameter.ParameterSymbol,
                    methodReturn,
                    rootType.ContainingNamespace,
                    multipleFluentMethodSymbol,
                    node.Key,
                    valueStorages);

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

        var createMethods =
            from value in node.Values
            where value.Constructor.Parameters.Length == node.Key.Length
            where !value.Options.HasFlag(NoCreateMethod)
            select new CreateMethod(
                rootType.ContainingNamespace,
                value.Constructor,
                node.Key,
                valueSources);

        foreach (var createMethod in createMethods) yield return createMethod;
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
                            .Select(symbol => symbol.Name)
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
                .Select(parameter => parameter.Type)
                .Select(type => type.ContainingNamespace)
                .Concat(fluentConstructorContexts.Select(ctx => ctx.Constructor.ContainingType.ContainingNamespace))
                .DistinctBy(ns => ns.ToDisplayString())
                .OrderBy(ns => ns.ToDisplayString())
        ];
    }
}
