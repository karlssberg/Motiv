namespace Motiv.Tests.MemoIdentity;

/// <summary>The named operands the memo-identity tests compose with each operator.</summary>
internal static class MemoIdentityPropositions
{
    public static PolicyBase<bool, string> Policy(string name) => Spec.Build((bool b) => b).Create(name);

    public static SpecBase<bool, string> Proposition(string name) => Policy(name);

    public static AsyncPolicyBase<bool, string> AsyncPolicy(string name) =>
        Spec.BuildAsync((bool b) => new ValueTask<bool>(b)).Create(name);

    public static AsyncSpecBase<bool, string> AsyncProposition(string name) => AsyncPolicy(name);

    public static ExpressionSpecBase<int, string> ExpressionProposition(string name) =>
        Spec.From((int n) => n > 0).Create(name);

    public static ExpressionPolicyBase<int, string> ExpressionPolicy(string name) =>
        Spec.From((int n) => n > 0).WhenTrue(name).WhenFalse($"not {name}").Create();
}
