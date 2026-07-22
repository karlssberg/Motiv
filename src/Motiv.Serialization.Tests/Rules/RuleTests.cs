namespace Motiv.Serialization.Tests.Rules;

public class RuleTests
{
    // Plain class (not a record) so the net472 target compiles without an IsExternalInit polyfill.
    private sealed class Customer(bool isActive)
    {
        public bool IsActive { get; } = isActive;
    }

    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private sealed class ActiveRule() : Rule<Customer, string>("is-active-rule", IsActive, "demo rule");

    private static SpecRegistry Registry() => new SpecRegistry().Register("is-active", IsActive);

    [Fact]
    public void Should_evaluate_the_compiled_default_at_version_1()
    {
        // Arrange
        var rule = new ActiveRule();
        new RuleSet(Registry()).Add(rule);

        // Act
        var result = rule.Evaluate(new Customer(true));

        // Assert
        result.Satisfied.ShouldBeTrue();
        result.Assertions.ShouldBe(["active"]);
        rule.Version.ShouldBe(1);
        rule.DocumentJson.ShouldBeNull();     // compiled default has no document
    }

    [Fact]
    public void Should_expose_identity()
    {
        // Arrange & Act
        var rule = new ActiveRule();

        // Assert
        rule.Name.ShouldBe("is-active-rule");
        rule.Description.ShouldNotBeNull().ShouldBe("demo rule");
        rule.ModelType.ShouldBe(typeof(Customer));
        rule.MetadataType.ShouldBe(typeof(string));
        rule.IsAsync.ShouldBeFalse();
    }

    [Fact]
    public void Should_throw_when_evaluated_before_being_added_to_a_rule_set()
    {
        // Arrange
        var rule = new ActiveRule();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => rule.Evaluate(new Customer(true)))
            .Message.ShouldContain("RuleSet");
    }

    [Fact]
    public void Should_swap_to_an_updated_document_and_bump_the_version()
    {
        // Arrange
        var rule = new ActiveRule();
        var rules = new RuleSet(Registry()).Add(rule);
        var document = """{ "rule": { "not": { "spec": "is-active" } } }""";

        // Act
        var outcome = rules.Update("is-active-rule", document, expectedVersion: 1);

        // Assert
        outcome.Outcome.ShouldBe(RuleUpdateOutcome.Updated);
        outcome.Version.ShouldBe(2);
        rule.Version.ShouldBe(2);
        rule.DocumentJson.ShouldNotBeNull();
        rule.Evaluate(new Customer(true)).Satisfied.ShouldBeFalse();   // negated now
    }

    [Fact]
    public void Should_report_a_version_conflict_for_a_stale_expected_version()
    {
        // Arrange
        var rule = new ActiveRule();
        var rules = new RuleSet(Registry()).Add(rule);
        rules.Update("is-active-rule", """{ "rule": { "spec": "is-active" } }""", 1)
            .Outcome.ShouldBe(RuleUpdateOutcome.Updated);

        // Act — second writer still believes version 1
        var outcome = rules.Update("is-active-rule", """{ "rule": { "spec": "is-active" } }""", 1);

        // Assert
        outcome.Outcome.ShouldBe(RuleUpdateOutcome.VersionConflict);
        outcome.Version.ShouldBe(2);
        rule.Version.ShouldBe(2);
    }

    [Fact]
    public void Should_report_binding_errors_without_touching_the_live_rule()
    {
        // Arrange
        var rule = new ActiveRule();
        var rules = new RuleSet(Registry()).Add(rule);

        // Act
        var outcome = rules.Update("is-active-rule", """{ "rule": { "spec": "missing" } }""", 1);

        // Assert
        outcome.Outcome.ShouldBe(RuleUpdateOutcome.Invalid);
        outcome.Errors.ShouldHaveSingleItem().Code.ShouldBe(RuleErrorCode.UnknownSpec);
        rule.Version.ShouldBe(1);
        rule.Evaluate(new Customer(true)).Satisfied.ShouldBeTrue();    // untouched
    }

    [Fact]
    public void Should_revert_to_the_compiled_default_and_bump_the_version()
    {
        // Arrange
        var rule = new ActiveRule();
        var rules = new RuleSet(Registry()).Add(rule);
        rules.Update("is-active-rule", """{ "rule": { "not": { "spec": "is-active" } } }""", 1);

        // Act
        var outcome = rules.Revert("is-active-rule", expectedVersion: 2);

        // Assert — revert is an update: version moves forward, never back (no ABA)
        outcome.Outcome.ShouldBe(RuleUpdateOutcome.Updated);
        outcome.Version.ShouldBe(3);
        rule.DocumentJson.ShouldBeNull();
        rule.Evaluate(new Customer(true)).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public void Should_report_a_version_conflict_when_reverting_with_a_stale_version()
    {
        // Arrange
        var rule = new ActiveRule();
        var rules = new RuleSet(Registry()).Add(rule);
        rules.Update("is-active-rule", """{ "rule": { "spec": "is-active" } }""", 1);

        // Act — the caller still believes version 1
        var outcome = rules.Revert("is-active-rule", expectedVersion: 1);

        // Assert
        outcome.Outcome.ShouldBe(RuleUpdateOutcome.VersionConflict);
        outcome.Version.ShouldBe(2);
        rule.Version.ShouldBe(2);
    }

    private sealed class BlockingBindRule() : Rule<Customer, string>("blocking-rule", IsActive)
    {
        public SemaphoreSlim BindEntered { get; } = new(0, 1);
        public SemaphoreSlim ResumeBind { get; } = new(0, 1);
        public volatile bool BlockNextBind;

        private protected override SpecBase<Customer, string> Bind(RuleSerializer serializer, string documentJson)
        {
            if (BlockNextBind)
            {
                BlockNextBind = false;
                BindEntered.Release();
                ResumeBind.Wait();
            }

            return base.Bind(serializer, documentJson);
        }
    }

    [Fact]
    public async Task Should_report_a_version_conflict_when_a_publish_loses_the_cas_race()
    {
        // Arrange — update A passes the version precheck, then blocks inside Bind while
        // update B publishes underneath; A's CAS must lose and report B's version.
        var rule = new BlockingBindRule();
        var rules = new RuleSet(Registry()).Add(rule);
        rule.BlockNextBind = true;

        var updateA = Task.Run(() =>
            rules.Update("blocking-rule", """{ "rule": { "spec": "is-active" } }""", 1));
        await rule.BindEntered.WaitAsync();

        rules.Update("blocking-rule", """{ "rule": { "not": { "spec": "is-active" } } }""", 1)
            .Outcome.ShouldBe(RuleUpdateOutcome.Updated);

        // Act
        rule.ResumeBind.Release();
        var outcome = await updateA;

        // Assert
        outcome.Outcome.ShouldBe(RuleUpdateOutcome.VersionConflict);
        outcome.Version.ShouldBe(2);
        rule.Version.ShouldBe(2);
        rule.Evaluate(new Customer(true)).Satisfied.ShouldBeFalse();   // B's negated document won
    }

    private sealed class FailingRevertRule() : Rule<Customer, string>(
        "failing-revert", RuleDocuments.FromJson("""{ "rule": { "spec": "is-active" } }"""))
    {
        public volatile bool FailNextBind;

        private protected override SpecBase<Customer, string> Bind(RuleSerializer serializer, string documentJson)
        {
            if (FailNextBind)
            {
                FailNextBind = false;
                throw new RuleSerializationException(
                    [new RuleError("$.rule", RuleErrorCode.UnknownSpec, "simulated bind failure")]);
            }

            return base.Bind(serializer, documentJson);
        }
    }

    [Fact]
    public void Should_report_binding_errors_from_revert_without_touching_the_live_rule()
    {
        // Arrange — a document default that binds at Add but fails when revert rebinds it
        var rule = new FailingRevertRule();
        var rules = new RuleSet(Registry()).Add(rule);
        rules.Update("failing-revert", """{ "rule": { "not": { "spec": "is-active" } } }""", 1);
        rule.FailNextBind = true;

        // Act
        var outcome = rules.Revert("failing-revert", expectedVersion: 2);

        // Assert
        outcome.Outcome.ShouldBe(RuleUpdateOutcome.Invalid);
        outcome.Errors.ShouldHaveSingleItem().Code.ShouldBe(RuleErrorCode.UnknownSpec);
        rule.Version.ShouldBe(2);
        rule.Evaluate(new Customer(true)).Satisfied.ShouldBeFalse();   // untouched
    }

    [Fact]
    public void Should_throw_for_a_null_update_document()
    {
        // Arrange
        var rules = new RuleSet(Registry()).Add(new ActiveRule());

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => rules.Update("is-active-rule", null!, 1));
    }

    [Fact]
    public void Should_throw_when_a_rule_is_added_to_a_second_rule_set()
    {
        // Arrange
        var rule = new ActiveRule();
        new RuleSet(Registry()).Add(rule);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => new RuleSet(Registry()).Add(rule))
            .Message.ShouldContain("already");
    }

    [Fact]
    public void Should_throw_for_a_duplicate_rule_name()
    {
        // Arrange
        var rules = new RuleSet(Registry()).Add(new ActiveRule());

        // Act & Assert
        Should.Throw<ArgumentException>(() => rules.Add(new ActiveRule()))
            .Message.ShouldContain("is-active-rule");
    }

    [Fact]
    public void Should_list_registered_rules_with_live_versions()
    {
        // Arrange
        var rule = new ActiveRule();
        var rules = new RuleSet(Registry()).Add(rule);
        rules.Update("is-active-rule", """{ "rule": { "spec": "is-active" } }""", 1);

        // Act
        var entry = rules.Rules.ShouldHaveSingleItem();

        // Assert
        rules.Count.ShouldBe(1);
        entry.Name.ShouldBe("is-active-rule");
        entry.ModelType.ShouldBe(typeof(Customer));
        entry.MetadataType.ShouldBe(typeof(string));
        entry.IsAsync.ShouldBeFalse();
        entry.IsPolicy.ShouldBeFalse();
        entry.Version.ShouldBe(2);
        entry.Description.ShouldNotBeNull().ShouldBe("demo rule");
        entry.DocumentJson.ShouldNotBeNull();
    }

    [Fact]
    public void Should_report_not_found_for_unknown_rule_names()
    {
        // Arrange
        var rules = new RuleSet(Registry());

        // Act & Assert
        rules.Find("nope").ShouldBeNull();
        rules.Update("nope", """{ "rule": { "spec": "is-active" } }""", 1)
            .Outcome.ShouldBe(RuleUpdateOutcome.NotFound);
        rules.Revert("nope", 1).Outcome.ShouldBe(RuleUpdateOutcome.NotFound);
    }

    [Fact]
    public void Should_find_a_single_entry_by_name()
    {
        // Arrange
        var rule = new ActiveRule();
        var rules = new RuleSet(Registry()).Add(rule);
        rules.Update("is-active-rule", """{ "rule": { "spec": "is-active" } }""", 1);

        // Act
        var entry = rules.FindEntry("is-active-rule").ShouldNotBeNull();

        // Assert
        entry.Name.ShouldBe("is-active-rule");
        entry.ModelType.ShouldBe(typeof(Customer));
        entry.MetadataType.ShouldBe(typeof(string));
        entry.IsAsync.ShouldBeFalse();
        entry.IsPolicy.ShouldBeFalse();
        entry.Version.ShouldBe(2);
        entry.Description.ShouldNotBeNull().ShouldBe("demo rule");
        entry.DocumentJson.ShouldNotBeNull();
    }

    [Fact]
    public void Should_return_null_from_find_entry_for_an_unknown_rule_name()
    {
        // Arrange
        var rules = new RuleSet(Registry());

        // Act & Assert
        rules.FindEntry("nope").ShouldBeNull();
    }

    [Fact]
    public void Should_serve_a_coherent_version_and_document_from_find_entry()
    {
        // Arrange
        var rule = new ActiveRule();
        var rules = new RuleSet(Registry()).Add(rule);

        // Act & Assert — compiled default: version 1 with no document
        var initial = rules.FindEntry("is-active-rule").ShouldNotBeNull();
        initial.Version.ShouldBe(1);
        initial.DocumentJson.ShouldBeNull();

        // Act & Assert — after an update, both fields move together
        rules.Update("is-active-rule", """{ "rule": { "not": { "spec": "is-active" } } }""", 1);
        var updated = rules.FindEntry("is-active-rule").ShouldNotBeNull();
        updated.Version.ShouldBe(2);
        updated.DocumentJson.ShouldNotBeNull();
    }

    private sealed class DocumentRule() : Rule<Customer, string>(
        "doc-rule", RuleDocuments.FromJson("""{ "rule": { "spec": "is-active" } }"""));

    private sealed class BrokenDocumentRule() : Rule<Customer, string>(
        "broken-rule", RuleDocuments.FromJson("""{ "rule": { "spec": "missing" } }"""));

    [Fact]
    public void Should_bind_a_document_default_at_add_and_serve_its_document()
    {
        // Arrange
        var rule = new DocumentRule();

        // Act
        new RuleSet(Registry()).Add(rule);

        // Assert
        rule.Version.ShouldBe(1);
        rule.DocumentJson.ShouldNotBeNull();
        rule.Evaluate(new Customer(true)).Satisfied.ShouldBeTrue();
    }

    [Fact]
    public void Should_fail_fast_at_add_for_an_invalid_document_default()
    {
        // Act & Assert
        Should.Throw<RuleSerializationException>(() => new RuleSet(Registry()).Add(new BrokenDocumentRule()));
    }

    [Fact]
    public async Task Should_never_yield_a_torn_state_under_concurrent_update_and_evaluation()
    {
        // Arrange — hammer evaluation while a writer alternates between two documents.
        // Assertions alone cannot distinguish them (both surface "active" — pinned from
        // actual output; the NOT wrapper surfaces its operand's assertion), so coherence
        // is asserted two ways: every VersionedDocument() read must pair a version with
        // its own document (parity), and whenever the version is stable across an
        // evaluation, the outcome must match that version's document.
        var rule = new ActiveRule();
        var rules = new RuleSet(Registry()).Add(rule);
        var positive = """{ "rule": { "spec": "is-active" } }""";
        var negated = """{ "rule": { "not": { "spec": "is-active" } } }""";
        var model = new Customer(true);
        using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        var writer = Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                var version = rule.Version;
                rules.Update("is-active-rule", version % 2 == 1 ? negated : positive, version);
            }
        });

        // Act & Assert
        var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                var (versionBefore, documentJson) = rule.VersionedDocument();
                var result = rule.Evaluate(model);
                var (versionAfter, _) = rule.VersionedDocument();

                result.Assertions.ShouldBe(["active"]);

                // Coherence: the writer publishes the negated document over odd versions,
                // so even versions must carry it; version 1 is the compiled default.
                if (versionBefore == 1)
                    documentJson.ShouldBeNull();
                else if (versionBefore % 2 == 0)
                    documentJson.ShouldNotBeNull().ShouldBe(negated);
                else
                    documentJson.ShouldNotBeNull().ShouldBe(positive);

                // Versions are monotonic, so an unchanged version means no swap happened
                // during the evaluation — the outcome must match that version's document.
                if (versionBefore == versionAfter)
                    result.Satisfied.ShouldBe(versionBefore % 2 == 1);
            }
        })).ToArray();

        await Task.WhenAll(readers.Append(writer));
    }
}
