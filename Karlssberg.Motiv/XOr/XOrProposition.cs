﻿namespace Karlssberg.Motiv.XOr;

internal sealed class XOrProposition<TModel, TMetadata>(
    SpecBase<TModel, TMetadata> left, 
    SpecBase<TModel, TMetadata> right)
    : IProposition
{

    public string Assertion => $"{Summarize(left)} ^ {Summarize(right)}";
    public string Detailed =>
        $"""
            {Explain(left).IndentAfterFirstLine()} ^
            {Explain(right).IndentAfterFirstLine()}
        """;
    
    private string Summarize(SpecBase<TModel, TMetadata> operand)
    {
        return operand switch 
        {
            XOrSpec<TModel, TMetadata> andSpec => andSpec.Proposition.Assertion,
            ICompositeSpec compositeSpec => $"({compositeSpec.Proposition.Assertion})",
            _ => operand.Proposition.Assertion
        };
    }
    
    private string Explain(SpecBase<TModel, TMetadata> operand)
    {
        return operand switch 
        {
            XOrSpec<TModel, TMetadata> andSpec => andSpec.Proposition.Detailed,
            ICompositeSpec compositeSpec => $"({compositeSpec.Proposition.Detailed})",
            _ => operand.Proposition.Detailed
        };
    }
    
    public override string ToString() => Assertion;
}