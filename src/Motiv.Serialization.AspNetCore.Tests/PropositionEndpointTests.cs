using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace Motiv.Serialization.AspNetCore.Tests;

public class PropositionEndpointTests
{
    // Matches MotivRulesOptions.JsonSerializerOptions (Web defaults + string enums), which is what
    // the server actually serializes RuleError.Code with — the default HttpClient JSON options read
    // enums as numbers and would fail to deserialize a response containing a RuleError.
    private static readonly JsonSerializerOptions ResponseJson =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private sealed record Customer(bool IsActive, int Age);

    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private static SpecBase<Customer, string> IsAdult { get; } =
        Spec.Build((Customer c) => c.Age >= 18).WhenTrue("adult").WhenFalse("minor").Create();

    private static async Task<WebApplication> StartAsync()
    {
        var registry = new SpecRegistry()
            .Register("customer.is-active", IsActive)
            .Register("customer.is-adult", IsAdult);
        var options = new MotivRulesOptions().AddModel<Customer>("customer");
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddMotivRules(registry, options).AddPropositions();
        var app = builder.Build();
        app.MapMotivRules("/api/rules");
        await app.StartAsync();
        return app;
    }

    private static Task<HttpResponseMessage> Create(
        HttpClient client, string name, string document, string modelType = "customer") =>
        client.PostAsJsonAsync("/api/rules/propositions", new
        {
            name, modelType, document = JsonDocument.Parse(document).RootElement, description = (string?)null,
        });

    private static Task<HttpResponseMessage> Put(HttpClient client, string name, string document, int baseVersion) =>
        client.PutAsJsonAsync($"/api/rules/propositions/{name}", new
        {
            document = JsonDocument.Parse(document).RootElement, baseVersion,
        });

    [Fact]
    public async Task Should_evaluate_a_document_that_references_an_authored_proposition()
    {
        // Arrange: the catalog lists an authored proposition, so a document may reference it — and
        // the rule endpoints will bind one that does, because RuleSet resolves through the
        // BindingScope. /evaluate has to agree, or the catalog advertises what it cannot evaluate.
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        await Create(client, "customer.is-eligible",
            """
            { "rule": { "and": [ { "spec": "customer.is-active" }, { "spec": "customer.is-adult" } ] } }
            """);

        // Act
        var response = await client.PostAsJsonAsync("/api/rules/evaluate", new
        {
            modelType = "customer",
            document = JsonDocument.Parse("""{ "rule": { "spec": "customer.is-eligible" } }""").RootElement,
            model = JsonDocument.Parse("""{ "isActive": true, "age": 30 }""").RootElement
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ResponseJson);
        body.GetProperty("satisfied").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Should_validate_a_document_that_references_an_authored_proposition()
    {
        // Arrange: /validate answers 200 with an errors array rather than a status code, so a
        // regression here would surface as live validation marking a legal document unknown.
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        await Create(client, "customer.is-eligible",
            """
            { "rule": { "and": [ { "spec": "customer.is-active" }, { "spec": "customer.is-adult" } ] } }
            """);

        // Act
        var response = await client.PostAsJsonAsync("/api/rules/validate", new
        {
            modelType = "customer",
            document = JsonDocument.Parse("""{ "rule": { "spec": "customer.is-eligible" } }""").RootElement
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ResponseJson);
        body.GetProperty("errors").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Should_create_a_proposition_with_201()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();

        // Act
        var response = await Create(client, "customer.derived", """{ "rule": { "spec": "customer.is-active" } }""");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<PropositionSaveResponse>();
        body!.Version.ShouldBe(1);
    }

    [Fact]
    public async Task Should_reject_a_duplicate_name_with_409()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        await Create(client, "customer.derived", """{ "rule": { "spec": "customer.is-active" } }""");

        // Act
        var response = await Create(client, "customer.derived", """{ "rule": { "spec": "customer.is-adult" } }""");

        // Assert — the distinguishing shape of this 409 (plain ErrorResponse, unlike the
        // RuleConflictResponse/PropositionReferencedResponse shapes the other two 409s carry)
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        body!.Error.ShouldContain("customer.derived");
    }

    [Fact]
    public async Task Should_accept_creating_an_override_of_a_compiled_spec()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();

        // Act — "taken" means an authored document exists, not that the name is known at all
        var response = await Create(client, "customer.is-active", """{ "rule": { "spec": "customer.is-adult" } }""");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Should_reject_a_missing_model_type_with_400()
    {
        // Arrange — modelType omitted entirely (not merely empty), the one shape the non-required
        // reference-type property lets through as null past model binding
        await using var app = await StartAsync();
        var client = app.GetTestClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/rules/propositions", new
        {
            name = "customer.derived",
            document = JsonDocument.Parse("""{ "rule": { "spec": "customer.is-active" } }""").RootElement,
            description = (string?)null,
        });

        // Assert — a typed 400, not the 500 an unguarded null modelType reaching
        // Dictionary.TryGetValue(null) inside PropositionSet.Create would produce
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        body!.Error.ShouldContain("modelType");
    }

    [Fact]
    public async Task Should_reject_an_invalid_document_with_400_and_typed_errors()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();

        // Act
        var response = await Create(client, "customer.derived", """{ "rule": { "spec": "nope" } }""");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<CascadeFailureResponse>(ResponseJson);
        body!.Errors.ShouldContain(error => error.Code == RuleErrorCode.UnknownSpec);
    }

    [Fact]
    public async Task Should_list_compiled_and_authored_propositions_with_their_origin()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        await Create(client, "customer.derived", """{ "rule": { "spec": "customer.is-active" } }""");

        // Act
        var listed = await client.GetFromJsonAsync<List<PropositionListEntry>>("/api/rules/propositions");

        // Assert
        var byName = listed!.ToDictionary(entry => entry.Name);
        byName["customer.is-active"].Origin.ShouldBe("Compiled");
        byName["customer.derived"].Origin.ShouldBe("Authored");
        byName["customer.derived"].Version.ShouldBe(1);
    }

    [Fact]
    public async Task Should_get_an_authored_propositions_document()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        await Create(client, "customer.derived", """{ "rule": { "spec": "customer.is-active" } }""");

        // Act
        var body = await client.GetFromJsonAsync<PropositionGetResponse>("/api/rules/propositions/customer.derived");

        // Assert
        body!.Version.ShouldBe(1);
        body.Origin.ShouldBe("Authored");
        body.HasCompiledDefault.ShouldBeFalse();
        body.Document.ShouldNotBeNull();
    }

    [Fact]
    public async Task Should_report_a_compiled_proposition_as_having_no_document()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();

        // Act
        var body = await client.GetFromJsonAsync<PropositionGetResponse>("/api/rules/propositions/customer.is-active");

        // Assert
        body!.Document.ShouldBeNull();
        body.Origin.ShouldBe("Compiled");
        body.HasCompiledDefault.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_return_404_for_an_unknown_name()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();

        // Act
        var response = await client.GetAsync("/api/rules/propositions/absent");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_update_a_proposition_and_return_the_new_version()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        await Create(client, "customer.derived", """{ "rule": { "spec": "customer.is-active" } }""");

        // Act
        var response = await Put(client, "customer.derived", """{ "rule": { "spec": "customer.is-adult" } }""", 1);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<PropositionSaveResponse>())!.Version.ShouldBe(2);
    }

    [Fact]
    public async Task Should_reject_a_stale_base_version_with_409()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        await Create(client, "customer.derived", """{ "rule": { "spec": "customer.is-active" } }""");
        await Put(client, "customer.derived", """{ "rule": { "spec": "customer.is-adult" } }""", 1);

        // Act
        var response = await Put(client, "customer.derived", """{ "rule": { "spec": "customer.is-active" } }""", 1);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<RuleConflictResponse>();
        body!.CurrentVersion.ShouldBe(2);
    }

    [Fact]
    public async Task Should_reject_a_non_positive_base_version_with_400()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        await Create(client, "customer.derived", """{ "rule": { "spec": "customer.is-active" } }""");

        // Act
        var response = await Put(client, "customer.derived", """{ "rule": { "spec": "customer.is-adult" } }""", 0);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_report_document_errors_for_an_invalid_edit()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        await Create(client, "customer.a", """{ "rule": { "spec": "customer.is-active" } }""");

        // Act
        var response = await Put(client, "customer.a", """{ "rule": { "spec": "nope" } }""", 1);

        // Assert — the HTTP-level job here is proving a rejected edit returns typed errors rather
        // than an empty 400 body. The cascade-break path (a sync rule that can no longer bind) is
        // covered at the unit level by RuleCascadeTests in Task 10.
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<CascadeFailureResponse>(ResponseJson);
        body!.Errors.ShouldContain(error => error.Code == RuleErrorCode.UnknownSpec);
        body.BrokenDependents.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_report_the_transitive_dependents_of_a_proposition()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        await Create(client, "customer.a", """{ "rule": { "spec": "customer.is-active" } }""");
        await Create(client, "customer.b", """{ "rule": { "spec": "customer.a" } }""");
        await Create(client, "customer.c", """{ "rule": { "spec": "customer.b" } }""");

        // Act
        var body = await client.GetFromJsonAsync<DependentsResponse>(
            "/api/rules/propositions/customer.a/dependents");

        // Assert
        body!.Dependents.Select(dependent => dependent.Name).ShouldBe(["customer.b", "customer.c"]);
        body.Dependents.ShouldAllBe(dependent => dependent.Kind == "proposition");
    }

    [Fact]
    public async Task Should_delete_an_unreferenced_proposition()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        await Create(client, "customer.derived", """{ "rule": { "spec": "customer.is-active" } }""");

        // Act
        var response = await client.DeleteAsync("/api/rules/propositions/customer.derived?baseVersion=1");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/api/rules/propositions/customer.derived")).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_reject_a_stale_base_version_on_delete_with_409_carrying_the_current_version()
    {
        // Arrange — Withdraw checks the version *before* the referrer check, so a stale baseVersion
        // answers the version-conflict shape { currentVersion }, not { referrers }
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        await Create(client, "customer.derived", """{ "rule": { "spec": "customer.is-active" } }""");
        await Put(client, "customer.derived", """{ "rule": { "spec": "customer.is-adult" } }""", 1);

        // Act
        var response = await client.DeleteAsync("/api/rules/propositions/customer.derived?baseVersion=1");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<RuleConflictResponse>();
        body!.CurrentVersion.ShouldBe(2);
    }

    [Fact]
    public async Task Should_return_404_when_updating_an_unknown_name()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();

        // Act
        var response = await Put(client, "absent", """{ "rule": { "spec": "customer.is-active" } }""", 1);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_return_404_when_deleting_an_unknown_name()
    {
        // Arrange — a name that exists only as a compiled spec has no authored document to
        // withdraw, so it is 404 too, not a revert
        await using var app = await StartAsync();
        var client = app.GetTestClient();

        // Act
        var response = await client.DeleteAsync("/api/rules/propositions/customer.is-active?baseVersion=1");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_return_404_for_the_dependents_of_an_unknown_name()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();

        // Act
        var response = await client.GetAsync("/api/rules/propositions/absent/dependents");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_refuse_to_delete_a_referenced_proposition_with_409_listing_referrers()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        await Create(client, "customer.a", """{ "rule": { "spec": "customer.is-active" } }""");
        await Create(client, "customer.b", """{ "rule": { "spec": "customer.a" } }""");

        // Act
        var response = await client.DeleteAsync("/api/rules/propositions/customer.a?baseVersion=1");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<PropositionReferencedResponse>();
        body!.Referrers.ShouldBe(["customer.b"]);
    }

    [Fact]
    public async Task Should_revert_an_override_to_its_compiled_spec()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        await Create(client, "customer.is-active", """{ "rule": { "spec": "customer.is-adult" } }""");

        // Assert (pre-condition) — the override is what deleting is about to revert, and
        // HasCompiledDefault is the field a UI reads to know DELETE will revert rather than remove
        var before = await client.GetFromJsonAsync<PropositionGetResponse>(
            "/api/rules/propositions/customer.is-active");
        before!.Origin.ShouldBe("Overridden");
        before.HasCompiledDefault.ShouldBeTrue();

        // Act
        var response = await client.DeleteAsync("/api/rules/propositions/customer.is-active?baseVersion=1");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await client.GetFromJsonAsync<PropositionGetResponse>(
            "/api/rules/propositions/customer.is-active");
        body!.Origin.ShouldBe("Compiled");
        body.Document.ShouldBeNull();
    }
}
