using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Motiv.FluentFactory.Generator.Generation.SyntaxElements.Methods;
using Motiv.FluentFactory.Generator.Model;
using Motiv.FluentFactory.Generator.Model.Methods;
using Motiv.FluentFactory.Generator.Model.Steps;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.FluentFactory.Generator.Generation.SyntaxElements;

public static class RootTypeDeclaration
{
    private static readonly SymbolDisplayFormat NameOnlyFormat = new SymbolDisplayFormat(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes)

    .WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);
    public static TypeDeclarationSyntax Create(FluentFactoryCompilationUnit file)
    {
        var rootMethodDeclarations = GetRootMethodDeclarations(file);

        var identifier = Identifier(file.RootType.Name);
        TypeDeclarationSyntax typeDeclaration = file.TypeKind switch
        {
            TypeKind.Struct when file.IsRecord  =>
                RecordDeclaration(SyntaxKind.RecordStructDeclaration, Token(SyntaxKind.StructKeyword), identifier)
                    .WithOpenBraceToken(Token(SyntaxKind.OpenBraceToken))
                    .WithCloseBraceToken(Token(SyntaxKind.CloseBraceToken))
                    .WithModifiers(
                        TokenList(GetRootTypeModifiers(file).Append(Token(SyntaxKind.RecordKeyword))))
                    .WithTypeParameterList(CreateTypeParameterList(file.RootType))
                    .WithConstraintClauses(CreateTypeParameterConstraints(file.RootType)),

            TypeKind.Struct =>
                StructDeclaration(identifier)
                    .WithModifiers(
                        TokenList(GetRootTypeModifiers(file)))
                    .WithTypeParameterList(CreateTypeParameterList(file.RootType))
                    .WithConstraintClauses(CreateTypeParameterConstraints(file.RootType)),

            TypeKind.Class when file.IsRecord =>
                RecordDeclaration(SyntaxKind.RecordDeclaration, Token(SyntaxKind.RecordKeyword), identifier)
                    .WithOpenBraceToken(Token(SyntaxKind.OpenBraceToken))
                    .WithCloseBraceToken(Token(SyntaxKind.CloseBraceToken))
                    .WithModifiers(
                        TokenList(GetRootTypeModifiers(file)))
                    .WithTypeParameterList(CreateTypeParameterList(file.RootType))
                    .WithConstraintClauses(CreateTypeParameterConstraints(file.RootType)),

            _ =>
                ClassDeclaration(identifier)
                    .WithModifiers(
                        TokenList(GetRootTypeModifiers(file)))
                    .WithTypeParameterList(CreateTypeParameterList(file.RootType))
                    .WithConstraintClauses(CreateTypeParameterConstraints(file.RootType))
        };

        return typeDeclaration.WithMembers(
            List(rootMethodDeclarations.OfType<MemberDeclarationSyntax>()));
    }

    private static TypeParameterListSyntax? CreateTypeParameterList(INamedTypeSymbol rootType)
    {
        if (!rootType.IsGenericType || rootType.TypeParameters.Length == 0)
            return null;

        var typeParameters = rootType.TypeParameters
            .Select(tp => TypeParameter(tp.Name))
            .ToArray();

        return TypeParameterList(SeparatedList(typeParameters));
    }

    private static SyntaxList<TypeParameterConstraintClauseSyntax> CreateTypeParameterConstraints(INamedTypeSymbol rootType)
    {
        if (!rootType.IsGenericType || rootType.TypeParameters.Length == 0)
            return List<TypeParameterConstraintClauseSyntax>();

        var constraintClauses = rootType.TypeParameters
            .Where(tp => tp.HasConstructorConstraint || tp.HasReferenceTypeConstraint || tp.HasValueTypeConstraint || tp.ConstraintTypes.Length > 0)
            .Select(tp =>
            {
                var constraints = new List<TypeParameterConstraintSyntax>();

                // Add reference type constraint (class)
                if (tp.HasReferenceTypeConstraint)
                    constraints.Add(ClassOrStructConstraint(SyntaxKind.ClassConstraint));

                // Add value type constraint (struct)
                if (tp.HasValueTypeConstraint)
                    constraints.Add(ClassOrStructConstraint(SyntaxKind.StructConstraint));

                // Add type constraints
                foreach (var constraintType in tp.ConstraintTypes)
                {
                    var typeName = constraintType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat
                        .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted));
                    constraints.Add(TypeConstraint(ParseTypeName(typeName)));
                }

                // Add constructor constraint (new())
                if (tp.HasConstructorConstraint)
                    constraints.Add(ConstructorConstraint());

                return TypeParameterConstraintClause(tp.Name)
                    .WithConstraints(SeparatedList(constraints));
            })
            .ToArray();

        return List(constraintClauses);
    }

    private static IEnumerable<SyntaxToken> GetRootTypeModifiers(FluentFactoryCompilationUnit file)
    {
        foreach (var syntaxKind in file.Accessibility.AccessibilityToSyntaxKind())
        {
            yield return Token(syntaxKind);
        }
        if (file.IsStatic)
        {
            yield return Token(SyntaxKind.StaticKeyword);
        }
        yield return Token(SyntaxKind.PartialKeyword);
    }

    private static IEnumerable<MethodDeclarationSyntax> GetRootMethodDeclarations(FluentFactoryCompilationUnit file)
    {
        return file.FluentMethods
            .Select<IFluentMethod, MethodDeclarationSyntax>(method => method switch
            {
                { Return: TargetTypeReturn } => FluentRootFactoryMethodDeclaration.Create(file.RootType.ContainingNamespace, method, file.RootType),
                MultiMethod multiMethod => FluentStepMethodDeclaration.Create(multiMethod, [], file.RootType.ContainingNamespace, file.RootType.TypeParameters),
                _ => FluentStepMethodDeclaration.Create(method, [], file.RootType.ContainingNamespace, file.RootType.TypeParameters)
            })
            .Select(method =>
            {
                return method
                    .WithModifiers(
                        TokenList(GetSyntaxTokens()));

                IEnumerable<SyntaxToken> GetSyntaxTokens()
                {
                    yield return Token(SyntaxKind.PublicKeyword);
                    yield return Token(SyntaxKind.StaticKeyword);
                }
            });
    }
}
