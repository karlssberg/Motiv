using Motiv.Serialization;
using Shouldly;

namespace Motiv.RuleAuthoring.Blazor.Tests;

/// <summary>
/// Task 8: <see cref="RuleDocumentWriter" /> has no way to author a <c>definitions</c> block or a
/// <c>local</c> reference &mdash; <c>DraftNode</c> only models spec leaves and operator nodes, with
/// no concept of a document-local name. Rather than inventing an authoring concept no other task
/// asked for, the writer is left alone (see the comment on <see cref="RuleDocumentWriter" />), and
/// this test instead pins the property that matters: a document carrying <c>definitions</c> and a
/// <c>local</c> node &mdash; however it was produced &mdash; is read by <see cref="RuleSerializer" />
/// unchanged, so the sample's read path is never coupled to what its own writer happens to emit.
/// </summary>
public class DefinitionsAreReadThoughUnwrittenTests
{
    private sealed class Customer(bool isActive)
    {
        public bool IsActive { get; } = isActive;
    }

    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive)
            .WhenTrue("customer is active").WhenFalse("customer is not active").Create();

    private static SpecRegistry Registry() => new SpecRegistry()
        .Register("customer.is-active", IsActive);

    [Fact]
    public void Reads_a_document_with_definitions_and_a_local_reference()
    {
        // Arrange — a hand-written document in the shape the writer cannot produce.
        const string json =
            """
            {
              "definitions": {
                "is-active": {
                  "rule": { "spec": "customer.is-active" }
                }
              },
              "rule": { "local": "is-active" }
            }
            """;

        // Act
        var spec = new RuleSerializer(Registry()).Deserialize<Customer>(json);

        // Assert — the local binds as `Spec.Build(body).Create(name)`, exactly as it does for the
        // document format generally: Reason takes the `name == true`/`== false` suffix, and the
        // definition's own assertion surfaces underneath.
        var whenActive = spec.Evaluate(new Customer(true));
        whenActive.Reason.ShouldBe("is-active == true");
        whenActive.Assertions.ShouldBe(["customer is active"]);

        var whenInactive = spec.Evaluate(new Customer(false));
        whenInactive.Reason.ShouldBe("is-active == false");
        whenInactive.Assertions.ShouldBe(["customer is not active"]);
    }
}
