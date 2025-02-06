using Motiv.BooleanPredicateProposition.PropositionBuilders;
using Motiv.BooleanPredicateProposition.PropositionBuilders.Explanation;
using Motiv.BooleanPredicateProposition.PropositionBuilders.Metadata;
using Motiv.BooleanResultPredicateProposition.PropositionBuilders;
using Motiv.BooleanResultPredicateProposition.PropositionBuilders.Explanation;
using Motiv.BooleanResultPredicateProposition.PropositionBuilders.Metadata;
using Motiv.ExpressionTreeProposition.PropositionBuilders;
using Motiv.ExpressionTreeProposition.PropositionBuilders.Explanation;
using Motiv.ExpressionTreeProposition.PropositionBuilders.Metadata;
using Motiv.HigherOrderProposition.PropositionBuilders;
using Motiv.HigherOrderProposition.PropositionBuilders.Explanation.BooleanPredicate;
using Motiv.HigherOrderProposition.PropositionBuilders.Explanation.BooleanPredicateWithName;
using Motiv.HigherOrderProposition.PropositionBuilders.Explanation.BooleanResultPredicate;
using Motiv.HigherOrderProposition.PropositionBuilders.Explanation.BooleanResultPredicateWithName;
using Motiv.HigherOrderProposition.PropositionBuilders.Explanation.ExpressionTree;
using Motiv.HigherOrderProposition.PropositionBuilders.Explanation.Policy;
using Motiv.HigherOrderProposition.PropositionBuilders.Explanation.PolicyResultPredicate;
using Motiv.HigherOrderProposition.PropositionBuilders.Explanation.PolicyResultPredicateWithName;
using Motiv.HigherOrderProposition.PropositionBuilders.Explanation.Spec;
using Motiv.HigherOrderProposition.PropositionBuilders.Metadata.BooleanPredicate;
using Motiv.HigherOrderProposition.PropositionBuilders.Metadata.BooleanResultPredicate;
using Motiv.HigherOrderProposition.PropositionBuilders.Metadata.ExpressionTree;
using Motiv.HigherOrderProposition.PropositionBuilders.Metadata.Policy;
using Motiv.HigherOrderProposition.PropositionBuilders.Metadata.PolicyResultPredicate;
using Motiv.HigherOrderProposition.PropositionBuilders.Metadata.Spec;
using Motiv.SpecDecoratorProposition.PropositionBuilders.Explanation;
using Motiv.SpecDecoratorProposition.PropositionBuilders.Metadata;
using System;
using System.Linq.Expressions;

namespace Motiv.BooleanPredicateProposition.PropositionBuilders
{
    public partial struct BooleanPredicatePropositionFactory<TModel>
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_1__Motiv_Spec<TModel> WhenTrue(in System.Func<TModel, string> whenTrue)
        {
            return new Step_1__Motiv_Spec<TModel>(predicate, whenTrue);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_1__Motiv_Spec<TModel> WhenTrue(in string whenTrue)
        {
            return new Step_1__Motiv_Spec<TModel>(predicate, whenTrue);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_2__Motiv_Spec<TModel> WhenTrue(in string trueBecause)
        {
            return new Step_2__Motiv_Spec<TModel>(predicate, trueBecause);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_3__Motiv_Spec<TModel> WhenTrueYield(in System.Func<TModel, System.Collections.Generic.IEnumerable<string>> whenTrue)
        {
            return new Step_3__Motiv_Spec<TModel>(predicate, whenTrue);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_4__Motiv_Spec<TModel, TMetadata> WhenTrue<TMetadata>(in System.Func<TModel, TMetadata> whenTrue)
        {
            return new Step_4__Motiv_Spec<TModel, TMetadata>(predicate, whenTrue);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_4__Motiv_Spec<TModel, TMetadata> WhenTrue<TMetadata>(in TMetadata whenTrue)
        {
            return new Step_4__Motiv_Spec<TModel, TMetadata>(predicate, whenTrue);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_5__Motiv_Spec<TModel, TMetadata> WhenTrueYield<TMetadata>(in System.Func<TModel, System.Collections.Generic.IEnumerable<TMetadata>> whenTrue)
        {
            return new Step_5__Motiv_Spec<TModel, TMetadata>(predicate, whenTrue);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Metadata.BooleanPredicate.TrueHigherOrderFromBooleanPredicatePropositionFactory<TModel> As(in System.Func<System.Collections.Generic.IEnumerable<HigherOrderProposition.ModelResult<TModel>>, bool> higherOrderPredicate, in System.Func<bool, System.Collections.Generic.IEnumerable<HigherOrderProposition.ModelResult<TModel>>, System.Collections.Generic.IEnumerable<HigherOrderProposition.ModelResult<TModel>>> causeSelector)
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Metadata.BooleanPredicate.TrueHigherOrderFromBooleanPredicatePropositionFactory<TModel>(predicate, higherOrderPredicate, causeSelector);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Metadata.BooleanPredicate.TrueHigherOrderFromBooleanPredicatePropositionFactory<TModel> As(in System.Func<System.Collections.Generic.IEnumerable<HigherOrderProposition.ModelResult<TModel>>, bool> higherOrderPredicate)
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Metadata.BooleanPredicate.TrueHigherOrderFromBooleanPredicatePropositionFactory<TModel>(predicate, higherOrderPredicate);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Metadata.BooleanPredicate.TrueHigherOrderFromBooleanPredicatePropositionFactory<TModel> AsAllSatisfied()
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Metadata.BooleanPredicate.TrueHigherOrderFromBooleanPredicatePropositionFactory<TModel>(predicate);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Metadata.BooleanPredicate.TrueHigherOrderFromBooleanPredicatePropositionFactory<TModel> AsAnySatisfied()
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Metadata.BooleanPredicate.TrueHigherOrderFromBooleanPredicatePropositionFactory<TModel>(predicate);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Metadata.BooleanPredicate.TrueHigherOrderFromBooleanPredicatePropositionFactory<TModel> AsAtLeastNSatisfied(in int n)
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Metadata.BooleanPredicate.TrueHigherOrderFromBooleanPredicatePropositionFactory<TModel>(predicate, n);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Metadata.BooleanPredicate.TrueHigherOrderFromBooleanPredicatePropositionFactory<TModel> AsAtMostNSatisfied(in int n)
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Metadata.BooleanPredicate.TrueHigherOrderFromBooleanPredicatePropositionFactory<TModel>(predicate, n);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Metadata.BooleanPredicate.TrueHigherOrderFromBooleanPredicatePropositionFactory<TModel> AsNoneSatisfied()
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Metadata.BooleanPredicate.TrueHigherOrderFromBooleanPredicatePropositionFactory<TModel>(predicate);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Metadata.BooleanPredicate.TrueHigherOrderFromBooleanPredicatePropositionFactory<TModel> AsNSatisfied(in int n)
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Metadata.BooleanPredicate.TrueHigherOrderFromBooleanPredicatePropositionFactory<TModel>(predicate, n);
        }
    }
}

namespace Motiv
{
    public static partial class Spec
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static BooleanPredicateProposition.PropositionBuilders.BooleanPredicatePropositionFactory<TModel> Build<TModel>(in System.Func<TModel, bool> predicate)
        {
            return new BooleanPredicateProposition.PropositionBuilders.BooleanPredicatePropositionFactory<TModel>(predicate);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static BooleanResultPredicateProposition.PropositionBuilders.BooleanResultPredicatePropositionFactory<TModel, TMetadata> Build<TModel, TMetadata>(in System.Func<TModel, BooleanResultBase<TMetadata>> resultFactory)
        {
            return new BooleanResultPredicateProposition.PropositionBuilders.BooleanResultPredicatePropositionFactory<TModel, TMetadata>(Shared.BooleanResultBuildOverloads.Build<TModel, TMetadata>(resultFactory));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static BooleanResultPredicateProposition.PropositionBuilders.BooleanResultPredicatePropositionFactory<TModel, TMetadata> Build<TModel>(in System.Func<TModel, BooleanResultBase<string>> resultFactory)
        {
            return new BooleanResultPredicateProposition.PropositionBuilders.BooleanResultPredicatePropositionFactory<TModel, TMetadata>(Shared.BooleanResultBuildOverloads.Build<TModel>(resultFactory));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static BooleanResultPredicateProposition.PropositionBuilders.PolicyResultPredicatePropositionFactory<TModel, TMetadata> Build<TModel, TMetadata>(in System.Func<TModel, PolicyResultBase<TMetadata>> resultFactory)
        {
            return new BooleanResultPredicateProposition.PropositionBuilders.PolicyResultPredicatePropositionFactory<TModel, TMetadata>(Shared.PolicyResultBuildOverloads.Build<TModel, TMetadata>(resultFactory));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static BooleanResultPredicateProposition.PropositionBuilders.PolicyResultPredicatePropositionFactory<TModel, TMetadata> Build<TModel>(in System.Func<TModel, PolicyResultBase<string>> resultFactory)
        {
            return new BooleanResultPredicateProposition.PropositionBuilders.PolicyResultPredicatePropositionFactory<TModel, TMetadata>(Shared.PolicyResultBuildOverloads.Build<TModel>(resultFactory));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static ExpressionTreeProposition.PropositionBuilders.MinimalExpressionTreePropositionFactory<TModel, TPredicateResult> From<TModel, TPredicateResult>(in System.Linq.Expressions.Expression<System.Func<TModel, TPredicateResult>> expression)
        {
            return new ExpressionTreeProposition.PropositionBuilders.MinimalExpressionTreePropositionFactory<TModel, TPredicateResult>(expression);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static SpecDecoratorProposition.PropositionBuilders.Explanation.MinimalPolicyDecoratorFactory<TModel, TMetadata> Build<TModel, TMetadata>(in PolicyBase<TModel, TMetadata> policy)
        {
            return new SpecDecoratorProposition.PropositionBuilders.Explanation.MinimalPolicyDecoratorFactory<TModel, TMetadata>(Shared.PolicyBuildOverloads.Build<TModel, TMetadata>(policy));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static SpecDecoratorProposition.PropositionBuilders.Explanation.MinimalPolicyDecoratorFactory<TModel, TMetadata> Build<TModel, TMetadata>(in System.Func<PolicyBase<TModel, TMetadata>> policyFactory)
        {
            return new SpecDecoratorProposition.PropositionBuilders.Explanation.MinimalPolicyDecoratorFactory<TModel, TMetadata>(Shared.PolicyBuildOverloads.Build<TModel, TMetadata>(policyFactory));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static SpecDecoratorProposition.PropositionBuilders.Explanation.MinimalPolicyDecoratorFactory<TModel, TMetadata> Build<TModel>(in PolicyBase<TModel, string> policy)
        {
            return new SpecDecoratorProposition.PropositionBuilders.Explanation.MinimalPolicyDecoratorFactory<TModel, TMetadata>(Shared.PolicyBuildOverloads.Build<TModel>(policy));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static SpecDecoratorProposition.PropositionBuilders.Explanation.MinimalPolicyDecoratorFactory<TModel, TMetadata> Build<TModel>(in System.Func<PolicyBase<TModel, string>> policyFactory)
        {
            return new SpecDecoratorProposition.PropositionBuilders.Explanation.MinimalPolicyDecoratorFactory<TModel, TMetadata>(Shared.PolicyBuildOverloads.Build<TModel>(policyFactory));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static SpecDecoratorProposition.PropositionBuilders.Explanation.MinimalPolicyDecoratorFactory<TModel, TMetadata> Build<TModel, TMetadata>(in SpecBase<TModel, TMetadata> spec)
        {
            return new SpecDecoratorProposition.PropositionBuilders.Explanation.MinimalPolicyDecoratorFactory<TModel, TMetadata>(Shared.SpecBuildOverloads.Build<TModel, TMetadata>(spec));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static SpecDecoratorProposition.PropositionBuilders.Explanation.MinimalPolicyDecoratorFactory<TModel, TMetadata> Build<TModel, TMetadata>(in System.Func<SpecBase<TModel, TMetadata>> specFactory)
        {
            return new SpecDecoratorProposition.PropositionBuilders.Explanation.MinimalPolicyDecoratorFactory<TModel, TMetadata>(Shared.SpecBuildOverloads.Build<TModel, TMetadata>(specFactory));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static SpecDecoratorProposition.PropositionBuilders.Explanation.MinimalPolicyDecoratorFactory<TModel, TMetadata> Build<TModel>(in SpecBase<TModel, string> spec)
        {
            return new SpecDecoratorProposition.PropositionBuilders.Explanation.MinimalPolicyDecoratorFactory<TModel, TMetadata>(Shared.SpecBuildOverloads.Build<TModel>(spec));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static SpecDecoratorProposition.PropositionBuilders.Explanation.MinimalPolicyDecoratorFactory<TModel, TMetadata> Build<TModel>(in System.Func<SpecBase<TModel, string>> specFactory)
        {
            return new SpecDecoratorProposition.PropositionBuilders.Explanation.MinimalPolicyDecoratorFactory<TModel, TMetadata>(Shared.SpecBuildOverloads.Build<TModel>(specFactory));
        }
    }

    public struct Step_1__Motiv_Spec<TModel>
    {
        private readonly System.Func<TModel, bool> _predicate__parameter;
        private readonly System.Func<TModel, string> _whenTrue__parameter;
        public Step_1__Motiv_Spec(in System.Func<TModel, bool> predicate, in System.Func<TModel, string> whenTrue)
        {
            this._predicate__parameter = predicate;
            this._whenTrue__parameter = whenTrue;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public BooleanPredicateProposition.PropositionBuilders.Explanation.ExplanationPropositionFactory<TModel> WhenFalse(in System.Func<TModel, string> whenFalse)
        {
            return new BooleanPredicateProposition.PropositionBuilders.Explanation.ExplanationPropositionFactory<TModel>(this._predicate__parameter, this._whenTrue__parameter, BooleanPredicateProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, string>(whenFalse));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public BooleanPredicateProposition.PropositionBuilders.Explanation.ExplanationPropositionFactory<TModel> WhenFalse(in string whenFalse)
        {
            return new BooleanPredicateProposition.PropositionBuilders.Explanation.ExplanationPropositionFactory<TModel>(this._predicate__parameter, this._whenTrue__parameter, BooleanPredicateProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, string>(whenFalse));
        }
    }

    public struct Step_2__Motiv_Spec<TModel>
    {
        private readonly System.Func<TModel, bool> _predicate__parameter;
        private readonly string _trueBecause__parameter;
        public Step_2__Motiv_Spec(in System.Func<TModel, bool> predicate, in string trueBecause)
        {
            this._predicate__parameter = predicate;
            this._trueBecause__parameter = trueBecause;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public BooleanPredicateProposition.PropositionBuilders.Explanation.ExplanationWithNamePropositionFactory<TModel> WhenFalse(in System.Func<TModel, string> whenFalse)
        {
            return new BooleanPredicateProposition.PropositionBuilders.Explanation.ExplanationWithNamePropositionFactory<TModel>(this._predicate__parameter, this._trueBecause__parameter, BooleanPredicateProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, string>(whenFalse));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public BooleanPredicateProposition.PropositionBuilders.Explanation.ExplanationWithNamePropositionFactory<TModel> WhenFalse(in string whenFalse)
        {
            return new BooleanPredicateProposition.PropositionBuilders.Explanation.ExplanationWithNamePropositionFactory<TModel>(this._predicate__parameter, this._trueBecause__parameter, BooleanPredicateProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, string>(whenFalse));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public BooleanPredicateProposition.PropositionBuilders.Explanation.MultiAssertionExplanationWithNamePropositionFactory<TModel> WhenFalseYield(in System.Func<TModel, System.Collections.Generic.IEnumerable<string>> whenFalse)
        {
            return new BooleanPredicateProposition.PropositionBuilders.Explanation.MultiAssertionExplanationWithNamePropositionFactory<TModel>(this._predicate__parameter, this._trueBecause__parameter, BooleanPredicateProposition.PropositionBuilders.WhenFalseYieldOverloads.WhenFalseYield<TModel, string>(whenFalse));
        }
    }

    public struct Step_3__Motiv_Spec<TModel>
    {
        private readonly System.Func<TModel, bool> _predicate__parameter;
        private readonly System.Func<TModel, System.Collections.Generic.IEnumerable<string>> _whenTrue__parameter;
        public Step_3__Motiv_Spec(in System.Func<TModel, bool> predicate, in System.Func<TModel, System.Collections.Generic.IEnumerable<string>> whenTrue)
        {
            this._predicate__parameter = predicate;
            this._whenTrue__parameter = whenTrue;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public BooleanPredicateProposition.PropositionBuilders.Explanation.MultiAssertionExplanationPropositionFactory<TModel> WhenFalseYield(in System.Func<TModel, System.Collections.Generic.IEnumerable<string>> whenFalse)
        {
            return new BooleanPredicateProposition.PropositionBuilders.Explanation.MultiAssertionExplanationPropositionFactory<TModel>(this._predicate__parameter, this._whenTrue__parameter, BooleanPredicateProposition.PropositionBuilders.WhenFalseYieldOverloads.WhenFalseYield<TModel, string>(whenFalse));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public BooleanPredicateProposition.PropositionBuilders.Explanation.MultiAssertionExplanationPropositionFactory<TModel> WhenFalse(in System.Func<TModel, string> whenFalse)
        {
            return new BooleanPredicateProposition.PropositionBuilders.Explanation.MultiAssertionExplanationPropositionFactory<TModel>(this._predicate__parameter, this._whenTrue__parameter, BooleanPredicateProposition.PropositionBuilders.WhenFalseYieldOverloads.WhenFalse<TModel, string>(whenFalse));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public BooleanPredicateProposition.PropositionBuilders.Explanation.MultiAssertionExplanationPropositionFactory<TModel> WhenFalse(in string whenFalse)
        {
            return new BooleanPredicateProposition.PropositionBuilders.Explanation.MultiAssertionExplanationPropositionFactory<TModel>(this._predicate__parameter, this._whenTrue__parameter, BooleanPredicateProposition.PropositionBuilders.WhenFalseYieldOverloads.WhenFalse<TModel, string>(whenFalse));
        }
    }

    public struct Step_4__Motiv_Spec<TModel, TMetadata>
    {
        private readonly System.Func<TModel, bool> _predicate__parameter;
        private readonly System.Func<TModel, TMetadata> _whenTrue__parameter;
        public Step_4__Motiv_Spec(in System.Func<TModel, bool> predicate, in System.Func<TModel, TMetadata> whenTrue)
        {
            this._predicate__parameter = predicate;
            this._whenTrue__parameter = whenTrue;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public BooleanPredicateProposition.PropositionBuilders.Metadata.MetadataPropositionFactory<TModel, TMetadata> WhenFalse(in System.Func<TModel, TMetadata> whenFalse)
        {
            return new BooleanPredicateProposition.PropositionBuilders.Metadata.MetadataPropositionFactory<TModel, TMetadata>(this._predicate__parameter, this._whenTrue__parameter, BooleanPredicateProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, TMetadata>(whenFalse));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public BooleanPredicateProposition.PropositionBuilders.Metadata.MetadataPropositionFactory<TModel, TMetadata> WhenFalse(in TMetadata whenFalse)
        {
            return new BooleanPredicateProposition.PropositionBuilders.Metadata.MetadataPropositionFactory<TModel, TMetadata>(this._predicate__parameter, this._whenTrue__parameter, BooleanPredicateProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, TMetadata>(whenFalse));
        }
    }

    public struct Step_5__Motiv_Spec<TModel, TMetadata>
    {
        private readonly System.Func<TModel, bool> _predicate__parameter;
        private readonly System.Func<TModel, System.Collections.Generic.IEnumerable<TMetadata>> _whenTrue__parameter;
        public Step_5__Motiv_Spec(in System.Func<TModel, bool> predicate, in System.Func<TModel, System.Collections.Generic.IEnumerable<TMetadata>> whenTrue)
        {
            this._predicate__parameter = predicate;
            this._whenTrue__parameter = whenTrue;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public BooleanPredicateProposition.PropositionBuilders.Metadata.MultiMetadataPropositionFactory<TModel, TMetadata> WhenFalseYield(in System.Func<TModel, System.Collections.Generic.IEnumerable<TMetadata>> whenFalse)
        {
            return new BooleanPredicateProposition.PropositionBuilders.Metadata.MultiMetadataPropositionFactory<TModel, TMetadata>(this._predicate__parameter, this._whenTrue__parameter, BooleanPredicateProposition.PropositionBuilders.WhenFalseYieldOverloads.WhenFalseYield<TModel, TMetadata>(whenFalse));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public BooleanPredicateProposition.PropositionBuilders.Metadata.MultiMetadataPropositionFactory<TModel, TMetadata> WhenFalse(in System.Func<TModel, TMetadata> whenFalse)
        {
            return new BooleanPredicateProposition.PropositionBuilders.Metadata.MultiMetadataPropositionFactory<TModel, TMetadata>(this._predicate__parameter, this._whenTrue__parameter, BooleanPredicateProposition.PropositionBuilders.WhenFalseYieldOverloads.WhenFalse<TModel, TMetadata>(whenFalse));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public BooleanPredicateProposition.PropositionBuilders.Metadata.MultiMetadataPropositionFactory<TModel, TMetadata> WhenFalse(in TMetadata whenFalse)
        {
            return new BooleanPredicateProposition.PropositionBuilders.Metadata.MultiMetadataPropositionFactory<TModel, TMetadata>(this._predicate__parameter, this._whenTrue__parameter, BooleanPredicateProposition.PropositionBuilders.WhenFalseYieldOverloads.WhenFalse<TModel, TMetadata>(whenFalse));
        }
    }

    public struct Step_7__Motiv_Spec<TModel>
    {
        private readonly System.Func<TModel, bool> _predicate__parameter;
        private readonly HigherOrderProposition.PropositionBuilders.HigherOrderSpecBooleanPredicateOperation<TModel> _higherOrderOperation__parameter;
        private readonly string _trueBecause__parameter;
        public Step_7__Motiv_Spec(in System.Func<TModel, bool> predicate, in Motiv.HigherOrderProposition.PropositionBuilders.HigherOrderSpecBooleanPredicateOperation<TModel> higherOrderOperation, in string trueBecause)
        {
            this._predicate__parameter = predicate;
            this._higherOrderOperation__parameter = higherOrderOperation;
            this._trueBecause__parameter = trueBecause;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public HigherOrderProposition.PropositionBuilders.Explanation.BooleanPredicateWithName.MultiAssertionExplanationWithNameHigherOrderPropositionFactory<TModel> WhenFalseYield(in System.Func<HigherOrderProposition.HigherOrderBooleanEvaluation<TModel>, System.Collections.Generic.IEnumerable<string>> function)
        {
            return new HigherOrderProposition.PropositionBuilders.Explanation.BooleanPredicateWithName.MultiAssertionExplanationWithNameHigherOrderPropositionFactory<TModel>(this._predicate__parameter, this._higherOrderOperation__parameter, this._trueBecause__parameter, HigherOrderProposition.PropositionBuilders.WhenFalseYieldOverloads.WhenFalseYield<HigherOrderProposition.HigherOrderBooleanEvaluation<TModel>, string>(function));
        }
    }

    public struct Step_8__Motiv_Spec<TModel, TMetadata>
    {
        private readonly System.Func<TModel, bool> _resultResolver__parameter;
        private readonly HigherOrderProposition.PropositionBuilders.HigherOrderSpecBooleanPredicateOperation<TModel> _higherOrderOperation__parameter;
        private readonly System.Func<HigherOrderProposition.HigherOrderBooleanEvaluation<TModel>, System.Collections.Generic.IEnumerable<TMetadata>> _whenTrue__parameter;
        public Step_8__Motiv_Spec(in System.Func<TModel, bool> resultResolver, in Motiv.HigherOrderProposition.PropositionBuilders.HigherOrderSpecBooleanPredicateOperation<TModel> higherOrderOperation, in System.Func<Motiv.HigherOrderProposition.HigherOrderBooleanEvaluation<TModel>, System.Collections.Generic.IEnumerable<TMetadata>> whenTrue)
        {
            this._resultResolver__parameter = resultResolver;
            this._higherOrderOperation__parameter = higherOrderOperation;
            this._whenTrue__parameter = whenTrue;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public HigherOrderProposition.PropositionBuilders.Metadata.BooleanPredicate.MultiMetadataFromBooleanHigherOrderPropositionFactory<TModel, TMetadata> WhenFalseYield(in System.Func<HigherOrderProposition.HigherOrderBooleanEvaluation<TModel>, System.Collections.Generic.IEnumerable<TMetadata>> function)
        {
            return new HigherOrderProposition.PropositionBuilders.Metadata.BooleanPredicate.MultiMetadataFromBooleanHigherOrderPropositionFactory<TModel, TMetadata>(this._resultResolver__parameter, this._higherOrderOperation__parameter, this._whenTrue__parameter, HigherOrderProposition.PropositionBuilders.WhenFalseYieldOverloads.WhenFalseYield<HigherOrderProposition.HigherOrderBooleanEvaluation<TModel>, TMetadata>(function));
        }
    }

    public struct Step_10__Motiv_Spec<TModel, TMetadata>
    {
        private readonly System.Func<TModel, BooleanResultBase<TMetadata>> _predicate__parameter;
        private readonly System.Func<TModel, BooleanResultBase<TMetadata>, string> _trueBecause__parameter;
        public Step_10__Motiv_Spec(in System.Func<TModel, Motiv.BooleanResultBase<TMetadata>> predicate, in System.Func<TModel, Motiv.BooleanResultBase<TMetadata>, string> trueBecause)
        {
            this._predicate__parameter = predicate;
            this._trueBecause__parameter = trueBecause;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public BooleanResultPredicateProposition.PropositionBuilders.Explanation.ExplanationPropositionFactory<TModel, TMetadata> WhenFalse(in System.Func<TModel, BooleanResultBase<TMetadata>, string> whenFalse)
        {
            return new BooleanResultPredicateProposition.PropositionBuilders.Explanation.ExplanationPropositionFactory<TModel, TMetadata>(this._predicate__parameter, this._trueBecause__parameter, BooleanResultPredicateProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, BooleanResultBase<TMetadata>, string>(whenFalse));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public BooleanResultPredicateProposition.PropositionBuilders.Explanation.ExplanationPropositionFactory<TModel, TMetadata> WhenFalse(in System.Func<TModel, string> whenFalse)
        {
            return new BooleanResultPredicateProposition.PropositionBuilders.Explanation.ExplanationPropositionFactory<TModel, TMetadata>(this._predicate__parameter, this._trueBecause__parameter, BooleanResultPredicateProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, BooleanResultBase<TMetadata>, string>(whenFalse));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public BooleanResultPredicateProposition.PropositionBuilders.Explanation.ExplanationPropositionFactory<TModel, TMetadata> WhenFalse(in string whenFalse)
        {
            return new BooleanResultPredicateProposition.PropositionBuilders.Explanation.ExplanationPropositionFactory<TModel, TMetadata>(this._predicate__parameter, this._trueBecause__parameter, BooleanResultPredicateProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, BooleanResultBase<TMetadata>, string>(whenFalse));
        }
    }

    public struct Step_11__Motiv_Spec<TModel, TMetadata>
    {
        private readonly System.Func<TModel, BooleanResultBase<TMetadata>> _predicate__parameter;
        private readonly string _trueBecause__parameter;
        public Step_11__Motiv_Spec(in System.Func<TModel, Motiv.BooleanResultBase<TMetadata>> predicate, in string trueBecause)
        {
            this._predicate__parameter = predicate;
            this._trueBecause__parameter = trueBecause;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public BooleanResultPredicateProposition.PropositionBuilders.Explanation.ExplanationWithNamePropositionFactory<TModel, TMetadata> WhenFalse(in System.Func<TModel, BooleanResultBase<TMetadata>, string> whenFalse)
        {
            return new BooleanResultPredicateProposition.PropositionBuilders.Explanation.ExplanationWithNamePropositionFactory<TModel, TMetadata>(this._predicate__parameter, this._trueBecause__parameter, BooleanResultPredicateProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, BooleanResultBase<TMetadata>, string>(whenFalse));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public BooleanResultPredicateProposition.PropositionBuilders.Explanation.ExplanationWithNamePropositionFactory<TModel, TMetadata> WhenFalse(in System.Func<TModel, string> whenFalse)
        {
            return new BooleanResultPredicateProposition.PropositionBuilders.Explanation.ExplanationWithNamePropositionFactory<TModel, TMetadata>(this._predicate__parameter, this._trueBecause__parameter, BooleanResultPredicateProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, BooleanResultBase<TMetadata>, string>(whenFalse));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public BooleanResultPredicateProposition.PropositionBuilders.Explanation.ExplanationWithNamePropositionFactory<TModel, TMetadata> WhenFalse(in string whenFalse)
        {
            return new BooleanResultPredicateProposition.PropositionBuilders.Explanation.ExplanationWithNamePropositionFactory<TModel, TMetadata>(this._predicate__parameter, this._trueBecause__parameter, BooleanResultPredicateProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, BooleanResultBase<TMetadata>, string>(whenFalse));
        }
    }

    public struct Step_12__Motiv_Spec<TModel, TMetadata, TReplacementMetadata>
    {
        private readonly System.Func<TModel, BooleanResultBase<TMetadata>> _spec__parameter;
        private readonly System.Func<TModel, BooleanResultBase<TMetadata>, TReplacementMetadata> _whenTrue__parameter;
        public Step_12__Motiv_Spec(in System.Func<TModel, Motiv.BooleanResultBase<TMetadata>> spec, in System.Func<TModel, Motiv.BooleanResultBase<TMetadata>, TReplacementMetadata> whenTrue)
        {
            this._spec__parameter = spec;
            this._whenTrue__parameter = whenTrue;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public BooleanResultPredicateProposition.PropositionBuilders.Metadata.MetadataPropositionFactory<TModel, TReplacementMetadata, TMetadata> WhenFalse(in System.Func<TModel, BooleanResultBase<TMetadata>, TReplacementMetadata> whenFalse)
        {
            return new BooleanResultPredicateProposition.PropositionBuilders.Metadata.MetadataPropositionFactory<TModel, TReplacementMetadata, TMetadata>(this._spec__parameter, this._whenTrue__parameter, BooleanResultPredicateProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, BooleanResultBase<TMetadata>, TReplacementMetadata>(whenFalse));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public BooleanResultPredicateProposition.PropositionBuilders.Metadata.MetadataPropositionFactory<TModel, TReplacementMetadata, TMetadata> WhenFalse(in System.Func<TModel, TReplacementMetadata> whenFalse)
        {
            return new BooleanResultPredicateProposition.PropositionBuilders.Metadata.MetadataPropositionFactory<TModel, TReplacementMetadata, TMetadata>(this._spec__parameter, this._whenTrue__parameter, BooleanResultPredicateProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, BooleanResultBase<TMetadata>, TReplacementMetadata>(whenFalse));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public BooleanResultPredicateProposition.PropositionBuilders.Metadata.MetadataPropositionFactory<TModel, TReplacementMetadata, TMetadata> WhenFalse(in TReplacementMetadata whenFalse)
        {
            return new BooleanResultPredicateProposition.PropositionBuilders.Metadata.MetadataPropositionFactory<TModel, TReplacementMetadata, TMetadata>(this._spec__parameter, this._whenTrue__parameter, BooleanResultPredicateProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, BooleanResultBase<TMetadata>, TReplacementMetadata>(whenFalse));
        }
    }

    public struct Step_14__Motiv_Spec<TModel, TMetadata>
    {
        private readonly System.Func<TModel, BooleanResultBase<TMetadata>> _resultResolver__parameter;
        private readonly HigherOrderProposition.PropositionBuilders.HigherOrderSpecPredicateOperation<TModel, TMetadata> _higherOrderOperation__parameter;
        private readonly string _trueBecause__parameter;
        public Step_14__Motiv_Spec(in System.Func<TModel, Motiv.BooleanResultBase<TMetadata>> resultResolver, in Motiv.HigherOrderProposition.PropositionBuilders.HigherOrderSpecPredicateOperation<TModel, TMetadata> higherOrderOperation, in string trueBecause)
        {
            this._resultResolver__parameter = resultResolver;
            this._higherOrderOperation__parameter = higherOrderOperation;
            this._trueBecause__parameter = trueBecause;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public HigherOrderProposition.PropositionBuilders.Explanation.BooleanResultPredicateWithName.MultiAssertionExplanationFromBooleanResultWithNameHigherOrderPropositionFactory<TModel, TMetadata> WhenFalseYield(in System.Func<HigherOrderProposition.HigherOrderBooleanResultEvaluation<TModel, TMetadata>, System.Collections.Generic.IEnumerable<string>> function)
        {
            return new HigherOrderProposition.PropositionBuilders.Explanation.BooleanResultPredicateWithName.MultiAssertionExplanationFromBooleanResultWithNameHigherOrderPropositionFactory<TModel, TMetadata>(this._resultResolver__parameter, this._higherOrderOperation__parameter, this._trueBecause__parameter, HigherOrderProposition.PropositionBuilders.WhenFalseYieldOverloads.WhenFalseYield<HigherOrderProposition.HigherOrderBooleanResultEvaluation<TModel, TMetadata>, string>(function));
        }
    }

    public struct Step_15__Motiv_Spec<TModel, TMetadata>
    {
        private readonly System.Func<TModel, BooleanResultBase<TMetadata>> _resultResolver__parameter;
        private readonly HigherOrderProposition.PropositionBuilders.HigherOrderSpecPredicateOperation<TModel, TMetadata> _higherOrderOperation__parameter;
        private readonly System.Func<HigherOrderProposition.HigherOrderBooleanResultEvaluation<TModel, TMetadata>, System.Collections.Generic.IEnumerable<string>> _trueBecause__parameter;
        public Step_15__Motiv_Spec(in System.Func<TModel, Motiv.BooleanResultBase<TMetadata>> resultResolver, in Motiv.HigherOrderProposition.PropositionBuilders.HigherOrderSpecPredicateOperation<TModel, TMetadata> higherOrderOperation, in System.Func<Motiv.HigherOrderProposition.HigherOrderBooleanResultEvaluation<TModel, TMetadata>, System.Collections.Generic.IEnumerable<string>> trueBecause)
        {
            this._resultResolver__parameter = resultResolver;
            this._higherOrderOperation__parameter = higherOrderOperation;
            this._trueBecause__parameter = trueBecause;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public HigherOrderProposition.PropositionBuilders.Explanation.BooleanResultPredicate.MultiAssertionExplanationFromBooleanResultHigherOrderPropositionFactory<TModel, TMetadata> WhenFalseYield(in System.Func<HigherOrderProposition.HigherOrderBooleanResultEvaluation<TModel, TMetadata>, System.Collections.Generic.IEnumerable<string>> function)
        {
            return new HigherOrderProposition.PropositionBuilders.Explanation.BooleanResultPredicate.MultiAssertionExplanationFromBooleanResultHigherOrderPropositionFactory<TModel, TMetadata>(this._resultResolver__parameter, this._higherOrderOperation__parameter, this._trueBecause__parameter, HigherOrderProposition.PropositionBuilders.WhenFalseYieldOverloads.WhenFalseYield<HigherOrderProposition.HigherOrderBooleanResultEvaluation<TModel, TMetadata>, string>(function));
        }
    }

    public struct Step_16__Motiv_Spec<TModel, TMetadata, TReplacementMetadata>
    {
        private readonly System.Func<TModel, BooleanResultBase<TMetadata>> _resultResolver__parameter;
        private readonly HigherOrderProposition.PropositionBuilders.HigherOrderSpecPredicateOperation<TModel, TMetadata> _higherOrderOperation__parameter;
        private readonly System.Func<HigherOrderProposition.HigherOrderBooleanResultEvaluation<TModel, TMetadata>, System.Collections.Generic.IEnumerable<TReplacementMetadata>> _whenTrue__parameter;
        public Step_16__Motiv_Spec(in System.Func<TModel, Motiv.BooleanResultBase<TMetadata>> resultResolver, in Motiv.HigherOrderProposition.PropositionBuilders.HigherOrderSpecPredicateOperation<TModel, TMetadata> higherOrderOperation, in System.Func<Motiv.HigherOrderProposition.HigherOrderBooleanResultEvaluation<TModel, TMetadata>, System.Collections.Generic.IEnumerable<TReplacementMetadata>> whenTrue)
        {
            this._resultResolver__parameter = resultResolver;
            this._higherOrderOperation__parameter = higherOrderOperation;
            this._whenTrue__parameter = whenTrue;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public HigherOrderProposition.PropositionBuilders.Metadata.BooleanResultPredicate.MultiMetadataFromBooleanResultHigherOrderPropositionFactory<TModel, TReplacementMetadata, TMetadata> WhenFalseYield(in System.Func<HigherOrderProposition.HigherOrderBooleanResultEvaluation<TModel, TMetadata>, System.Collections.Generic.IEnumerable<TReplacementMetadata>> function)
        {
            return new HigherOrderProposition.PropositionBuilders.Metadata.BooleanResultPredicate.MultiMetadataFromBooleanResultHigherOrderPropositionFactory<TModel, TReplacementMetadata, TMetadata>(this._resultResolver__parameter, this._higherOrderOperation__parameter, this._whenTrue__parameter, HigherOrderProposition.PropositionBuilders.WhenFalseYieldOverloads.WhenFalseYield<HigherOrderProposition.HigherOrderBooleanResultEvaluation<TModel, TMetadata>, TReplacementMetadata>(function));
        }
    }

    public struct Step_18__Motiv_Spec<TModel, TMetadata>
    {
        private readonly System.Func<TModel, PolicyResultBase<TMetadata>> _predicate__parameter;
        private readonly System.Func<TModel, PolicyResultBase<TMetadata>, string> _trueBecause__parameter;
        public Step_18__Motiv_Spec(in System.Func<TModel, Motiv.PolicyResultBase<TMetadata>> predicate, in System.Func<TModel, Motiv.PolicyResultBase<TMetadata>, string> trueBecause)
        {
            this._predicate__parameter = predicate;
            this._trueBecause__parameter = trueBecause;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public BooleanResultPredicateProposition.PropositionBuilders.Explanation.ExplanationFromPolicyResultPropositionFactory<TModel, TMetadata> WhenFalse(in System.Func<TModel, PolicyResultBase<TMetadata>, string> whenFalse)
        {
            return new BooleanResultPredicateProposition.PropositionBuilders.Explanation.ExplanationFromPolicyResultPropositionFactory<TModel, TMetadata>(this._predicate__parameter, this._trueBecause__parameter, BooleanResultPredicateProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, PolicyResultBase<TMetadata>, string>(whenFalse));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public BooleanResultPredicateProposition.PropositionBuilders.Explanation.ExplanationFromPolicyResultPropositionFactory<TModel, TMetadata> WhenFalse(in System.Func<TModel, string> whenFalse)
        {
            return new BooleanResultPredicateProposition.PropositionBuilders.Explanation.ExplanationFromPolicyResultPropositionFactory<TModel, TMetadata>(this._predicate__parameter, this._trueBecause__parameter, BooleanResultPredicateProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, PolicyResultBase<TMetadata>, string>(whenFalse));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public BooleanResultPredicateProposition.PropositionBuilders.Explanation.ExplanationFromPolicyResultPropositionFactory<TModel, TMetadata> WhenFalse(in string whenFalse)
        {
            return new BooleanResultPredicateProposition.PropositionBuilders.Explanation.ExplanationFromPolicyResultPropositionFactory<TModel, TMetadata>(this._predicate__parameter, this._trueBecause__parameter, BooleanResultPredicateProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, PolicyResultBase<TMetadata>, string>(whenFalse));
        }
    }

    public struct Step_19__Motiv_Spec<TModel, TMetadata>
    {
        private readonly System.Func<TModel, PolicyResultBase<TMetadata>> _predicate__parameter;
        private readonly string _trueBecause__parameter;
        public Step_19__Motiv_Spec(in System.Func<TModel, Motiv.PolicyResultBase<TMetadata>> predicate, in string trueBecause)
        {
            this._predicate__parameter = predicate;
            this._trueBecause__parameter = trueBecause;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public BooleanResultPredicateProposition.PropositionBuilders.Explanation.ExplanationFromPolicyResultWithNamePropositionFactory<TModel, TMetadata> WhenFalse(in System.Func<TModel, PolicyResultBase<TMetadata>, string> whenFalse)
        {
            return new BooleanResultPredicateProposition.PropositionBuilders.Explanation.ExplanationFromPolicyResultWithNamePropositionFactory<TModel, TMetadata>(this._predicate__parameter, this._trueBecause__parameter, BooleanResultPredicateProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, PolicyResultBase<TMetadata>, string>(whenFalse));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public BooleanResultPredicateProposition.PropositionBuilders.Explanation.ExplanationFromPolicyResultWithNamePropositionFactory<TModel, TMetadata> WhenFalse(in System.Func<TModel, string> whenFalse)
        {
            return new BooleanResultPredicateProposition.PropositionBuilders.Explanation.ExplanationFromPolicyResultWithNamePropositionFactory<TModel, TMetadata>(this._predicate__parameter, this._trueBecause__parameter, BooleanResultPredicateProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, PolicyResultBase<TMetadata>, string>(whenFalse));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public BooleanResultPredicateProposition.PropositionBuilders.Explanation.ExplanationFromPolicyResultWithNamePropositionFactory<TModel, TMetadata> WhenFalse(in string whenFalse)
        {
            return new BooleanResultPredicateProposition.PropositionBuilders.Explanation.ExplanationFromPolicyResultWithNamePropositionFactory<TModel, TMetadata>(this._predicate__parameter, this._trueBecause__parameter, BooleanResultPredicateProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, PolicyResultBase<TMetadata>, string>(whenFalse));
        }
    }

    public struct Step_20__Motiv_Spec<TModel, TMetadata, TReplacementMetadata>
    {
        private readonly System.Func<TModel, PolicyResultBase<TMetadata>> _spec__parameter;
        private readonly System.Func<TModel, PolicyResultBase<TMetadata>, TReplacementMetadata> _whenTrue__parameter;
        public Step_20__Motiv_Spec(in System.Func<TModel, Motiv.PolicyResultBase<TMetadata>> spec, in System.Func<TModel, Motiv.PolicyResultBase<TMetadata>, TReplacementMetadata> whenTrue)
        {
            this._spec__parameter = spec;
            this._whenTrue__parameter = whenTrue;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public BooleanResultPredicateProposition.PropositionBuilders.Metadata.MetadataFromPolicyResultPropositionFactory<TModel, TReplacementMetadata, TMetadata> WhenFalse(in System.Func<TModel, PolicyResultBase<TMetadata>, TReplacementMetadata> whenFalse)
        {
            return new BooleanResultPredicateProposition.PropositionBuilders.Metadata.MetadataFromPolicyResultPropositionFactory<TModel, TReplacementMetadata, TMetadata>(this._spec__parameter, this._whenTrue__parameter, BooleanResultPredicateProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, PolicyResultBase<TMetadata>, TReplacementMetadata>(whenFalse));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public BooleanResultPredicateProposition.PropositionBuilders.Metadata.MetadataFromPolicyResultPropositionFactory<TModel, TReplacementMetadata, TMetadata> WhenFalse(in System.Func<TModel, TReplacementMetadata> whenFalse)
        {
            return new BooleanResultPredicateProposition.PropositionBuilders.Metadata.MetadataFromPolicyResultPropositionFactory<TModel, TReplacementMetadata, TMetadata>(this._spec__parameter, this._whenTrue__parameter, BooleanResultPredicateProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, PolicyResultBase<TMetadata>, TReplacementMetadata>(whenFalse));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public BooleanResultPredicateProposition.PropositionBuilders.Metadata.MetadataFromPolicyResultPropositionFactory<TModel, TReplacementMetadata, TMetadata> WhenFalse(in TReplacementMetadata whenFalse)
        {
            return new BooleanResultPredicateProposition.PropositionBuilders.Metadata.MetadataFromPolicyResultPropositionFactory<TModel, TReplacementMetadata, TMetadata>(this._spec__parameter, this._whenTrue__parameter, BooleanResultPredicateProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, PolicyResultBase<TMetadata>, TReplacementMetadata>(whenFalse));
        }
    }

    public struct Step_22__Motiv_Spec<TModel, TMetadata>
    {
        private readonly System.Func<TModel, PolicyResultBase<TMetadata>> _resultResolver__parameter;
        private readonly HigherOrderProposition.PropositionBuilders.HigherOrderPolicyPredicateOperation<TModel, TMetadata> _higherOrderOperation__parameter;
        private readonly string _trueBecause__parameter;
        public Step_22__Motiv_Spec(in System.Func<TModel, Motiv.PolicyResultBase<TMetadata>> resultResolver, in Motiv.HigherOrderProposition.PropositionBuilders.HigherOrderPolicyPredicateOperation<TModel, TMetadata> higherOrderOperation, in string trueBecause)
        {
            this._resultResolver__parameter = resultResolver;
            this._higherOrderOperation__parameter = higherOrderOperation;
            this._trueBecause__parameter = trueBecause;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public HigherOrderProposition.PropositionBuilders.Explanation.Policy.MultiAssertionExplanationWithNameHigherOrderPolicyResultPropositionFactory<TModel, TMetadata> WhenFalseYield(in System.Func<HigherOrderProposition.HigherOrderPolicyResultEvaluation<TModel, TMetadata>, System.Collections.Generic.IEnumerable<string>> function)
        {
            return new HigherOrderProposition.PropositionBuilders.Explanation.Policy.MultiAssertionExplanationWithNameHigherOrderPolicyResultPropositionFactory<TModel, TMetadata>(this._resultResolver__parameter, this._higherOrderOperation__parameter, this._trueBecause__parameter, HigherOrderProposition.PropositionBuilders.WhenFalseYieldOverloads.WhenFalseYield<HigherOrderProposition.HigherOrderPolicyResultEvaluation<TModel, TMetadata>, string>(function));
        }
    }

    public struct Step_23__Motiv_Spec<TModel, TMetadata>
    {
        private readonly System.Func<TModel, PolicyResultBase<TMetadata>> _resultResolver__parameter;
        private readonly HigherOrderProposition.PropositionBuilders.HigherOrderPolicyPredicateOperation<TModel, TMetadata> _higherOrderOperation__parameter;
        private readonly System.Func<HigherOrderProposition.HigherOrderPolicyResultEvaluation<TModel, TMetadata>, System.Collections.Generic.IEnumerable<string>> _trueBecause__parameter;
        public Step_23__Motiv_Spec(in System.Func<TModel, Motiv.PolicyResultBase<TMetadata>> resultResolver, in Motiv.HigherOrderProposition.PropositionBuilders.HigherOrderPolicyPredicateOperation<TModel, TMetadata> higherOrderOperation, in System.Func<Motiv.HigherOrderProposition.HigherOrderPolicyResultEvaluation<TModel, TMetadata>, System.Collections.Generic.IEnumerable<string>> trueBecause)
        {
            this._resultResolver__parameter = resultResolver;
            this._higherOrderOperation__parameter = higherOrderOperation;
            this._trueBecause__parameter = trueBecause;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public HigherOrderProposition.PropositionBuilders.Explanation.PolicyResultPredicate.MultiAssertionExplanationFromPolicyResultHigherOrderPropositionFactory<TModel, TMetadata> WhenFalseYield(in System.Func<HigherOrderProposition.HigherOrderPolicyResultEvaluation<TModel, TMetadata>, System.Collections.Generic.IEnumerable<string>> function)
        {
            return new HigherOrderProposition.PropositionBuilders.Explanation.PolicyResultPredicate.MultiAssertionExplanationFromPolicyResultHigherOrderPropositionFactory<TModel, TMetadata>(this._resultResolver__parameter, this._higherOrderOperation__parameter, this._trueBecause__parameter, HigherOrderProposition.PropositionBuilders.WhenFalseYieldOverloads.WhenFalseYield<HigherOrderProposition.HigherOrderPolicyResultEvaluation<TModel, TMetadata>, string>(function));
        }
    }

    public struct Step_24__Motiv_Spec<TModel, TMetadata, TReplacementMetadata>
    {
        private readonly System.Func<TModel, PolicyResultBase<TMetadata>> _resultResolver__parameter;
        private readonly HigherOrderProposition.PropositionBuilders.HigherOrderPolicyPredicateOperation<TModel, TMetadata> _higherOrderOperation__parameter;
        private readonly System.Func<HigherOrderProposition.HigherOrderPolicyResultEvaluation<TModel, TMetadata>, System.Collections.Generic.IEnumerable<TReplacementMetadata>> _whenTrue__parameter;
        public Step_24__Motiv_Spec(in System.Func<TModel, Motiv.PolicyResultBase<TMetadata>> resultResolver, in Motiv.HigherOrderProposition.PropositionBuilders.HigherOrderPolicyPredicateOperation<TModel, TMetadata> higherOrderOperation, in System.Func<Motiv.HigherOrderProposition.HigherOrderPolicyResultEvaluation<TModel, TMetadata>, System.Collections.Generic.IEnumerable<TReplacementMetadata>> whenTrue)
        {
            this._resultResolver__parameter = resultResolver;
            this._higherOrderOperation__parameter = higherOrderOperation;
            this._whenTrue__parameter = whenTrue;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public HigherOrderProposition.PropositionBuilders.Metadata.PolicyResultPredicate.MultiMetadataFromPolicyResultHigherOrderPropositionFactory<TModel, TReplacementMetadata, TMetadata> WhenFalseYield(in System.Func<HigherOrderProposition.HigherOrderPolicyResultEvaluation<TModel, TMetadata>, System.Collections.Generic.IEnumerable<TReplacementMetadata>> function)
        {
            return new HigherOrderProposition.PropositionBuilders.Metadata.PolicyResultPredicate.MultiMetadataFromPolicyResultHigherOrderPropositionFactory<TModel, TReplacementMetadata, TMetadata>(this._resultResolver__parameter, this._higherOrderOperation__parameter, this._whenTrue__parameter, HigherOrderProposition.PropositionBuilders.WhenFalseYieldOverloads.WhenFalseYield<HigherOrderProposition.HigherOrderPolicyResultEvaluation<TModel, TMetadata>, TReplacementMetadata>(function));
        }
    }

    public struct Step_26__Motiv_Spec<TModel, TPredicateResult>
    {
        private readonly System.Linq.Expressions.Expression<System.Func<TModel, TPredicateResult>> _expression__parameter;
        private readonly System.Func<TModel, BooleanResultBase<string>, string> _trueBecause__parameter;
        public Step_26__Motiv_Spec(in System.Linq.Expressions.Expression<System.Func<TModel, TPredicateResult>> expression, in System.Func<TModel, Motiv.BooleanResultBase<string>, string> trueBecause)
        {
            this._expression__parameter = expression;
            this._trueBecause__parameter = trueBecause;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public ExpressionTreeProposition.PropositionBuilders.Explanation.ExplanationExpressionTreePropositionFactory<TModel, TPredicateResult> WhenFalse(in System.Func<TModel, BooleanResultBase<string>, string> whenFalse)
        {
            return new ExpressionTreeProposition.PropositionBuilders.Explanation.ExplanationExpressionTreePropositionFactory<TModel, TPredicateResult>(this._expression__parameter, this._trueBecause__parameter, ExpressionTreeProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, BooleanResultBase<string>, string>(whenFalse));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public ExpressionTreeProposition.PropositionBuilders.Explanation.ExplanationExpressionTreePropositionFactory<TModel, TPredicateResult> WhenFalse(in System.Func<TModel, string> whenFalse)
        {
            return new ExpressionTreeProposition.PropositionBuilders.Explanation.ExplanationExpressionTreePropositionFactory<TModel, TPredicateResult>(this._expression__parameter, this._trueBecause__parameter, ExpressionTreeProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, BooleanResultBase<string>, string>(whenFalse));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public ExpressionTreeProposition.PropositionBuilders.Explanation.ExplanationExpressionTreePropositionFactory<TModel, TPredicateResult> WhenFalse(in string whenFalse)
        {
            return new ExpressionTreeProposition.PropositionBuilders.Explanation.ExplanationExpressionTreePropositionFactory<TModel, TPredicateResult>(this._expression__parameter, this._trueBecause__parameter, ExpressionTreeProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, BooleanResultBase<string>, string>(whenFalse));
        }
    }

    public struct Step_27__Motiv_Spec<TModel, TPredicateResult>
    {
        private readonly System.Linq.Expressions.Expression<System.Func<TModel, TPredicateResult>> _expression__parameter;
        private readonly string _trueBecause__parameter;
        public Step_27__Motiv_Spec(in System.Linq.Expressions.Expression<System.Func<TModel, TPredicateResult>> expression, in string trueBecause)
        {
            this._expression__parameter = expression;
            this._trueBecause__parameter = trueBecause;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public ExpressionTreeProposition.PropositionBuilders.Explanation.ExplanationWithNameExpressionTreePropositionFactory<TModel, TPredicateResult> WhenFalse(in System.Func<TModel, BooleanResultBase<string>, string> whenFalse)
        {
            return new ExpressionTreeProposition.PropositionBuilders.Explanation.ExplanationWithNameExpressionTreePropositionFactory<TModel, TPredicateResult>(this._expression__parameter, this._trueBecause__parameter, ExpressionTreeProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, BooleanResultBase<string>, string>(whenFalse));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public ExpressionTreeProposition.PropositionBuilders.Explanation.ExplanationWithNameExpressionTreePropositionFactory<TModel, TPredicateResult> WhenFalse(in System.Func<TModel, string> whenFalse)
        {
            return new ExpressionTreeProposition.PropositionBuilders.Explanation.ExplanationWithNameExpressionTreePropositionFactory<TModel, TPredicateResult>(this._expression__parameter, this._trueBecause__parameter, ExpressionTreeProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, BooleanResultBase<string>, string>(whenFalse));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public ExpressionTreeProposition.PropositionBuilders.Explanation.ExplanationWithNameExpressionTreePropositionFactory<TModel, TPredicateResult> WhenFalse(in string whenFalse)
        {
            return new ExpressionTreeProposition.PropositionBuilders.Explanation.ExplanationWithNameExpressionTreePropositionFactory<TModel, TPredicateResult>(this._expression__parameter, this._trueBecause__parameter, ExpressionTreeProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, BooleanResultBase<string>, string>(whenFalse));
        }
    }

    public struct Step_28__Motiv_Spec<TModel, TPredicateResult, TMetadata>
    {
        private readonly System.Linq.Expressions.Expression<System.Func<TModel, TPredicateResult>> _expression__parameter;
        private readonly System.Func<TModel, BooleanResultBase<string>, TMetadata> _whenTrue__parameter;
        public Step_28__Motiv_Spec(in System.Linq.Expressions.Expression<System.Func<TModel, TPredicateResult>> expression, in System.Func<TModel, Motiv.BooleanResultBase<string>, TMetadata> whenTrue)
        {
            this._expression__parameter = expression;
            this._whenTrue__parameter = whenTrue;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public ExpressionTreeProposition.PropositionBuilders.Metadata.MetadataExpressionTreePropositionFactory<TModel, TMetadata, TPredicateResult> WhenFalse(in System.Func<TModel, BooleanResultBase<string>, TMetadata> whenFalse)
        {
            return new ExpressionTreeProposition.PropositionBuilders.Metadata.MetadataExpressionTreePropositionFactory<TModel, TMetadata, TPredicateResult>(this._expression__parameter, this._whenTrue__parameter, ExpressionTreeProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, BooleanResultBase<string>, TMetadata>(whenFalse));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public ExpressionTreeProposition.PropositionBuilders.Metadata.MetadataExpressionTreePropositionFactory<TModel, TMetadata, TPredicateResult> WhenFalse(in System.Func<TModel, TMetadata> whenFalse)
        {
            return new ExpressionTreeProposition.PropositionBuilders.Metadata.MetadataExpressionTreePropositionFactory<TModel, TMetadata, TPredicateResult>(this._expression__parameter, this._whenTrue__parameter, ExpressionTreeProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, BooleanResultBase<string>, TMetadata>(whenFalse));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public ExpressionTreeProposition.PropositionBuilders.Metadata.MetadataExpressionTreePropositionFactory<TModel, TMetadata, TPredicateResult> WhenFalse(in TMetadata whenFalse)
        {
            return new ExpressionTreeProposition.PropositionBuilders.Metadata.MetadataExpressionTreePropositionFactory<TModel, TMetadata, TPredicateResult>(this._expression__parameter, this._whenTrue__parameter, ExpressionTreeProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, BooleanResultBase<string>, TMetadata>(whenFalse));
        }
    }

    public struct Step_30__Motiv_Spec<TModel, TPredicateResult>
    {
        private readonly System.Linq.Expressions.Expression<System.Func<TModel, TPredicateResult>> _expression__parameter;
        private readonly HigherOrderProposition.PropositionBuilders.HigherOrderSpecPredicateOperation<TModel, string> _higherOrderOperation__parameter;
        private readonly string _trueBecause__parameter;
        public Step_30__Motiv_Spec(in System.Linq.Expressions.Expression<System.Func<TModel, TPredicateResult>> expression, in Motiv.HigherOrderProposition.PropositionBuilders.HigherOrderSpecPredicateOperation<TModel, string> higherOrderOperation, in string trueBecause)
        {
            this._expression__parameter = expression;
            this._higherOrderOperation__parameter = higherOrderOperation;
            this._trueBecause__parameter = trueBecause;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public HigherOrderProposition.PropositionBuilders.Explanation.ExpressionTree.MultiAssertionExplanationFromBooleanResultWithNameHigherOrderExpressionTreePropositionFactory<TModel, TPredicateResult> WhenFalseYield(in System.Func<HigherOrderProposition.HigherOrderBooleanResultEvaluation<TModel, string>, System.Collections.Generic.IEnumerable<string>> function)
        {
            return new HigherOrderProposition.PropositionBuilders.Explanation.ExpressionTree.MultiAssertionExplanationFromBooleanResultWithNameHigherOrderExpressionTreePropositionFactory<TModel, TPredicateResult>(this._expression__parameter, this._higherOrderOperation__parameter, this._trueBecause__parameter, HigherOrderProposition.PropositionBuilders.WhenFalseYieldOverloads.WhenFalseYield<HigherOrderProposition.HigherOrderBooleanResultEvaluation<TModel, string>, string>(function));
        }
    }

    public struct Step_31__Motiv_Spec<TModel, TPredicateResult>
    {
        private readonly System.Linq.Expressions.Expression<System.Func<TModel, TPredicateResult>> _expression__parameter;
        private readonly HigherOrderProposition.PropositionBuilders.HigherOrderSpecPredicateOperation<TModel, string> _higherOrderOperation__parameter;
        private readonly System.Func<HigherOrderProposition.HigherOrderBooleanResultEvaluation<TModel, string>, System.Collections.Generic.IEnumerable<string>> _trueBecause__parameter;
        public Step_31__Motiv_Spec(in System.Linq.Expressions.Expression<System.Func<TModel, TPredicateResult>> expression, in Motiv.HigherOrderProposition.PropositionBuilders.HigherOrderSpecPredicateOperation<TModel, string> higherOrderOperation, in System.Func<Motiv.HigherOrderProposition.HigherOrderBooleanResultEvaluation<TModel, string>, System.Collections.Generic.IEnumerable<string>> trueBecause)
        {
            this._expression__parameter = expression;
            this._higherOrderOperation__parameter = higherOrderOperation;
            this._trueBecause__parameter = trueBecause;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public HigherOrderProposition.PropositionBuilders.Explanation.ExpressionTree.MultiAssertionExplanationFromBooleanResultHigherOrderExpressionTreePropositionFactory<TModel, TPredicateResult> WhenFalseYield(in System.Func<HigherOrderProposition.HigherOrderBooleanResultEvaluation<TModel, string>, System.Collections.Generic.IEnumerable<string>> function)
        {
            return new HigherOrderProposition.PropositionBuilders.Explanation.ExpressionTree.MultiAssertionExplanationFromBooleanResultHigherOrderExpressionTreePropositionFactory<TModel, TPredicateResult>(this._expression__parameter, this._higherOrderOperation__parameter, this._trueBecause__parameter, HigherOrderProposition.PropositionBuilders.WhenFalseYieldOverloads.WhenFalseYield<HigherOrderProposition.HigherOrderBooleanResultEvaluation<TModel, string>, string>(function));
        }
    }

    public struct Step_32__Motiv_Spec<TModel, TPredicateResult, TMetadata>
    {
        private readonly System.Linq.Expressions.Expression<System.Func<TModel, TPredicateResult>> _expression__parameter;
        private readonly HigherOrderProposition.PropositionBuilders.HigherOrderSpecPredicateOperation<TModel, string> _higherOrderOperation__parameter;
        private readonly System.Func<HigherOrderProposition.HigherOrderBooleanResultEvaluation<TModel, string>, System.Collections.Generic.IEnumerable<TMetadata>> _whenTrue__parameter;
        public Step_32__Motiv_Spec(in System.Linq.Expressions.Expression<System.Func<TModel, TPredicateResult>> expression, in Motiv.HigherOrderProposition.PropositionBuilders.HigherOrderSpecPredicateOperation<TModel, string> higherOrderOperation, in System.Func<Motiv.HigherOrderProposition.HigherOrderBooleanResultEvaluation<TModel, string>, System.Collections.Generic.IEnumerable<TMetadata>> whenTrue)
        {
            this._expression__parameter = expression;
            this._higherOrderOperation__parameter = higherOrderOperation;
            this._whenTrue__parameter = whenTrue;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public HigherOrderProposition.PropositionBuilders.Metadata.ExpressionTree.MultiMetadataFromSpecHigherOrderExpressionTreePropositionFactory<TModel, TMetadata, TPredicateResult> WhenFalseYield(in System.Func<HigherOrderProposition.HigherOrderBooleanResultEvaluation<TModel, string>, System.Collections.Generic.IEnumerable<TMetadata>> function)
        {
            return new HigherOrderProposition.PropositionBuilders.Metadata.ExpressionTree.MultiMetadataFromSpecHigherOrderExpressionTreePropositionFactory<TModel, TMetadata, TPredicateResult>(this._expression__parameter, this._higherOrderOperation__parameter, this._whenTrue__parameter, HigherOrderProposition.PropositionBuilders.WhenFalseYieldOverloads.WhenFalseYield<HigherOrderProposition.HigherOrderBooleanResultEvaluation<TModel, string>, TMetadata>(function));
        }
    }

    public struct Step_35__Motiv_Spec<TModel, TMetadata, TReplacementMetadata>
    {
        private readonly PolicyBase<TModel, TMetadata> _policy__parameter;
        private readonly HigherOrderProposition.PropositionBuilders.HigherOrderPolicyPredicateOperation<TModel, TMetadata> _higherOrderOperation__parameter;
        private readonly System.Func<HigherOrderProposition.HigherOrderPolicyResultEvaluation<TModel, TMetadata>, System.Collections.Generic.IEnumerable<TReplacementMetadata>> _whenTrue__parameter;
        public Step_35__Motiv_Spec(in Motiv.PolicyBase<TModel, TMetadata> policy, in Motiv.HigherOrderProposition.PropositionBuilders.HigherOrderPolicyPredicateOperation<TModel, TMetadata> higherOrderOperation, in System.Func<Motiv.HigherOrderProposition.HigherOrderPolicyResultEvaluation<TModel, TMetadata>, System.Collections.Generic.IEnumerable<TReplacementMetadata>> whenTrue)
        {
            this._policy__parameter = policy;
            this._higherOrderOperation__parameter = higherOrderOperation;
            this._whenTrue__parameter = whenTrue;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public HigherOrderProposition.PropositionBuilders.Metadata.Policy.MultiMetadataFromPolicyHigherOrderPropositionFactory<TModel, TReplacementMetadata, TMetadata> WhenFalseYield(in System.Func<HigherOrderProposition.HigherOrderPolicyResultEvaluation<TModel, TMetadata>, System.Collections.Generic.IEnumerable<TReplacementMetadata>> function)
        {
            return new HigherOrderProposition.PropositionBuilders.Metadata.Policy.MultiMetadataFromPolicyHigherOrderPropositionFactory<TModel, TReplacementMetadata, TMetadata>(this._policy__parameter, this._higherOrderOperation__parameter, this._whenTrue__parameter, HigherOrderProposition.PropositionBuilders.WhenFalseYieldOverloads.WhenFalseYield<HigherOrderProposition.HigherOrderPolicyResultEvaluation<TModel, TMetadata>, TReplacementMetadata>(function));
        }
    }

    public struct Step_36__Motiv_Spec<TModel, TMetadata>
    {
        private readonly PolicyBase<TModel, TMetadata> _policy__parameter;
        private readonly System.Func<TModel, PolicyResultBase<TMetadata>, string> _trueBecause__parameter;
        public Step_36__Motiv_Spec(in Motiv.PolicyBase<TModel, TMetadata> policy, in System.Func<TModel, Motiv.PolicyResultBase<TMetadata>, string> trueBecause)
        {
            this._policy__parameter = policy;
            this._trueBecause__parameter = trueBecause;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public SpecDecoratorProposition.PropositionBuilders.Explanation.ExplanationPolicyDecoratorFactory<TModel, TMetadata> WhenFalse(in System.Func<TModel, PolicyResultBase<TMetadata>, string> whenFalse)
        {
            return new SpecDecoratorProposition.PropositionBuilders.Explanation.ExplanationPolicyDecoratorFactory<TModel, TMetadata>(this._policy__parameter, this._trueBecause__parameter, SpecDecoratorProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, PolicyResultBase<TMetadata>, string>(whenFalse));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public SpecDecoratorProposition.PropositionBuilders.Explanation.ExplanationPolicyDecoratorFactory<TModel, TMetadata> WhenFalse(in System.Func<TModel, string> whenFalse)
        {
            return new SpecDecoratorProposition.PropositionBuilders.Explanation.ExplanationPolicyDecoratorFactory<TModel, TMetadata>(this._policy__parameter, this._trueBecause__parameter, SpecDecoratorProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, PolicyResultBase<TMetadata>, string>(whenFalse));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public SpecDecoratorProposition.PropositionBuilders.Explanation.ExplanationPolicyDecoratorFactory<TModel, TMetadata> WhenFalse(in string whenFalse)
        {
            return new SpecDecoratorProposition.PropositionBuilders.Explanation.ExplanationPolicyDecoratorFactory<TModel, TMetadata>(this._policy__parameter, this._trueBecause__parameter, SpecDecoratorProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, PolicyResultBase<TMetadata>, string>(whenFalse));
        }
    }

    public struct Step_37__Motiv_Spec<TModel, TMetadata>
    {
        private readonly PolicyBase<TModel, TMetadata> _policy__parameter;
        private readonly string _trueBecause__parameter;
        public Step_37__Motiv_Spec(in Motiv.PolicyBase<TModel, TMetadata> policy, in string trueBecause)
        {
            this._policy__parameter = policy;
            this._trueBecause__parameter = trueBecause;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public SpecDecoratorProposition.PropositionBuilders.Explanation.ExplanationWithNamePropositionFactory<TModel, TMetadata> WhenFalse(in System.Func<TModel, BooleanResultBase<TMetadata>, string> whenFalse)
        {
            return new SpecDecoratorProposition.PropositionBuilders.Explanation.ExplanationWithNamePropositionFactory<TModel, TMetadata>(this._policy__parameter, this._trueBecause__parameter, SpecDecoratorProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, BooleanResultBase<TMetadata>, string>(whenFalse));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public SpecDecoratorProposition.PropositionBuilders.Explanation.ExplanationWithNamePropositionFactory<TModel, TMetadata> WhenFalse(in System.Func<TModel, string> whenFalse)
        {
            return new SpecDecoratorProposition.PropositionBuilders.Explanation.ExplanationWithNamePropositionFactory<TModel, TMetadata>(this._policy__parameter, this._trueBecause__parameter, SpecDecoratorProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, BooleanResultBase<TMetadata>, string>(whenFalse));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public SpecDecoratorProposition.PropositionBuilders.Explanation.ExplanationWithNamePropositionFactory<TModel, TMetadata> WhenFalse(in string whenFalse)
        {
            return new SpecDecoratorProposition.PropositionBuilders.Explanation.ExplanationWithNamePropositionFactory<TModel, TMetadata>(this._policy__parameter, this._trueBecause__parameter, SpecDecoratorProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, BooleanResultBase<TMetadata>, string>(whenFalse));
        }
    }

    public struct Step_38__Motiv_Spec<TModel, TMetadata, TReplacementMetadata>
    {
        private readonly PolicyBase<TModel, TMetadata> _spec__parameter;
        private readonly System.Func<TModel, PolicyResultBase<TMetadata>, TReplacementMetadata> _whenTrue__parameter;
        public Step_38__Motiv_Spec(in Motiv.PolicyBase<TModel, TMetadata> spec, in System.Func<TModel, Motiv.PolicyResultBase<TMetadata>, TReplacementMetadata> whenTrue)
        {
            this._spec__parameter = spec;
            this._whenTrue__parameter = whenTrue;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public SpecDecoratorProposition.PropositionBuilders.Metadata.MetadataPolicyFactory<TModel, TReplacementMetadata, TMetadata> WhenFalse(in System.Func<TModel, PolicyResultBase<TMetadata>, TReplacementMetadata> whenFalse)
        {
            return new SpecDecoratorProposition.PropositionBuilders.Metadata.MetadataPolicyFactory<TModel, TReplacementMetadata, TMetadata>(this._spec__parameter, this._whenTrue__parameter, SpecDecoratorProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, PolicyResultBase<TMetadata>, TReplacementMetadata>(whenFalse));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public SpecDecoratorProposition.PropositionBuilders.Metadata.MetadataPolicyFactory<TModel, TReplacementMetadata, TMetadata> WhenFalse(in System.Func<TModel, TReplacementMetadata> whenFalse)
        {
            return new SpecDecoratorProposition.PropositionBuilders.Metadata.MetadataPolicyFactory<TModel, TReplacementMetadata, TMetadata>(this._spec__parameter, this._whenTrue__parameter, SpecDecoratorProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, PolicyResultBase<TMetadata>, TReplacementMetadata>(whenFalse));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public SpecDecoratorProposition.PropositionBuilders.Metadata.MetadataPolicyFactory<TModel, TReplacementMetadata, TMetadata> WhenFalse(in TReplacementMetadata whenFalse)
        {
            return new SpecDecoratorProposition.PropositionBuilders.Metadata.MetadataPolicyFactory<TModel, TReplacementMetadata, TMetadata>(this._spec__parameter, this._whenTrue__parameter, SpecDecoratorProposition.PropositionBuilders.WhenFalseOverloads.WhenFalse<TModel, PolicyResultBase<TMetadata>, TReplacementMetadata>(whenFalse));
        }
    }
}

namespace Motiv.HigherOrderProposition.PropositionBuilders.Metadata.BooleanPredicate
{
    public partial struct TrueHigherOrderFromBooleanPredicatePropositionFactory<TModel>
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_7__Motiv_Spec<TModel> WhenTrue(in string trueBecause)
        {
            return new Step_7__Motiv_Spec<TModel>(predicate, higherOrderOperation, trueBecause);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_8__Motiv_Spec<TModel, TMetadata> WhenTrueYield<TMetadata>(in System.Func<HigherOrderProposition.HigherOrderBooleanEvaluation<TModel>, System.Collections.Generic.IEnumerable<TMetadata>> function)
        {
            return new Step_8__Motiv_Spec<TModel, TMetadata>(predicate, higherOrderOperation, function);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_8__Motiv_Spec<TModel, TMetadata> WhenTrue<TMetadata>(in System.Func<HigherOrderProposition.HigherOrderBooleanEvaluation<TModel>, TMetadata> function)
        {
            return new Step_8__Motiv_Spec<TModel, TMetadata>(predicate, higherOrderOperation, function);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_8__Motiv_Spec<TModel, TMetadata> WhenTrue<TMetadata>(in TMetadata value)
        {
            return new Step_8__Motiv_Spec<TModel, TMetadata>(predicate, higherOrderOperation, value);
        }
    }
}

namespace Motiv.BooleanResultPredicateProposition.PropositionBuilders
{
    public partial struct BooleanResultPredicatePropositionFactory<TModel, TMetadata>
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_10__Motiv_Spec<TModel, TMetadata> WhenTrue(in System.Func<TModel, BooleanResultBase<TMetadata>, string> whenTrue)
        {
            return new Step_10__Motiv_Spec<TModel, TMetadata>(predicate, whenTrue);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_10__Motiv_Spec<TModel, TMetadata> WhenTrue(in System.Func<TModel, string> whenTrue)
        {
            return new Step_10__Motiv_Spec<TModel, TMetadata>(predicate, whenTrue);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_10__Motiv_Spec<TModel, TMetadata> WhenTrue(in string whenTrue)
        {
            return new Step_10__Motiv_Spec<TModel, TMetadata>(predicate, whenTrue);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_11__Motiv_Spec<TModel, TMetadata> WhenTrue(in string trueBecause)
        {
            return new Step_11__Motiv_Spec<TModel, TMetadata>(predicate, trueBecause);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_12__Motiv_Spec<TModel, TMetadata, TReplacementMetadata> WhenTrue<TReplacementMetadata>(in System.Func<TModel, BooleanResultBase<TMetadata>, TReplacementMetadata> whenTrue)
        {
            return new Step_12__Motiv_Spec<TModel, TMetadata, TReplacementMetadata>(predicate, whenTrue);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_12__Motiv_Spec<TModel, TMetadata, TReplacementMetadata> WhenTrue<TReplacementMetadata>(in System.Func<TModel, TReplacementMetadata> whenTrue)
        {
            return new Step_12__Motiv_Spec<TModel, TMetadata, TReplacementMetadata>(predicate, whenTrue);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_12__Motiv_Spec<TModel, TMetadata, TReplacementMetadata> WhenTrue<TReplacementMetadata>(in TReplacementMetadata whenTrue)
        {
            return new Step_12__Motiv_Spec<TModel, TMetadata, TReplacementMetadata>(predicate, whenTrue);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Metadata.BooleanResultPredicate.TrueHigherOrderFromBooleanResultPredicatePropositionFactory<TModel, TMetadata> As(in System.Func<System.Collections.Generic.IEnumerable<HigherOrderProposition.BooleanResult<TModel, TMetadata>>, bool> higherOrderPredicate, in System.Func<bool, System.Collections.Generic.IEnumerable<HigherOrderProposition.BooleanResult<TModel, TMetadata>>, System.Collections.Generic.IEnumerable<HigherOrderProposition.BooleanResult<TModel, TMetadata>>> causeSelector)
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Metadata.BooleanResultPredicate.TrueHigherOrderFromBooleanResultPredicatePropositionFactory<TModel, TMetadata>(predicate, higherOrderPredicate, causeSelector);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Metadata.BooleanResultPredicate.TrueHigherOrderFromBooleanResultPredicatePropositionFactory<TModel, TMetadata> As(in System.Func<System.Collections.Generic.IEnumerable<HigherOrderProposition.BooleanResult<TModel, TMetadata>>, bool> higherOrderPredicate)
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Metadata.BooleanResultPredicate.TrueHigherOrderFromBooleanResultPredicatePropositionFactory<TModel, TMetadata>(predicate, higherOrderPredicate);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Metadata.BooleanResultPredicate.TrueHigherOrderFromBooleanResultPredicatePropositionFactory<TModel, TMetadata> AsAllSatisfied()
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Metadata.BooleanResultPredicate.TrueHigherOrderFromBooleanResultPredicatePropositionFactory<TModel, TMetadata>(predicate);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Metadata.BooleanResultPredicate.TrueHigherOrderFromBooleanResultPredicatePropositionFactory<TModel, TMetadata> AsAnySatisfied()
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Metadata.BooleanResultPredicate.TrueHigherOrderFromBooleanResultPredicatePropositionFactory<TModel, TMetadata>(predicate);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Metadata.BooleanResultPredicate.TrueHigherOrderFromBooleanResultPredicatePropositionFactory<TModel, TMetadata> AsAtLeastNSatisfied(in int n)
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Metadata.BooleanResultPredicate.TrueHigherOrderFromBooleanResultPredicatePropositionFactory<TModel, TMetadata>(predicate, n);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Metadata.BooleanResultPredicate.TrueHigherOrderFromBooleanResultPredicatePropositionFactory<TModel, TMetadata> AsAtMostNSatisfied(in int n)
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Metadata.BooleanResultPredicate.TrueHigherOrderFromBooleanResultPredicatePropositionFactory<TModel, TMetadata>(predicate, n);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Metadata.BooleanResultPredicate.TrueHigherOrderFromBooleanResultPredicatePropositionFactory<TModel, TMetadata> AsNoneSatisfied()
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Metadata.BooleanResultPredicate.TrueHigherOrderFromBooleanResultPredicatePropositionFactory<TModel, TMetadata>(predicate);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Metadata.BooleanResultPredicate.TrueHigherOrderFromBooleanResultPredicatePropositionFactory<TModel, TMetadata> AsNSatisfied(in int n)
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Metadata.BooleanResultPredicate.TrueHigherOrderFromBooleanResultPredicatePropositionFactory<TModel, TMetadata>(predicate, n);
        }
    }

    public partial struct PolicyResultPredicatePropositionFactory<TModel, TMetadata>
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_18__Motiv_Spec<TModel, TMetadata> WhenTrue(in System.Func<TModel, PolicyResultBase<TMetadata>, string> whenTrue)
        {
            return new Step_18__Motiv_Spec<TModel, TMetadata>(predicate, whenTrue);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_18__Motiv_Spec<TModel, TMetadata> WhenTrue(in System.Func<TModel, string> whenTrue)
        {
            return new Step_18__Motiv_Spec<TModel, TMetadata>(predicate, whenTrue);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_18__Motiv_Spec<TModel, TMetadata> WhenTrue(in string whenTrue)
        {
            return new Step_18__Motiv_Spec<TModel, TMetadata>(predicate, whenTrue);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_19__Motiv_Spec<TModel, TMetadata> WhenTrue(in string trueBecause)
        {
            return new Step_19__Motiv_Spec<TModel, TMetadata>(predicate, trueBecause);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_20__Motiv_Spec<TModel, TMetadata, TReplacementMetadata> WhenTrue<TReplacementMetadata>(in System.Func<TModel, PolicyResultBase<TMetadata>, TReplacementMetadata> whenTrue)
        {
            return new Step_20__Motiv_Spec<TModel, TMetadata, TReplacementMetadata>(predicate, whenTrue);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_20__Motiv_Spec<TModel, TMetadata, TReplacementMetadata> WhenTrue<TReplacementMetadata>(in System.Func<TModel, TReplacementMetadata> whenTrue)
        {
            return new Step_20__Motiv_Spec<TModel, TMetadata, TReplacementMetadata>(predicate, whenTrue);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_20__Motiv_Spec<TModel, TMetadata, TReplacementMetadata> WhenTrue<TReplacementMetadata>(in TReplacementMetadata whenTrue)
        {
            return new Step_20__Motiv_Spec<TModel, TMetadata, TReplacementMetadata>(predicate, whenTrue);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Metadata.PolicyResultPredicate.TrueHigherOrderFromPolicyResultPredicatePropositionFactory<TModel, TMetadata> As(in System.Func<System.Collections.Generic.IEnumerable<HigherOrderProposition.PolicyResult<TModel, TMetadata>>, bool> higherOrderPredicate, in System.Func<bool, System.Collections.Generic.IEnumerable<HigherOrderProposition.PolicyResult<TModel, TMetadata>>, System.Collections.Generic.IEnumerable<HigherOrderProposition.PolicyResult<TModel, TMetadata>>> causeSelector)
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Metadata.PolicyResultPredicate.TrueHigherOrderFromPolicyResultPredicatePropositionFactory<TModel, TMetadata>(predicate, higherOrderPredicate, causeSelector);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Metadata.PolicyResultPredicate.TrueHigherOrderFromPolicyResultPredicatePropositionFactory<TModel, TMetadata> As(in System.Func<System.Collections.Generic.IEnumerable<HigherOrderProposition.PolicyResult<TModel, TMetadata>>, bool> higherOrderPredicate)
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Metadata.PolicyResultPredicate.TrueHigherOrderFromPolicyResultPredicatePropositionFactory<TModel, TMetadata>(predicate, higherOrderPredicate);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Metadata.PolicyResultPredicate.TrueHigherOrderFromPolicyResultPredicatePropositionFactory<TModel, TMetadata> AsAllSatisfied()
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Metadata.PolicyResultPredicate.TrueHigherOrderFromPolicyResultPredicatePropositionFactory<TModel, TMetadata>(predicate);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Metadata.PolicyResultPredicate.TrueHigherOrderFromPolicyResultPredicatePropositionFactory<TModel, TMetadata> AsAnySatisfied()
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Metadata.PolicyResultPredicate.TrueHigherOrderFromPolicyResultPredicatePropositionFactory<TModel, TMetadata>(predicate);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Metadata.PolicyResultPredicate.TrueHigherOrderFromPolicyResultPredicatePropositionFactory<TModel, TMetadata> AsAtLeastNSatisfied(in int n)
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Metadata.PolicyResultPredicate.TrueHigherOrderFromPolicyResultPredicatePropositionFactory<TModel, TMetadata>(predicate, n);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Metadata.PolicyResultPredicate.TrueHigherOrderFromPolicyResultPredicatePropositionFactory<TModel, TMetadata> AsAtMostNSatisfied(in int n)
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Metadata.PolicyResultPredicate.TrueHigherOrderFromPolicyResultPredicatePropositionFactory<TModel, TMetadata>(predicate, n);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Metadata.PolicyResultPredicate.TrueHigherOrderFromPolicyResultPredicatePropositionFactory<TModel, TMetadata> AsNoneSatisfied()
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Metadata.PolicyResultPredicate.TrueHigherOrderFromPolicyResultPredicatePropositionFactory<TModel, TMetadata>(predicate);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Metadata.PolicyResultPredicate.TrueHigherOrderFromPolicyResultPredicatePropositionFactory<TModel, TMetadata> AsNSatisfied(in int n)
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Metadata.PolicyResultPredicate.TrueHigherOrderFromPolicyResultPredicatePropositionFactory<TModel, TMetadata>(predicate, n);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Metadata.PolicyResultPredicate.TrueHigherOrderFromPolicyResultPredicatePropositionFactory<TModel, TMetadata> As(in System.Func<System.Collections.Generic.IEnumerable<HigherOrderProposition.BooleanResult<TModel, TMetadata>>, bool> higherOrderPredicate, in System.Func<bool, System.Collections.Generic.IEnumerable<HigherOrderProposition.BooleanResult<TModel, TMetadata>>, System.Collections.Generic.IEnumerable<HigherOrderProposition.BooleanResult<TModel, TMetadata>>> causeSelector)
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Metadata.PolicyResultPredicate.TrueHigherOrderFromPolicyResultPredicatePropositionFactory<TModel, TMetadata>(predicate, higherOrderPredicate, causeSelector);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Metadata.PolicyResultPredicate.TrueHigherOrderFromPolicyResultPredicatePropositionFactory<TModel, TMetadata> As(in System.Func<System.Collections.Generic.IEnumerable<HigherOrderProposition.BooleanResult<TModel, TMetadata>>, bool> higherOrderPredicate)
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Metadata.PolicyResultPredicate.TrueHigherOrderFromPolicyResultPredicatePropositionFactory<TModel, TMetadata>(predicate, higherOrderPredicate);
        }
    }
}

namespace Motiv.HigherOrderProposition.PropositionBuilders.Metadata.BooleanResultPredicate
{
    public partial struct TrueHigherOrderFromBooleanResultPredicatePropositionFactory<TModel, TMetadata>
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_14__Motiv_Spec<TModel, TMetadata> WhenTrue(in string trueBecause)
        {
            return new Step_14__Motiv_Spec<TModel, TMetadata>(resultResolver, higherOrderOperation, trueBecause);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_15__Motiv_Spec<TModel, TMetadata> WhenTrueYield(in System.Func<HigherOrderProposition.HigherOrderBooleanResultEvaluation<TModel, TMetadata>, System.Collections.Generic.IEnumerable<string>> function)
        {
            return new Step_15__Motiv_Spec<TModel, TMetadata>(resultResolver, higherOrderOperation, function);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_15__Motiv_Spec<TModel, TMetadata> WhenTrue(in System.Func<HigherOrderProposition.HigherOrderBooleanResultEvaluation<TModel, TMetadata>, string> function)
        {
            return new Step_15__Motiv_Spec<TModel, TMetadata>(resultResolver, higherOrderOperation, function);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_16__Motiv_Spec<TModel, TMetadata, TReplacementMetadata> WhenTrueYield<TReplacementMetadata>(in System.Func<HigherOrderProposition.HigherOrderBooleanResultEvaluation<TModel, TMetadata>, System.Collections.Generic.IEnumerable<TReplacementMetadata>> function)
        {
            return new Step_16__Motiv_Spec<TModel, TMetadata, TReplacementMetadata>(resultResolver, higherOrderOperation, function);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_16__Motiv_Spec<TModel, TMetadata, TReplacementMetadata> WhenTrue<TReplacementMetadata>(in System.Func<HigherOrderProposition.HigherOrderBooleanResultEvaluation<TModel, TMetadata>, TReplacementMetadata> function)
        {
            return new Step_16__Motiv_Spec<TModel, TMetadata, TReplacementMetadata>(resultResolver, higherOrderOperation, function);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_16__Motiv_Spec<TModel, TMetadata, TReplacementMetadata> WhenTrue<TReplacementMetadata>(in TReplacementMetadata value)
        {
            return new Step_16__Motiv_Spec<TModel, TMetadata, TReplacementMetadata>(resultResolver, higherOrderOperation, value);
        }
    }
}

namespace Motiv.HigherOrderProposition.PropositionBuilders.Metadata.PolicyResultPredicate
{
    public partial struct TrueHigherOrderFromPolicyResultPredicatePropositionFactory<TModel, TMetadata>
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_22__Motiv_Spec<TModel, TMetadata> WhenTrue(in string trueBecause)
        {
            return new Step_22__Motiv_Spec<TModel, TMetadata>(resultResolver, higherOrderOperation, trueBecause);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_23__Motiv_Spec<TModel, TMetadata> WhenTrueYield(in System.Func<HigherOrderProposition.HigherOrderPolicyResultEvaluation<TModel, TMetadata>, System.Collections.Generic.IEnumerable<string>> function)
        {
            return new Step_23__Motiv_Spec<TModel, TMetadata>(resultResolver, higherOrderOperation, function);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_23__Motiv_Spec<TModel, TMetadata> WhenTrue(in System.Func<HigherOrderProposition.HigherOrderPolicyResultEvaluation<TModel, TMetadata>, string> function)
        {
            return new Step_23__Motiv_Spec<TModel, TMetadata>(resultResolver, higherOrderOperation, function);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_24__Motiv_Spec<TModel, TMetadata, TReplacementMetadata> WhenTrueYield<TReplacementMetadata>(in System.Func<HigherOrderProposition.HigherOrderPolicyResultEvaluation<TModel, TMetadata>, System.Collections.Generic.IEnumerable<TReplacementMetadata>> function)
        {
            return new Step_24__Motiv_Spec<TModel, TMetadata, TReplacementMetadata>(resultResolver, higherOrderOperation, function);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_24__Motiv_Spec<TModel, TMetadata, TReplacementMetadata> WhenTrue<TReplacementMetadata>(in System.Func<HigherOrderProposition.HigherOrderPolicyResultEvaluation<TModel, TMetadata>, TReplacementMetadata> function)
        {
            return new Step_24__Motiv_Spec<TModel, TMetadata, TReplacementMetadata>(resultResolver, higherOrderOperation, function);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_24__Motiv_Spec<TModel, TMetadata, TReplacementMetadata> WhenTrue<TReplacementMetadata>(in TReplacementMetadata value)
        {
            return new Step_24__Motiv_Spec<TModel, TMetadata, TReplacementMetadata>(resultResolver, higherOrderOperation, value);
        }
    }
}

namespace Motiv.ExpressionTreeProposition.PropositionBuilders
{
    public partial struct MinimalExpressionTreePropositionFactory<TModel, TPredicateResult>
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_26__Motiv_Spec<TModel, TPredicateResult> WhenTrue(in System.Func<TModel, BooleanResultBase<string>, string> whenTrue)
        {
            return new Step_26__Motiv_Spec<TModel, TPredicateResult>(expression, whenTrue);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_26__Motiv_Spec<TModel, TPredicateResult> WhenTrue(in System.Func<TModel, string> whenTrue)
        {
            return new Step_26__Motiv_Spec<TModel, TPredicateResult>(expression, whenTrue);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_26__Motiv_Spec<TModel, TPredicateResult> WhenTrue(in string whenTrue)
        {
            return new Step_26__Motiv_Spec<TModel, TPredicateResult>(expression, whenTrue);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_27__Motiv_Spec<TModel, TPredicateResult> WhenTrue(in string trueBecause)
        {
            return new Step_27__Motiv_Spec<TModel, TPredicateResult>(expression, trueBecause);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_28__Motiv_Spec<TModel, TPredicateResult, TMetadata> WhenTrue<TMetadata>(in System.Func<TModel, BooleanResultBase<string>, TMetadata> whenTrue)
        {
            return new Step_28__Motiv_Spec<TModel, TPredicateResult, TMetadata>(expression, whenTrue);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_28__Motiv_Spec<TModel, TPredicateResult, TMetadata> WhenTrue<TMetadata>(in System.Func<TModel, TMetadata> whenTrue)
        {
            return new Step_28__Motiv_Spec<TModel, TPredicateResult, TMetadata>(expression, whenTrue);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_28__Motiv_Spec<TModel, TPredicateResult, TMetadata> WhenTrue<TMetadata>(in TMetadata whenTrue)
        {
            return new Step_28__Motiv_Spec<TModel, TPredicateResult, TMetadata>(expression, whenTrue);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Metadata.ExpressionTree.TrueExpressionTreeHigherOrderFromSpecPropositionFactory<TModel, TPredicateResult> As(in System.Func<System.Collections.Generic.IEnumerable<HigherOrderProposition.BooleanResult<TModel, string>>, bool> higherOrderPredicate, in System.Func<bool, System.Collections.Generic.IEnumerable<HigherOrderProposition.BooleanResult<TModel, string>>, System.Collections.Generic.IEnumerable<HigherOrderProposition.BooleanResult<TModel, string>>> causeSelector)
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Metadata.ExpressionTree.TrueExpressionTreeHigherOrderFromSpecPropositionFactory<TModel, TPredicateResult>(expression, higherOrderPredicate, causeSelector);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Metadata.ExpressionTree.TrueExpressionTreeHigherOrderFromSpecPropositionFactory<TModel, TPredicateResult> As(in System.Func<System.Collections.Generic.IEnumerable<HigherOrderProposition.BooleanResult<TModel, string>>, bool> higherOrderPredicate)
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Metadata.ExpressionTree.TrueExpressionTreeHigherOrderFromSpecPropositionFactory<TModel, TPredicateResult>(expression, higherOrderPredicate);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Metadata.ExpressionTree.TrueExpressionTreeHigherOrderFromSpecPropositionFactory<TModel, TPredicateResult> AsAllSatisfied()
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Metadata.ExpressionTree.TrueExpressionTreeHigherOrderFromSpecPropositionFactory<TModel, TPredicateResult>(expression);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Metadata.ExpressionTree.TrueExpressionTreeHigherOrderFromSpecPropositionFactory<TModel, TPredicateResult> AsAnySatisfied()
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Metadata.ExpressionTree.TrueExpressionTreeHigherOrderFromSpecPropositionFactory<TModel, TPredicateResult>(expression);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Metadata.ExpressionTree.TrueExpressionTreeHigherOrderFromSpecPropositionFactory<TModel, TPredicateResult> AsAtLeastNSatisfied(in int n)
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Metadata.ExpressionTree.TrueExpressionTreeHigherOrderFromSpecPropositionFactory<TModel, TPredicateResult>(expression, n);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Metadata.ExpressionTree.TrueExpressionTreeHigherOrderFromSpecPropositionFactory<TModel, TPredicateResult> AsAtMostNSatisfied(in int n)
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Metadata.ExpressionTree.TrueExpressionTreeHigherOrderFromSpecPropositionFactory<TModel, TPredicateResult>(expression, n);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Metadata.ExpressionTree.TrueExpressionTreeHigherOrderFromSpecPropositionFactory<TModel, TPredicateResult> AsNoneSatisfied()
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Metadata.ExpressionTree.TrueExpressionTreeHigherOrderFromSpecPropositionFactory<TModel, TPredicateResult>(expression);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Metadata.ExpressionTree.TrueExpressionTreeHigherOrderFromSpecPropositionFactory<TModel, TPredicateResult> AsNSatisfied(in int n)
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Metadata.ExpressionTree.TrueExpressionTreeHigherOrderFromSpecPropositionFactory<TModel, TPredicateResult>(expression, n);
        }
    }
}

namespace Motiv.HigherOrderProposition.PropositionBuilders.Metadata.ExpressionTree
{
    public partial struct TrueExpressionTreeHigherOrderFromSpecPropositionFactory<TModel, TPredicateResult>
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_30__Motiv_Spec<TModel, TPredicateResult> WhenTrue(in string trueBecause)
        {
            return new Step_30__Motiv_Spec<TModel, TPredicateResult>(expression, higherOrderOperation, trueBecause);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_31__Motiv_Spec<TModel, TPredicateResult> WhenTrueYield(in System.Func<HigherOrderProposition.HigherOrderBooleanResultEvaluation<TModel, string>, System.Collections.Generic.IEnumerable<string>> function)
        {
            return new Step_31__Motiv_Spec<TModel, TPredicateResult>(expression, higherOrderOperation, function);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_31__Motiv_Spec<TModel, TPredicateResult> WhenTrue(in System.Func<HigherOrderProposition.HigherOrderBooleanResultEvaluation<TModel, string>, string> function)
        {
            return new Step_31__Motiv_Spec<TModel, TPredicateResult>(expression, higherOrderOperation, function);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_32__Motiv_Spec<TModel, TPredicateResult, TMetadata> WhenTrueYield<TMetadata>(in System.Func<HigherOrderProposition.HigherOrderBooleanResultEvaluation<TModel, string>, System.Collections.Generic.IEnumerable<TMetadata>> function)
        {
            return new Step_32__Motiv_Spec<TModel, TPredicateResult, TMetadata>(expression, higherOrderOperation, function);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_32__Motiv_Spec<TModel, TPredicateResult, TMetadata> WhenTrue<TMetadata>(in System.Func<HigherOrderProposition.HigherOrderBooleanResultEvaluation<TModel, string>, TMetadata> function)
        {
            return new Step_32__Motiv_Spec<TModel, TPredicateResult, TMetadata>(expression, higherOrderOperation, function);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_32__Motiv_Spec<TModel, TPredicateResult, TMetadata> WhenTrue<TMetadata>(in TMetadata value)
        {
            return new Step_32__Motiv_Spec<TModel, TPredicateResult, TMetadata>(expression, higherOrderOperation, value);
        }
    }
}

namespace Motiv.SpecDecoratorProposition.PropositionBuilders.Explanation
{
    public partial struct MinimalPolicyDecoratorFactory<TModel, TMetadata>
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Explanation.Policy.HigherOrderFromPolicyPropositionFactory<TModel, TMetadata> As(in System.Func<System.Collections.Generic.IEnumerable<HigherOrderProposition.PolicyResult<TModel, TMetadata>>, bool> higherOrderPredicate, in System.Func<bool, System.Collections.Generic.IEnumerable<HigherOrderProposition.PolicyResult<TModel, TMetadata>>, System.Collections.Generic.IEnumerable<HigherOrderProposition.PolicyResult<TModel, TMetadata>>> causeSelector)
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Explanation.Policy.HigherOrderFromPolicyPropositionFactory<TModel, TMetadata>(policy, higherOrderPredicate, causeSelector);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Explanation.Policy.HigherOrderFromPolicyPropositionFactory<TModel, TMetadata> As(in System.Func<System.Collections.Generic.IEnumerable<HigherOrderProposition.PolicyResult<TModel, TMetadata>>, bool> higherOrderPredicate)
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Explanation.Policy.HigherOrderFromPolicyPropositionFactory<TModel, TMetadata>(policy, higherOrderPredicate);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Explanation.Policy.HigherOrderFromPolicyPropositionFactory<TModel, TMetadata> AsAllSatisfied()
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Explanation.Policy.HigherOrderFromPolicyPropositionFactory<TModel, TMetadata>(policy);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Explanation.Policy.HigherOrderFromPolicyPropositionFactory<TModel, TMetadata> AsAnySatisfied()
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Explanation.Policy.HigherOrderFromPolicyPropositionFactory<TModel, TMetadata>(policy);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Explanation.Policy.HigherOrderFromPolicyPropositionFactory<TModel, TMetadata> AsAtLeastNSatisfied(in int n)
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Explanation.Policy.HigherOrderFromPolicyPropositionFactory<TModel, TMetadata>(policy, n);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Explanation.Policy.HigherOrderFromPolicyPropositionFactory<TModel, TMetadata> AsAtMostNSatisfied(in int n)
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Explanation.Policy.HigherOrderFromPolicyPropositionFactory<TModel, TMetadata>(policy, n);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Explanation.Policy.HigherOrderFromPolicyPropositionFactory<TModel, TMetadata> AsNoneSatisfied()
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Explanation.Policy.HigherOrderFromPolicyPropositionFactory<TModel, TMetadata>(policy);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Motiv.HigherOrderProposition.PropositionBuilders.Explanation.Policy.HigherOrderFromPolicyPropositionFactory<TModel, TMetadata> AsNSatisfied(in int n)
        {
            return new Motiv.HigherOrderProposition.PropositionBuilders.Explanation.Policy.HigherOrderFromPolicyPropositionFactory<TModel, TMetadata>(policy, n);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_36__Motiv_Spec<TModel, TMetadata> WhenTrue(in System.Func<TModel, PolicyResultBase<TMetadata>, string> whenTrue)
        {
            return new Step_36__Motiv_Spec<TModel, TMetadata>(policy, whenTrue);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_36__Motiv_Spec<TModel, TMetadata> WhenTrue(in System.Func<TModel, string> whenTrue)
        {
            return new Step_36__Motiv_Spec<TModel, TMetadata>(policy, whenTrue);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_36__Motiv_Spec<TModel, TMetadata> WhenTrue(in string whenTrue)
        {
            return new Step_36__Motiv_Spec<TModel, TMetadata>(policy, whenTrue);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_37__Motiv_Spec<TModel, TMetadata> WhenTrue(in string trueBecause)
        {
            return new Step_37__Motiv_Spec<TModel, TMetadata>(policy, trueBecause);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_38__Motiv_Spec<TModel, TMetadata, TReplacementMetadata> WhenTrue<TReplacementMetadata>(in System.Func<TModel, PolicyResultBase<TMetadata>, TReplacementMetadata> whenTrue)
        {
            return new Step_38__Motiv_Spec<TModel, TMetadata, TReplacementMetadata>(policy, whenTrue);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_38__Motiv_Spec<TModel, TMetadata, TReplacementMetadata> WhenTrue<TReplacementMetadata>(in System.Func<TModel, TReplacementMetadata> whenTrue)
        {
            return new Step_38__Motiv_Spec<TModel, TMetadata, TReplacementMetadata>(policy, whenTrue);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_38__Motiv_Spec<TModel, TMetadata, TReplacementMetadata> WhenTrue<TReplacementMetadata>(in TReplacementMetadata whenTrue)
        {
            return new Step_38__Motiv_Spec<TModel, TMetadata, TReplacementMetadata>(policy, whenTrue);
        }
    }
}

namespace Motiv.HigherOrderProposition.PropositionBuilders.Explanation.Policy
{
    public partial struct HigherOrderFromPolicyPropositionFactory<TModel, TMetadata>
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public ExplanationFromPolicyWithNameHigherOrderPropositionFactory<TModel, TMetadata> WhenTrue(in string trueBecause)
        {
            return new ExplanationFromPolicyWithNameHigherOrderPropositionFactory<TModel, TMetadata>(policy, higherOrderOperation, trueBecause);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_35__Motiv_Spec<TModel, TMetadata, TReplacementMetadata> WhenTrueYield<TReplacementMetadata>(in System.Func<HigherOrderProposition.HigherOrderPolicyResultEvaluation<TModel, TMetadata>, System.Collections.Generic.IEnumerable<TReplacementMetadata>> function)
        {
            return new Step_35__Motiv_Spec<TModel, TMetadata, TReplacementMetadata>(policy, higherOrderOperation, function);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_35__Motiv_Spec<TModel, TMetadata, TReplacementMetadata> WhenTrue<TReplacementMetadata>(in System.Func<HigherOrderProposition.HigherOrderPolicyResultEvaluation<TModel, TMetadata>, TReplacementMetadata> function)
        {
            return new Step_35__Motiv_Spec<TModel, TMetadata, TReplacementMetadata>(policy, higherOrderOperation, function);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Step_35__Motiv_Spec<TModel, TMetadata, TReplacementMetadata> WhenTrue<TReplacementMetadata>(in TReplacementMetadata value)
        {
            return new Step_35__Motiv_Spec<TModel, TMetadata, TReplacementMetadata>(policy, higherOrderOperation, value);
        }
    }
}