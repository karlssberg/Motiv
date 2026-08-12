using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Motiv.Serialization.AspNetCore.Tests;

/// <summary>
/// The 3am escape end to end: a deploy-time <see cref="BreakGlass"/> bypasses a blocking gate for
/// both a direct write and a workflow publish, stamps the durable record, and is loud about it via
/// the <c>MOTIV-AUDIT</c> log line under the <c>Motiv.Governance.Audit</c> category — but only for a
/// write that actually landed, and stops working the moment the window expires.
/// </summary>
public class BreakGlassTests
{
    private const string MakerCheckerGate =
        """
        {"rule": {"and": [
            {"spec": "change.approver-count-at-least", "args": {"n": 1}},
            {"not": {"spec": "change.author-is-approver"}}
        ]}}
        """;

    private const string RuleName = "checkout.can-checkout";
    private const string AuditMarker = "MOTIV-AUDIT break-glass publish";
    private const string AuditCategory = "Motiv.Governance.Audit";

    private sealed record Customer(bool IsActive, int Age);

    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private static SpecBase<Customer, string> IsAdult { get; } =
        Spec.Build((Customer c) => c.Age >= 18).WhenTrue("adult").WhenFalse("minor").Create();

    private sealed class CanCheckoutRule() : Rule<Customer, string>(RuleName, IsActive);

    /// <summary>The one document every test swaps the rule to — it binds, and it is not the default.</summary>
    private static JsonElement AdultDocument =>
        JsonDocument.Parse("""{ "rule": { "spec": "customer.is-adult" } }""").RootElement;

    /// <summary>
    /// A host with a blocking maker-checker gate already installed, and — when supplied — a
    /// <see cref="BreakGlass"/> singleton registered over <c>AddGovernance</c>'s
    /// <see cref="BreakGlass.Off"/> default (AddSingleton after AddGovernance beats its TryAdd, the
    /// same override order the sample host uses). A <see cref="CapturingLoggerProvider"/> is wired in
    /// regardless, so every test can inspect what — if anything — was audit-logged, and under which
    /// category.
    /// </summary>
    private static async Task<(WebApplication App, CapturingLoggerProvider Logs)> StartAsync(BreakGlass? breakGlass = null)
    {
        var registry = new SpecRegistry()
            .Register("customer.is-active", IsActive)
            .Register("customer.is-adult", IsAdult);
        var options = new MotivRulesOptions().AddModel<Customer>("customer");
        var logs = new CapturingLoggerProvider();

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddTestAuth();
        builder.Services.AddLogging(logging => logging.AddProvider(logs));

        builder.Services.AddMotivRules(registry, options)
            .AddRule<CanCheckoutRule>()
            .AddGovernance();

        if (breakGlass is not null)
            builder.Services.AddSingleton(breakGlass);

        var app = builder.Build();
        app.UseTestAuth();
        app.MapMotivRules("/api/rules");
        app.Services.GetRequiredService<ApprovalGate>()
            .SetGate(MakerCheckerGate, []).Outcome.ShouldBe(GateUpdateOutcome.Updated);

        await app.StartAsync();
        return (app, logs);
    }

    private static Task<HttpResponseMessage> Send(
        HttpClient client, HttpMethod method, string url, string? user = null, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (user is not null)
            request.Headers.Add(TestAuthHandler.SubjectHeader, user);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return client.SendAsync(request);
    }

    private static object OneRuleChange(string name, JsonElement document, int baseVersion) => new
    {
        changeNote = "swap the eligibility check",
        changes = new[] { new { kind = "rule", name, document, baseVersion } }
    };

    private static async Task<Guid> IdOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

    /// <summary>Whether any captured entry is an audit line, in the audit category, matching <paramref name="predicate"/>.</summary>
    private static bool HasAuditEntry(CapturingLoggerProvider logs, Func<string, bool> predicate) =>
        logs.Entries.Any(entry =>
            entry.Category == AuditCategory
            && entry.Message.Contains(AuditMarker, StringComparison.Ordinal)
            && predicate(entry.Message));

    [Fact]
    public async Task Should_bypass_a_blocking_gate_for_a_direct_write_and_audit_log_it()
    {
        // Arrange — a blocking maker-checker gate (StartAsync installs it), and break-glass on with
        // no expiry
        var (app, logs) = await StartAsync(new BreakGlass(true, null));
        await using var _ = app;
        var client = app.GetTestClient();

        // Act — the direct write is a change request with no approvals on it, which the gate would
        // ordinarily refuse
        var direct = await client.PutAsJsonAsync($"/api/rules/rules/{RuleName}",
            new { document = AdultDocument, baseVersion = 1 });

        // Assert — it lands anyway, and the bypass is loudly audit-logged under the audit category
        direct.StatusCode.ShouldBe(HttpStatusCode.OK);
        HasAuditEntry(logs, message => message.Contains(RuleName, StringComparison.Ordinal)).ShouldBeTrue();
    }

    [Fact]
    public async Task Should_publish_an_unapproved_change_request_under_break_glass_stamping_and_auditing_it()
    {
        // Arrange
        var (app, logs) = await StartAsync(new BreakGlass(true, null));
        await using var _ = app;
        var client = app.GetTestClient();
        var id = await IdOf(await Send(client, HttpMethod.Post, "/api/rules/change-requests",
            "author", OneRuleChange(RuleName, AdultDocument, 1)));

        // Act — nobody approved it; only break-glass gets this through
        var published = await Send(client, HttpMethod.Post, $"/api/rules/change-requests/{id}/publish", "author");

        // Assert — publishes, the durable stamp shows on the response, and the same event is
        // audit-logged, under the audit category, naming the change request's own id
        published.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await published.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("request").GetProperty("publishedUnderBreakGlass").GetBoolean().ShouldBeTrue();
        HasAuditEntry(logs, message => message.Contains(id.ToString(), StringComparison.Ordinal)).ShouldBeTrue();
    }

    [Fact]
    public async Task Should_leave_the_gate_blocking_once_the_break_glass_window_has_expired()
    {
        // Arrange — break-glass configured, but its window closed five minutes ago
        var (app, logs) = await StartAsync(new BreakGlass(true, DateTimeOffset.UtcNow.AddMinutes(-5)));
        await using var _ = app;
        var client = app.GetTestClient();

        // Act
        var direct = await client.PutAsJsonAsync($"/api/rules/rules/{RuleName}",
            new { document = AdultDocument, baseVersion = 1 });

        // Assert — refused exactly as it would be with no break-glass at all, and nothing audit-logged
        direct.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        HasAuditEntry(logs, _ => true).ShouldBeFalse();
    }

    [Fact]
    public async Task Should_refuse_a_stale_direct_write_as_a_conflict_under_break_glass_without_auditing_it()
    {
        // Arrange — break-glass on, so the gate is skipped entirely, but the write itself is doomed
        // regardless: a stale base version. The core still refuses it on its own terms, and since
        // nothing actually published, there must be no audit entry for it — an audit line here would
        // be a record of a publish that never happened, and for a direct write the audit log is the
        // only record there is.
        var (app, logs) = await StartAsync(new BreakGlass(true, null));
        await using var _ = app;
        var client = app.GetTestClient();

        // Act — the rule is at version 1; ask to replace version 2
        var direct = await client.PutAsJsonAsync($"/api/rules/rules/{RuleName}",
            new { document = AdultDocument, baseVersion = 2 });

        // Assert — a genuine version conflict, not a gate refusal, and no audit entry despite
        // break-glass being active throughout
        direct.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        HasAuditEntry(logs, _ => true).ShouldBeFalse();
    }

    [Fact]
    public void Active_reports_true_when_enabled_with_no_expiry()
    {
        new BreakGlass(true, null).Active(DateTimeOffset.UtcNow).ShouldBeTrue();
    }

    [Fact]
    public void Active_reports_true_when_enabled_and_not_yet_expired()
    {
        new BreakGlass(true, DateTimeOffset.UtcNow.AddMinutes(5)).Active(DateTimeOffset.UtcNow).ShouldBeTrue();
    }

    [Fact]
    public void Active_reports_false_when_enabled_but_expired()
    {
        new BreakGlass(true, DateTimeOffset.UtcNow.AddMinutes(-5)).Active(DateTimeOffset.UtcNow).ShouldBeFalse();
    }

    [Fact]
    public void Active_reports_false_when_disabled_regardless_of_expiry()
    {
        BreakGlass.Off.Active(DateTimeOffset.UtcNow).ShouldBeFalse();
        new BreakGlass(false, DateTimeOffset.UtcNow.AddMinutes(5)).Active(DateTimeOffset.UtcNow).ShouldBeFalse();
    }

    /// <summary>One captured log line, with the category it was logged under.</summary>
    private sealed record LoggedEntry(string Category, string Message);

    /// <summary>Captures every formatted log line across every category, for asserting audit output.</summary>
    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly List<LoggedEntry> _entries = [];
        private readonly object _lock = new();

        public IReadOnlyList<LoggedEntry> Entries
        {
            get
            {
                lock (_lock)
                    return [.. _entries];
            }
        }

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this, categoryName);

        public void Dispose() { }

        private void Add(string category, string message)
        {
            lock (_lock)
                _entries.Add(new LoggedEntry(category, message));
        }

        private sealed class CapturingLogger(CapturingLoggerProvider owner, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                owner.Add(category, formatter(state, exception));
        }
    }
}
