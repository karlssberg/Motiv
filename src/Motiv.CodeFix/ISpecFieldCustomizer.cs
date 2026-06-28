using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Motiv.CodeFix;

/// <summary>
///     Customizes how the spec field is declared, initialized, and formatted
///     in the containing class during a code fix conversion.
/// </summary>
internal interface ISpecFieldCustomizer
{
    /// <summary>
    ///     Returns the type syntax for the field declaration.
    /// </summary>
    /// <param name="propositionName">The proposition class name.</param>
    /// <param name="modelTypeName">The model type name, if known.</param>
    /// <returns>The field type as a <see cref="TypeSyntax" />.</returns>
    TypeSyntax GetFieldType(string propositionName, string? modelTypeName);

    /// <summary>
    ///     Returns the initializer expression for a non-instance field (simple case).
    /// </summary>
    /// <param name="propositionName">The proposition class name.</param>
    /// <returns>The initializer expression.</returns>
    ExpressionSyntax GetFieldInitializer(string propositionName);

    /// <summary>
    ///     Returns the expression to assign to the field inside a constructor body
    ///     (instance methods case).
    /// </summary>
    /// <param name="propositionName">The proposition class name.</param>
    /// <returns>The constructor assignment expression.</returns>
    ExpressionSyntax GetConstructorAssignment(string propositionName);

    /// <summary>
    ///     Returns additional using directives needed by this customizer.
    /// </summary>
    /// <returns>Using directive syntax nodes to add.</returns>
    IEnumerable<UsingDirectiveSyntax> GetAdditionalUsings();

    /// <summary>
    ///     Applies customizer-specific formatting to a constructed member
    ///     (e.g., inserting line breaks in fluent chains).
    ///     Called after <c>NormalizeWhitespace()</c>.
    /// </summary>
    /// <param name="member">The member to format.</param>
    /// <param name="lineFeed">The line feed trivia from the source file.</param>
    /// <returns>The formatted member.</returns>
    MemberDeclarationSyntax FormatMember(MemberDeclarationSyntax member, SyntaxTrivia lineFeed);
}
