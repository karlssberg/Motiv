# Spec 1 — Trust & Control Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Every change to live behaviour passes through **authenticate → authorize (namespace grant) → govern (approval gate) → publish**, and the system can always recover from a gate it can no longer satisfy (layered recovery, bottoming out at infrastructure access).

**Architecture:** Four layered phases, each independently shippable, mirroring the spec's build sequence: (1) `.RequireAuthorization()` on the whole endpoint group + a fail-closed dev identity; (2) `IGrantSource` + a prefix-covering grant evaluator with three implementations; (3) the `ChangeRequest` envelope + a `may-publish` Motiv Policy gate with a built-in `change.*` spec catalogue; (4) administer-gated gate config, the lockout pre-check, break-glass, and bootstrap elevation. A fifth phase delivers the Keycloak/OIDC e2e evidence.

**Tech Stack:** C# / .NET (`Motiv.Serialization` multi-TFM incl. netstandard2.0; `Motiv.Serialization.AspNetCore` net10.0), xUnit + Shouldly, ASP.NET Core minimal APIs + `ClaimsPrincipal`, Playwright e2e, Keycloak via docker compose profile.

**Source spec:** `.scratch/enterprise-grade-product/specs/1-trust-and-control.md` on branch `wayfinder/enterprise-grade-product` (tickets 03, 05, 12, 13, 14 in `.scratch/enterprise-grade-product/issues/`). ADR: `docs/adr/0001-approval-gate-is-a-motiv-rule.md`. Glossary: `CONTEXT.md`.

## Global Constraints

- **Every `dotnet` command** must be prefixed with `export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH"` (net8/net9 testhosts abort otherwise). Use `-f net10.0` for filtered runs; net472 never runs on this Mac.
- There is **no `timeout` command on macOS** — never wrap test runs in it (it silently runs nothing).
- e2e is `pnpm -C ui/apps/demo e2e` (vite build + playwright; never bare `playwright test`). In a worktree, set `MOTIV_E2E_PORT` to a free port — another checkout may already hold :5100 and you'd test its build.
- **TDD strictly**: failing test → confirm failure → minimal code → confirm pass → commit. Run the **full solution suite** (including `src/examples/*Tests`) before calling any phase complete.
- Test naming: `public class {Subject}Tests`, `[Fact] public async Task Should_snake_case_phrase()`, `// Arrange` / `// Act` / `// Assert` comments, Shouldly assertions.
- `Motiv.Serialization` targets netstandard2.0 among others — no ranges/`Index`; governance types must not reference ASP.NET types (`ClaimsPrincipal` stays in `.AspNetCore`, which has `InternalsVisibleTo` access to `Motiv.Serialization` internals).
- Secure-by-default is a breaking change with **zero adopters** (`Motiv.Serialization.AspNetCore` was never published — ticket 06). No compatibility shims.
- Invariants that must hold at the end (spec §4): no endpoint evaluates a named live rule; the gate never governs itself; the app-owned grant store cannot remove the last `administer`; dev identity, dev grants, and break-glass are each fail-closed and loud; every break-glass publish is audit-stamped.
- Per project convention, after each phase's tests pass, spawn a `code-simplifier` agent over the changed files and apply its findings before moving on.

## Design Decisions Locked by This Plan

The spec fixes the architecture; these are the mechanism-level decisions it left open, decided here so no task re-derives them:

1. **Parameterised gate specs** use a new optional `"args"` object on spec nodes (`{"spec": "change.approver-count-at-least", "args": {"n": 2}}`) plus `SpecRegistry.RegisterParameterised<TModel>`. Args are validated/coerced by the existing `RuleParameterResolver` — which is exactly why ticket 13 verified the resolver covers scalars.
2. **Envelope semantics** for the built-ins: `in-namespace`, `target-is-proposition`, `is-rollback`, `is-creation`, `is-deletion`, `touches-async-spec` are satisfied when **any** proposed change matches (a mixed envelope touching a guarded namespace must trip its ceremony); `is-metadata-only` requires **all** changes to be metadata-only (one logic change makes the envelope a logic change).
3. **Dev grant source** reconciles tickets 12 and 14: `IsAdministrator` returns true (the dev principal *is* the first admin — ticket 14) while `SupportsAdministration` is false (the source is immutable, so no grant-admin surface exists and a leaked dev superuser cannot persist grants — ticket 12).
4. **`Approval` snapshots the approver's roles** at approval time — `change.approver-has-role(role)` must evaluate later without re-resolving principals.
5. App-side pieces land in `src/examples/Motiv.RulesEngine.Sample` — the app that *becomes* `Motiv.Studio`. The rename/promotion is spec 4's plan, not this one.
6. **Direct writes cannot bypass the gate**: when governance is registered, `PUT/DELETE /rules/{name}` and proposition writes internally build a single-change `ChangeRequest` and publish it through the gate. The permissive default keeps today's hot-swap behaviour byte-for-byte.
7. The gate binds against a **dedicated `GateSpecs` registry** (only `change.*` specs, all synchronous). The bind-time "gate must be synchronous" check is kept anyway as a cheap guard for future registry extension.
8. The sample's `/api/checkout` gets `.RequireAuthorization()` too — the whole surface is uniform (production evaluation stays in-process; checkout demonstrates that).
9. `ChangeRequest` storage is **in-memory** in this spec; spec 2 (durability & data) owns persistence. `IGateStore` is the one persistence seam introduced here (the active gate must survive restart or an admin's ceremony evaporates).
10. e2e of the authenticated path drives the **HTTP API with real Keycloak tokens** (password grant). A browser login UI for the SPA is surface-quality work (spec 4) and is not required by spec 1 §3.

---

## File Structure

| File | Responsibility |
|---|---|
| `src/Motiv.Serialization.AspNetCore/MotivRulesEndpointOptions.cs` | Per-mount options; the greppable `AllowAnonymous()` escape |
| `src/Motiv.Serialization.AspNetCore/MotivRulesEndpoints.cs` | Gains `RequireAuthorization()`, grant checks, governance/gate mounting |
| `src/Motiv.Serialization.AspNetCore/Grants.cs` | `GrantVerb`, `NamespaceGrant`, `IGrantSource`, `GrantEvaluator` |
| `src/Motiv.Serialization.AspNetCore/GrantGate.cs` | Internal per-request refusal helpers + `PrincipalIdentity` |
| `src/Motiv.Serialization.AspNetCore/MotivGovernanceEndpoints.cs` | `/change-requests` + `/gate` endpoints |
| `src/Motiv.Serialization.AspNetCore/GovernanceContracts.cs` | Request/response records incl. `GateRefusalResponse` |
| `src/Motiv.Serialization/NamespacePrefix.cs` | Dot-boundary prefix matching (shared by grants and `change.in-namespace`) |
| `src/Motiv.Serialization/Governance/ChangeRequest.cs` | `ChangeRequest`, `ProposedChange`, `ChangeTarget`, `Approval`, statuses, classification |
| `src/Motiv.Serialization/Governance/RuleDocumentComparer.cs` | Structural diff for `is-metadata-only` |
| `src/Motiv.Serialization/Governance/ChangeClassifier.cs` | Derives classification from the diff; rollback is stored intent |
| `src/Motiv.Serialization/Governance/GateSpecs.cs` | The built-in `change.*` spec catalogue |
| `src/Motiv.Serialization/Governance/ApprovalGate.cs` | Binds/evaluates the `may-publish` Policy; `IGateStore`; sync check; pre-check |
| `src/Motiv.Serialization/Governance/SyntheticChangeRequests.cs` | Maximally-approvable builder for the lockout pre-check |
| `src/Motiv.Serialization/Governance/ChangeRequestSet.cs` | Workflow lifecycle + atomic publish (`ChangeRequestPublisher`) |
| `src/Motiv.Serialization/SpecRegistry.cs` + `RuleNode/Parser/Binder` | `RegisterParameterised`, spec-node `args` |
| `src/examples/Motiv.RulesEngine.Sample/DevIdentity.cs` | Fail-closed dev auth handler + continuous warning service |
| `src/examples/Motiv.RulesEngine.Sample/GrantSources.cs` | `DevGrantSource`, `JsonFileGrantSource`, `ClaimsGrantSource`, `BootstrapGrantSource` |
| `src/examples/Motiv.RulesEngine.Sample/Program.cs` | Startup guards, OIDC wiring, admin endpoints, break-glass |
| `docker-compose.yml`, `keycloak/motiv-realm.json` | `--profile auth` Keycloak + OIDC demo service |
| `ui/apps/demo/src/panes/AdminPage.tsx` | Grant admin surface, rendered only for a mutable source |
| `ui/apps/demo/e2e/auth.spec.ts` | e2e of the authenticated path against Keycloak |

---

# Phase 1 — The Floor: secure by default + fail-closed dev identity (tickets 03/08)

### Task 1: Secure-by-default `MapMotivRules` with a greppable escape

**Files:**
- Create: `src/Motiv.Serialization.AspNetCore/MotivRulesEndpointOptions.cs`
- Modify: `src/Motiv.Serialization.AspNetCore/MotivRulesEndpoints.cs` (both overloads)
- Modify: `src/Motiv.Serialization.AspNetCore.Tests/TestApp.cs`
- Create: `src/Motiv.Serialization.AspNetCore.Tests/TestAuthHandler.cs`
- Test: `src/Motiv.Serialization.AspNetCore.Tests/AuthorizationTests.cs`

**Interfaces:**
- Consumes: existing `MapMotivRules` overloads (see file), `TestApp.StartAsync`.
- Produces: `MotivRulesEndpointOptions { MotivRulesEndpointOptions AllowAnonymous(); internal bool Anonymous }`; both `MapMotivRules` overloads gain a trailing `Action<MotivRulesEndpointOptions>? configureEndpoints = null` parameter; `TestApp.StartAsync(SpecRegistry registry, MotivRulesOptions options, RuleSet? rules = null, Action<MotivRulesEndpointOptions>? endpointOptions = null, Action<IServiceCollection>? services = null)`; `TestAuthHandler` with `Scheme = "Test"`, headers `X-Test-Anonymous`, `X-Test-User` (default subject `test-user`), `X-Test-Roles` (comma-separated). Every later task's integration tests use these.

- [ ] **Step 1: Write the failing tests**

```csharp
public class AuthorizationTests
{
    [Fact]
    public async Task Should_reject_unauthenticated_requests_with_401()
    {
        // Arrange
        await using var app = await TestApp.StartAsync(Registry(), Options());
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.AnonymousHeader, "true");

        // Act
        var response = await client.GetAsync("/api/rules/catalog");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_serve_authenticated_requests()
    {
        // Arrange
        await using var app = await TestApp.StartAsync(Registry(), Options());

        // Act
        var response = await app.GetTestClient().GetAsync("/api/rules/catalog");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_serve_anonymous_requests_when_the_mount_site_opts_out()
    {
        // Arrange — the explicit, greppable escape at the call site
        await using var app = await TestApp.StartAsync(
            Registry(), Options(), endpointOptions: o => o.AllowAnonymous());
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.AnonymousHeader, "true");

        // Act
        var response = await client.GetAsync("/api/rules/catalog");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static SpecRegistry Registry() => new SpecRegistry().Register(
        "customer.is-active",
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create());

    private static MotivRulesOptions Options() => new MotivRulesOptions().AddModel<Customer>("customer");

    private sealed record Customer(bool IsActive);
}
```

`TestAuthHandler` (authenticates by default so the existing ~72 endpoint tests keep passing; `X-Test-Anonymous` suppresses it):

```csharp
internal sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string Scheme = "Test";
    public const string AnonymousHeader = "X-Test-Anonymous";
    public const string SubjectHeader = "X-Test-User";
    public const string RolesHeader = "X-Test-Roles";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.ContainsKey(AnonymousHeader))
            return Task.FromResult(AuthenticateResult.NoResult());

        var subject = Request.Headers.TryGetValue(SubjectHeader, out var user)
            ? user.ToString()
            : "test-user";
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, subject) };
        if (Request.Headers.TryGetValue(RolesHeader, out var roles))
            claims.AddRange(roles.ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(role => new Claim(ClaimTypes.Role, role.Trim())));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme)));
    }
}
```

Widen `TestApp` (keep the 4-arg call shape used by the 72 existing tests source-compatible via the new optional parameters):

```csharp
internal static class TestApp
{
    public static async Task<WebApplication> StartAsync(
        SpecRegistry registry,
        MotivRulesOptions options,
        RuleSet? rules = null,
        Action<MotivRulesEndpointOptions>? endpointOptions = null,
        Action<IServiceCollection>? services = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services
            .AddAuthentication(TestAuthHandler.Scheme)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Scheme, null);
        builder.Services.AddAuthorization();
        services?.Invoke(builder.Services);
        var app = builder.Build();
        app.MapMotivRules("/api/rules", registry, options, rules, endpointOptions);
        await app.StartAsync();
        return app;
    }
}
```

- [ ] **Step 2: Run the new tests to verify they fail**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.AspNetCore.Tests -f net10.0 --filter AuthorizationTests
```
Expected: FAIL (401 test gets 200 — no authorization is applied yet; compile errors until the new parameters exist).

- [ ] **Step 3: Implement**

New `MotivRulesEndpointOptions.cs`:

```csharp
namespace Motiv.Serialization.AspNetCore;

/// <summary>
/// Per-mount endpoint options. The endpoints are secure by default; opening them requires the
/// explicit, greppable <see cref="AllowAnonymous"/> call at the mount site, so an open deployment
/// is auditable in review rather than the silent default.
/// </summary>
public sealed class MotivRulesEndpointOptions
{
    internal bool Anonymous { get; private set; }

    /// <summary>Opens every mapped endpoint to unauthenticated callers.</summary>
    public MotivRulesEndpointOptions AllowAnonymous()
    {
        Anonymous = true;
        return this;
    }
}
```

In `MotivRulesEndpoints.cs`, add `Action<MotivRulesEndpointOptions>? configureEndpoints = null` as the last parameter of **both** overloads (the DI overload passes it through), and immediately after `var group = endpoints.MapGroup(basePath);`:

```csharp
var endpointOptions = new MotivRulesEndpointOptions();
configureEndpoints?.Invoke(endpointOptions);
if (endpointOptions.Anonymous)
    group.AllowAnonymous();
else
    group.RequireAuthorization();
```

(`WebApplication` auto-inserts the authentication/authorization middleware when the services are registered — no `Use*` calls needed in `TestApp`.)

- [ ] **Step 4: Run the whole AspNetCore test project to verify everything passes**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/Motiv.Serialization.AspNetCore.Tests -f net10.0
```
Expected: PASS, including all pre-existing endpoint tests (they authenticate via the default `TestAuthHandler` identity).

- [ ] **Step 5: Commit**

```bash
git add src/Motiv.Serialization.AspNetCore src/Motiv.Serialization.AspNetCore.Tests
git commit -m "feat: secure MapMotivRules by default with explicit AllowAnonymous escape"
```

### Task 2: Fail-closed dev identity in the sample host

**Files:**
- Create: `src/examples/Motiv.RulesEngine.Sample/DevIdentity.cs`
- Create: `src/examples/Motiv.RulesEngine.Sample/appsettings.Development.json`
- Modify: `src/examples/Motiv.RulesEngine.Sample/Program.cs`
- Modify: `docker-compose.yml`, `Makefile`, `run-demo.sh`, `ui/apps/demo/playwright.config.ts`
- Test: `src/examples/Motiv.RulesEngine.Sample.Tests/DevIdentityTests.cs`

**Interfaces:**
- Consumes: Task 1's secured group.
- Produces: config keys `Motiv:DevIdentity:Enabled`, `Motiv:DevIdentity:AllowInProduction`, `Motiv:Oidc:Authority`, `Motiv:Oidc:Audience`; `DevIdentityHandler` (scheme `"DevIdentity"`, subject claim `ClaimTypes.NameIdentifier = "dev"`, role claim `motiv-dev`); `DevIdentityWarningService`. Later tasks branch on the same `Motiv:DevIdentity:Enabled` flag for the dev grant source.

- [ ] **Step 1: Write the failing tests**

```csharp
public class DevIdentityTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public void Should_refuse_to_start_when_no_identity_is_configured()
    {
        // Arrange — Production environment, no dev identity, no OIDC
        var bare = factory.WithWebHostBuilder(builder => builder
            .UseEnvironment("Production")
            .UseSetting("Motiv:DevIdentity:Enabled", "false"));

        // Act
        var startup = () => bare.CreateClient();

        // Assert
        startup.ShouldThrow<Exception>().Message.ShouldContain("secure by default");
    }

    [Fact]
    public void Should_refuse_the_dev_identity_in_production_without_explicit_acknowledgement()
    {
        // Arrange
        var production = factory.WithWebHostBuilder(builder => builder
            .UseEnvironment("Production")
            .UseSetting("Motiv:DevIdentity:Enabled", "true"));

        // Act
        var startup = () => production.CreateClient();

        // Assert
        startup.ShouldThrow<Exception>().Message.ShouldContain("AllowInProduction");
    }

    [Fact]
    public async Task Should_authenticate_every_request_as_the_dev_principal_when_enabled()
    {
        // Arrange — default factory environment is Development; appsettings.Development.json enables it
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/rules/catalog");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
```

- [ ] **Step 2: Run to verify failure**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/examples/Motiv.RulesEngine.Sample.Tests -f net10.0 --filter DevIdentityTests
```
Expected: FAIL (no guards exist; catalog currently 401s once Task 1 lands with no auth configured in the sample).

- [ ] **Step 3: Implement**

`DevIdentity.cs`:

```csharp
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Motiv.RulesEngine.Sample;

/// <summary>
/// The fail-closed dev identity: authenticates every request as a fixed dev principal so
/// `docker compose up` coexists with secure-by-default endpoints. Never active by omission.
/// </summary>
internal sealed class DevIdentityHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string Scheme = "DevIdentity";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "dev"),
                new Claim(ClaimTypes.Name, "Dev User"),
                new Claim(ClaimTypes.Role, "motiv-dev")
            ],
            Scheme);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
    }
}

/// <summary>Warns continuously while the dev identity is active — loud, never silent.</summary>
internal sealed class DevIdentityWarningService(ILogger<DevIdentityWarningService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Motiv dev identity is ACTIVE: every request is authenticated as the dev " +
                "superuser. Never enable this in a production deployment.");
            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }
    }
}
```

In `Program.cs`, after `var builder = WebApplication.CreateBuilder(args);` (and the `$PORT` block), add:

```csharp
// Fail-closed identity wiring: the endpoints are secure by default, so the host must be told
// who supplies the principal — OIDC for real deployments, the dev identity for local evaluation.
// Anything enable-able by omission is a default-credentials vulnerability, so no identity means
// no startup.
var devIdentityEnabled = builder.Configuration.GetValue<bool>("Motiv:DevIdentity:Enabled");
var oidcAuthority = builder.Configuration["Motiv:Oidc:Authority"];

if (devIdentityEnabled
    && builder.Environment.IsProduction()
    && !builder.Configuration.GetValue<bool>("Motiv:DevIdentity:AllowInProduction"))
{
    throw new InvalidOperationException(
        "The Motiv dev identity is enabled in a Production environment. Set " +
        "Motiv:DevIdentity:AllowInProduction=true only if you accept every request being " +
        "authenticated as the dev superuser.");
}

if (!devIdentityEnabled && string.IsNullOrWhiteSpace(oidcAuthority))
{
    throw new InvalidOperationException(
        "No identity is configured and the Motiv endpoints are secure by default. Configure " +
        "OIDC (Motiv:Oidc:Authority, Motiv:Oidc:Audience) or explicitly enable the dev " +
        "identity (Motiv:DevIdentity:Enabled=true).");
}

if (devIdentityEnabled)
{
    builder.Services
        .AddAuthentication(DevIdentityHandler.Scheme)
        .AddScheme<AuthenticationSchemeOptions, DevIdentityHandler>(DevIdentityHandler.Scheme, null);
    builder.Services.AddHostedService<DevIdentityWarningService>();
}
else
{
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(o =>
        {
            o.Authority = oidcAuthority;
            o.Audience = builder.Configuration["Motiv:Oidc:Audience"];
        });
}
builder.Services.AddAuthorization();
```

(Add the `Microsoft.AspNetCore.Authentication.JwtBearer` package reference to the sample csproj. The full OIDC path is exercised in Phase 5; wiring it now keeps this the only identity block ever written.) Append `.RequireAuthorization()` to the `/api/checkout` mapping — the surface is uniform.

`appsettings.Development.json` (explicit, checked-in dev enablement — `dotnet run` and `WebApplicationFactory` both run Development):

```json
{
  "Motiv": {
    "DevIdentity": { "Enabled": true }
  }
}
```

`docker-compose.yml` demo service (the container runs the release image in Production — both flags, loud and explicit):

```yaml
    environment:
      Motiv__DevIdentity__Enabled: "true"
      Motiv__DevIdentity__AllowInProduction: "true"  # demo container only — never copy to a real deployment
```

`ui/apps/demo/playwright.config.ts`: `dotnet run` outside launchSettings defaults to Production, so add to `webServer`:

```ts
env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development' },
```

Add `ASPNETCORE_ENVIRONMENT=Development` to the `dotnet run` invocations in `Makefile` and `run-demo.sh` the same way.

- [ ] **Step 4: Run the sample test project and the e2e smoke spec**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test src/examples/Motiv.RulesEngine.Sample.Tests -f net10.0
```
Expected: PASS. Then `MOTIV_E2E_PORT=5109 pnpm -C ui/apps/demo e2e` — expected: existing specs pass (dev identity authenticates the browser silently).

- [ ] **Step 5: Commit**

```bash
git add src/examples/Motiv.RulesEngine.Sample src/examples/Motiv.RulesEngine.Sample.Tests docker-compose.yml Makefile run-demo.sh ui/apps/demo/playwright.config.ts
git commit -m "feat: fail-closed dev identity so secure-by-default and docker compose up coexist"
```

---

# Phase 2 — Grants: `IGrantSource` + prefix-covering evaluator (ticket 12)

### Task 3: `NamespacePrefix.Covers`

**Files:**
- Create: `src/Motiv.Serialization/NamespacePrefix.cs`
- Test: `src/Motiv.Serialization.Tests/NamespacePrefixTests.cs`

**Interfaces:**
- Produces: `public static class NamespacePrefix { public static bool Covers(string prefix, string name) }` — empty prefix covers everything; otherwise whole-segment prefix match (`pricing` covers `pricing` and `pricing.eu.vat`, never `pricingx`). Used by `GrantEvaluator` (Task 4) and `change.in-namespace` (Task 15).

- [ ] **Step 1: Write the failing tests**

```csharp
public class NamespacePrefixTests
{
    [Theory]
    [InlineData("", "anything.at.all", true)]
    [InlineData("pricing", "pricing", true)]
    [InlineData("pricing", "pricing.eu.vat", true)]
    [InlineData("pricing", "pricingx", false)]
    [InlineData("pricing.eu", "pricing", false)]
    [InlineData("Pricing", "pricing.eu", false)]
    public void Should_cover_only_whole_segment_prefixes(string prefix, string name, bool expected)
    {
        // Act & Assert
        NamespacePrefix.Covers(prefix, name).ShouldBe(expected);
    }
}
```

- [ ] **Step 2: Run to verify failure** — `dotnet test src/Motiv.Serialization.Tests -f net10.0 --filter NamespacePrefixTests`. Expected: compile FAIL (type missing).

- [ ] **Step 3: Implement**

```csharp
namespace Motiv.Serialization;

/// <summary>
/// Dot-boundary namespace-prefix matching, shared by the grant evaluator and the
/// <c>change.in-namespace</c> gate spec so "covers" means one thing everywhere.
/// </summary>
public static class NamespacePrefix
{
    /// <summary>
    /// Whether <paramref name="prefix"/> covers <paramref name="name"/>: the empty prefix covers
    /// everything; otherwise the prefix must equal the name or end on a whole dotted segment of it.
    /// </summary>
    public static bool Covers(string prefix, string name)
    {
        if (prefix.Length == 0)
            return true;
        if (!name.StartsWith(prefix, StringComparison.Ordinal))
            return false;
        return name.Length == prefix.Length || name[prefix.Length] == '.';
    }
}
```

- [ ] **Step 4: Run to verify pass.**

- [ ] **Step 5: Commit** — `git commit -m "feat: dot-boundary namespace prefix matching"` (with both files staged).

### Task 4: Grant model + evaluator

**Files:**
- Create: `src/Motiv.Serialization.AspNetCore/Grants.cs`
- Test: `src/Motiv.Serialization.AspNetCore.Tests/GrantEvaluatorTests.cs`

**Interfaces:**
- Consumes: `NamespacePrefix.Covers` (Task 3).
- Produces (verbatim contracts every later task builds on):

```csharp
/// <summary>The verb ladder: publish ⊃ author ⊃ read. Enum order is load-bearing.</summary>
public enum GrantVerb { Read, Author, Publish }

public sealed record NamespaceGrant(string Prefix, GrantVerb Verb);

/// <summary>Yields a principal's namespace grants. Swappable: app store, IdP claims, or dev.</summary>
public interface IGrantSource
{
    /// <summary>Whether grants can be administered in-app (mutable source). Gates the admin surface.</summary>
    bool SupportsAdministration { get; }

    /// <summary>The role universe for the lockout pre-check; empty when unknowable.</summary>
    IReadOnlyCollection<string> KnownRoles { get; }

    IReadOnlyList<NamespaceGrant> GrantsFor(ClaimsPrincipal principal);

    /// <summary>Whether the principal holds administer — gate config and grant administration.</summary>
    bool IsAdministrator(ClaimsPrincipal principal);
}

public static class GrantEvaluator
{
    public static bool IsGranted(IReadOnlyList<NamespaceGrant> grants, GrantVerb verb, string name);
    public static bool CanAuthorAnywhere(IReadOnlyList<NamespaceGrant> grants);
}
```

- [ ] **Step 1: Write the failing tests**

```csharp
public class GrantEvaluatorTests
{
    [Theory]
    [InlineData(GrantVerb.Publish, GrantVerb.Read, true)]    // ladder: publish covers read
    [InlineData(GrantVerb.Publish, GrantVerb.Author, true)]
    [InlineData(GrantVerb.Author, GrantVerb.Publish, false)] // never upward
    [InlineData(GrantVerb.Read, GrantVerb.Author, false)]
    public void Should_apply_the_verb_ladder(GrantVerb held, GrantVerb required, bool expected)
    {
        // Arrange
        var grants = new[] { new NamespaceGrant("pricing", held) };

        // Act & Assert
        GrantEvaluator.IsGranted(grants, required, "pricing.eu.vat").ShouldBe(expected);
    }

    [Fact]
    public void Should_deny_outside_the_granted_prefix()
    {
        // Arrange
        var grants = new[] { new NamespaceGrant("pricing", GrantVerb.Publish) };

        // Act & Assert
        GrantEvaluator.IsGranted(grants, GrantVerb.Author, "fraud.velocity").ShouldBeFalse();
    }

    [Fact]
    public void Should_report_author_anywhere_from_any_author_or_publish_grant()
    {
        // Act & Assert
        GrantEvaluator.CanAuthorAnywhere([new NamespaceGrant("pricing", GrantVerb.Author)]).ShouldBeTrue();
        GrantEvaluator.CanAuthorAnywhere([new NamespaceGrant("pricing", GrantVerb.Read)]).ShouldBeFalse();
        GrantEvaluator.CanAuthorAnywhere([]).ShouldBeFalse();
    }
}
```

- [ ] **Step 2: Run to verify failure** (compile error — types missing).

- [ ] **Step 3: Implement** `Grants.cs` with the contracts above; the evaluator bodies:

```csharp
public static bool IsGranted(IReadOnlyList<NamespaceGrant> grants, GrantVerb verb, string name)
{
    foreach (var grant in grants)
        if (grant.Verb >= verb && NamespacePrefix.Covers(grant.Prefix, name))
            return true;
    return false;
}

public static bool CanAuthorAnywhere(IReadOnlyList<NamespaceGrant> grants)
{
    foreach (var grant in grants)
        if (grant.Verb >= GrantVerb.Author)
            return true;
    return false;
}
```

- [ ] **Step 4: Run to verify pass.**
- [ ] **Step 5: Commit** — `git commit -m "feat: namespace grant model with read-author-publish ladder"`.

### Task 5: Enforce grants at the endpoints — unfiltered read, filtered write

**Files:**
- Create: `src/Motiv.Serialization.AspNetCore/GrantGate.cs`
- Modify: `src/Motiv.Serialization.AspNetCore/MotivRulesEndpoints.cs`, `MotivPropositionEndpoints.cs`
- Test: `src/Motiv.Serialization.AspNetCore.Tests/GrantEnforcementTests.cs`

**Interfaces:**
- Consumes: Task 4 contracts; Task 1 `TestApp` `services` hook.
- Produces:

```csharp
internal static class PrincipalIdentity
{
    public static string Subject(ClaimsPrincipal principal);           // NameIdentifier ?? "sub" ?? Name ?? "unknown"
    public static IReadOnlyList<string> Roles(ClaimsPrincipal principal); // ClaimTypes.Role + "roles", distinct
}

internal static class GrantGate
{
    // Each returns null to proceed, or a 403 ErrorResponse IResult naming the missing verb/name.
    // All return null when no IGrantSource is registered — grants are opt-in; without a source the
    // surface is authenticated-only (Phase 1 behaviour).
    public static IResult? Refuse(HttpContext http, GrantVerb verb, string name, JsonSerializerOptions json);
    public static IResult? RefuseUnlessAuthorAnywhere(HttpContext http, JsonSerializerOptions json);
    public static IResult? RefuseUnlessAdministrator(HttpContext http, JsonSerializerOptions json);
}
```

Enforcement map (the write grant is a function of the artefact's **own name**, never of what it references): `PUT/DELETE /rules/{name}` → `Publish` on `name`; `POST /propositions` → `Publish` on the request body's name; `PUT/DELETE /propositions/{name}` → `Publish` on `name`; `POST /validate` and `POST /evaluate` → `RefuseUnlessAuthorAnywhere` (the sandbox gates on "holds any author grant", not per-namespace); every `GET` → authenticated only (unfiltered read — the evaluator stays off the `/catalog` path).

- [ ] **Step 1: Write the failing tests**

```csharp
public class GrantEnforcementTests
{
    [Fact]
    public async Task Should_refuse_publishing_a_rule_outside_the_granted_prefix_with_403()
    {
        // Arrange
        await using var app = await StartWithGrants(new NamespaceGrant("pricing", GrantVerb.Publish));

        // Act — the enrolled test rule is named "checkout.can-checkout"
        var response = await app.GetTestClient().PutAsJsonAsync(
            "/api/rules/rules/checkout.can-checkout",
            new { document = new { spec = "customer.is-active" }, baseVersion = 1 });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).ShouldContain("publish");
    }

    [Fact]
    public async Task Should_allow_publishing_inside_the_granted_prefix()
    {
        // Arrange
        await using var app = await StartWithGrants(new NamespaceGrant("checkout", GrantVerb.Publish));

        // Act
        var response = await app.GetTestClient().PutAsJsonAsync(
            "/api/rules/rules/checkout.can-checkout",
            new { document = new { spec = "customer.is-active" }, baseVersion = 1 });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_refuse_the_evaluate_sandbox_to_read_only_principals()
    {
        // Arrange
        await using var app = await StartWithGrants(new NamespaceGrant("", GrantVerb.Read));

        // Act
        var response = await app.GetTestClient().PostAsJsonAsync("/api/rules/evaluate",
            new { modelType = "customer", document = new { spec = "customer.is-active" }, model = new { isActive = true } });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Should_leave_reads_unfiltered()
    {
        // Arrange — no grants at all, still authenticated
        await using var app = await StartWithGrants();

        // Act
        var response = await app.GetTestClient().GetAsync("/api/rules/catalog");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
```

with the shared fixture pieces in the same file:

```csharp
private static Task<WebApplication> StartWithGrants(params NamespaceGrant[] grants)
{
    var registry = new SpecRegistry().Register(
        "customer.is-active",
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create());
    var options = new MotivRulesOptions().AddModel<Customer>("customer");
    var rules = new RuleSet(registry).Add(new CanCheckoutRule());
    return TestApp.StartAsync(registry, options, rules,
        services: s => s.AddSingleton<IGrantSource>(new FakeGrantSource(grants)));
}

private sealed record Customer(bool IsActive);

private sealed class CanCheckoutRule() : Rule<Customer, string>(
    "checkout.can-checkout",
    Spec.Build((Customer c) => c.IsActive).WhenTrue("ok").WhenFalse("no").Create());

internal sealed class FakeGrantSource(IReadOnlyList<NamespaceGrant> grants) : IGrantSource
{
    public bool SupportsAdministration => false;
    public IReadOnlyCollection<string> KnownRoles => [];
    public IReadOnlyList<NamespaceGrant> GrantsFor(ClaimsPrincipal principal) => grants;
    public bool IsAdministrator(ClaimsPrincipal principal) => false;
}
```

(Match `Rule<,>`'s actual constructor from `src/Motiv.Serialization/Rules/Rule.cs` — mirror how `AppRules.cs` in the sample declares rules. Reuse `FakeGrantSource` from here in later tasks' tests.)

- [ ] **Step 2: Run to verify failure** (403 tests get 200/404 — no enforcement).

- [ ] **Step 3: Implement.** `GrantGate.cs`:

```csharp
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Motiv.Serialization.AspNetCore;

internal static class PrincipalIdentity
{
    public static string Subject(ClaimsPrincipal principal) =>
        principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? principal.FindFirst("sub")?.Value
        ?? principal.Identity?.Name
        ?? "unknown";

    public static IReadOnlyList<string> Roles(ClaimsPrincipal principal) =>
        [.. principal.FindAll(ClaimTypes.Role).Concat(principal.FindAll("roles"))
            .Select(claim => claim.Value).Distinct()];
}

internal static class GrantGate
{
    public static IResult? Refuse(HttpContext http, GrantVerb verb, string name, JsonSerializerOptions json)
    {
        if (http.RequestServices.GetService<IGrantSource>() is not { } source)
            return null;
        return GrantEvaluator.IsGranted(source.GrantsFor(http.User), verb, name)
            ? null
            : Results.Json(new ErrorResponse(
                $"Requires the '{verb.ToString().ToLowerInvariant()}' grant on '{name}'."),
                json, statusCode: 403);
    }

    public static IResult? RefuseUnlessAuthorAnywhere(HttpContext http, JsonSerializerOptions json)
    {
        if (http.RequestServices.GetService<IGrantSource>() is not { } source)
            return null;
        return GrantEvaluator.CanAuthorAnywhere(source.GrantsFor(http.User))
            ? null
            : Results.Json(new ErrorResponse("Requires an 'author' grant on at least one namespace."),
                json, statusCode: 403);
    }

    public static IResult? RefuseUnlessAdministrator(HttpContext http, JsonSerializerOptions json)
    {
        if (http.RequestServices.GetService<IGrantSource>() is not { } source)
            return null;
        return source.IsAdministrator(http.User)
            ? null
            : Results.Json(new ErrorResponse("Requires 'administer'."), json, statusCode: 403);
    }
}
```

Then thread the checks through the handlers: each write handler gains an `HttpContext http` parameter and opens with, e.g. for `PUT /rules/{name}`:

```csharp
group.MapPut("/rules/{name}", (string name, RulePutRequest request, HttpContext http) =>
{
    if (GrantGate.Refuse(http, GrantVerb.Publish, name, json) is { } refusal)
        return refusal;
    // ... existing body unchanged
```

Same pattern for `DELETE /rules/{name}`, the proposition create/update/delete handlers in `MotivPropositionEndpoints.cs` (create reads the name from its request contract), and `RefuseUnlessAuthorAnywhere` at the top of `/validate` and `/evaluate`.

- [ ] **Step 4: Run the whole AspNetCore test project** — new tests pass, pre-existing tests still pass (no `IGrantSource` registered → `GrantGate` is a no-op for them).
- [ ] **Step 5: Commit** — `git commit -m "feat: enforce namespace grants on write endpoints; reads stay unfiltered"`.

### Task 6: Dev grant source in the sample

**Files:**
- Create: `src/examples/Motiv.RulesEngine.Sample/GrantSources.cs` (starts with `DevGrantSource`)
- Modify: `src/examples/Motiv.RulesEngine.Sample/Program.cs`
- Test: `src/examples/Motiv.RulesEngine.Sample.Tests/GrantSourceTests.cs`

**Interfaces:**
- Consumes: `IGrantSource` (Task 4), `Motiv:DevIdentity:Enabled` (Task 2).
- Produces: `DevGrantSource` — `SupportsAdministration => false`, `KnownRoles => ["motiv-dev"]`, `GrantsFor => [new NamespaceGrant("", GrantVerb.Publish)]`, `IsAdministrator => true` (design decision 3). Registered **only** while the dev identity switch is on — a source-backed grant that evaporates with the switch, never a persisted seed.

- [ ] **Step 1: Write the failing test**

```csharp
public class GrantSourceTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Should_let_the_dev_principal_publish_anywhere_while_the_switch_is_on()
    {
        // Arrange — Development enables the dev identity, and with it the dev grant source
        var client = factory.CreateClient();
        var current = await client.GetFromJsonAsync<JsonElement>("/api/rules/rules/loyalty-discount");

        // Act
        var response = await client.PutAsJsonAsync("/api/rules/rules/loyalty-discount", new
        {
            document = current.GetProperty("document"),
            baseVersion = current.GetProperty("version").GetInt32()
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
```

(Use the sample's real rule names from `AppRules.cs` — check them when writing the test.)

- [ ] **Step 2: Run to verify failure** (compiles but 200 already? No — it passes trivially until a grant source exists; the *meaningful* red is the next test: temporarily assert that an `IGrantSource` is registered. Simplest honest red: write the test asserting `factory.Services.GetService<IGrantSource>().ShouldNotBeNull()` first, watch it fail, then keep both assertions.)

- [ ] **Step 3: Implement**

```csharp
/// <summary>
/// The authorization-side twin of the dev identity: grants the single dev principal everything,
/// zero-config, and evaporates the moment the switch is off — never persisted. Immutable, so it
/// has no administration surface (a leaked dev superuser cannot persist new grants), but the dev
/// principal IS the first administrator (ticket 14): gate configuration works out of the box.
/// </summary>
internal sealed class DevGrantSource : IGrantSource
{
    public bool SupportsAdministration => false;
    public IReadOnlyCollection<string> KnownRoles => ["motiv-dev"];
    public IReadOnlyList<NamespaceGrant> GrantsFor(ClaimsPrincipal principal) =>
        [new NamespaceGrant("", GrantVerb.Publish)];
    public bool IsAdministrator(ClaimsPrincipal principal) => true;
}
```

In `Program.cs`, inside the `if (devIdentityEnabled)` branch: `builder.Services.AddSingleton<IGrantSource, DevGrantSource>();`

- [ ] **Step 4: Run sample tests to verify pass.**
- [ ] **Step 5: Commit** — `git commit -m "feat: dev grant source active only while the dev identity switch is on"`.

### Task 7: App-owned JSON grant source with the last-administer invariant

**Files:**
- Modify: `src/examples/Motiv.RulesEngine.Sample/GrantSources.cs`
- Modify: `src/examples/Motiv.RulesEngine.Sample/Program.cs`
- Test: `src/examples/Motiv.RulesEngine.Sample.Tests/JsonFileGrantSourceTests.cs`

**Interfaces:**
- Produces:

```csharp
internal sealed record GrantRecord(string Subject, string Prefix, string Verb); // "read"|"author"|"publish"|"administer"
internal enum GrantRemovalOutcome { Removed, NotFound, LastAdminister }

internal sealed class JsonFileGrantSource(string path) : IGrantSource
{
    public bool SupportsAdministration => true;
    public IReadOnlyCollection<string> KnownRoles => [];  // app grants bind subjects, not roles
    public IReadOnlyList<GrantRecord> All { get; }
    public void Add(GrantRecord grant);
    public GrantRemovalOutcome Remove(GrantRecord grant); // refuses removing the last administer row
    public bool AnyAdministrators { get; }
    // IGrantSource: GrantsFor matches rows by Subject == PrincipalIdentity-style subject claim;
    // administer rows ignore Prefix. IsAdministrator: any administer row for the subject.
}
```

Persistence mirrors `JsonFilePropositionStore` (same file-io/locking idiom — read it first and copy its shape). Config: `Motiv:Grants:Source` = `"app"` (default when dev identity is off) with `Motiv:Grants:Path` (default `grants.json` under content root).

- [ ] **Step 1: Write the failing tests**

```csharp
public class JsonFileGrantSourceTests
{
    [Fact]
    public void Should_grant_only_the_matching_subject()
    {
        // Arrange
        var source = new JsonFileGrantSource(TempPath());
        source.Add(new GrantRecord("alice", "pricing", "author"));

        // Act & Assert
        GrantEvaluator.IsGranted(source.GrantsFor(Principal("alice")), GrantVerb.Author, "pricing.eu").ShouldBeTrue();
        GrantEvaluator.IsGranted(source.GrantsFor(Principal("bob")), GrantVerb.Author, "pricing.eu").ShouldBeFalse();
    }

    [Fact]
    public void Should_refuse_removing_the_last_administer()
    {
        // Arrange
        var source = new JsonFileGrantSource(TempPath());
        var admin = new GrantRecord("root", "", "administer");
        source.Add(admin);

        // Act & Assert — the grant-lockout twin of gate lockout
        source.Remove(admin).ShouldBe(GrantRemovalOutcome.LastAdminister);
        source.IsAdministrator(Principal("root")).ShouldBeTrue();
    }

    [Fact]
    public void Should_persist_across_instances()
    {
        // Arrange
        var path = TempPath();
        new JsonFileGrantSource(path).Add(new GrantRecord("alice", "pricing", "publish"));

        // Act
        var reloaded = new JsonFileGrantSource(path);

        // Assert
        reloaded.All.ShouldContain(new GrantRecord("alice", "pricing", "publish"));
    }

    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), $"grants-{Guid.NewGuid():N}.json");

    private static ClaimsPrincipal Principal(string subject) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, subject)], "test"));
}
```

- [ ] **Step 2: Run to verify failure.**
- [ ] **Step 3: Implement** `JsonFileGrantSource` per the contract (in-memory list guarded by a lock, serialized to `path` on every mutation, loaded in the constructor; `Remove` counts administer rows first and refuses on the last one). Wire in `Program.cs`'s `else` branch (dev identity off): read `Motiv:Grants:Source`; `"app"`/absent → `JsonFileGrantSource`; `"claims"` → Task 9's source.
- [ ] **Step 4: Run to verify pass.**
- [ ] **Step 5: Commit** — `git commit -m "feat: app-owned grant store that cannot drop its last administer"`.

### Task 8: Admin + capabilities endpoints

**Files:**
- Modify: `src/examples/Motiv.RulesEngine.Sample/Program.cs`
- Test: `src/examples/Motiv.RulesEngine.Sample.Tests/AdminEndpointTests.cs`

**Interfaces:**
- Produces app-side routes: `GET /api/admin/capabilities` (authenticated) → `{ "grantAdministration": bool, "administrator": bool, "devIdentity": bool }`; `GET/POST/DELETE /api/admin/grants` — 403 unless `IsAdministrator`, 404 when `!SupportsAdministration` (the surface doesn't exist for an immutable source). POST/DELETE bodies are `GrantRecord` JSON. DELETE returns 409 with an explanatory body on `LastAdminister`.

- [ ] **Step 1: Write the failing tests** — dev-identity factory (dev is admin): `GET /api/admin/capabilities` → `administrator: true`, `grantAdministration: false`; `GET /api/admin/grants` → 404 (dev source immutable). App-store factory (`WithWebHostBuilder` setting `Motiv:DevIdentity:Enabled=false`… but that kills auth — instead register a test auth handler and `JsonFileGrantSource` via `builder.ConfigureServices`): POST then GET round-trips a grant; DELETE of the last administer → 409.
- [ ] **Step 2: Run to verify failure.**
- [ ] **Step 3: Implement** in `Program.cs` after `MapMotivRules`:

```csharp
app.MapGet("/api/admin/capabilities", (HttpContext http, IGrantSource grants) => Results.Json(new
{
    grantAdministration = grants.SupportsAdministration,
    administrator = grants.IsAdministrator(http.User),
    devIdentity = devIdentityEnabled
})).RequireAuthorization();

var admin = app.MapGroup("/api/admin/grants").RequireAuthorization();
admin.MapGet("", (HttpContext http, IGrantSource grants) =>
    grants is not JsonFileGrantSource store ? Results.NotFound()
    : !grants.IsAdministrator(http.User) ? Results.StatusCode(403)
    : Results.Json(store.All));
admin.MapPost("", (HttpContext http, IGrantSource grants, GrantRecord record) =>
{
    if (grants is not JsonFileGrantSource store)
        return Results.NotFound();
    if (!grants.IsAdministrator(http.User))
        return Results.StatusCode(403);
    store.Add(record);
    return Results.NoContent();
});
admin.MapDelete("", (HttpContext http, IGrantSource grants, GrantRecord record) =>
    grants is not JsonFileGrantSource store ? Results.NotFound()
    : !grants.IsAdministrator(http.User) ? Results.StatusCode(403)
    : store.Remove(record) switch
    {
        GrantRemovalOutcome.Removed => Results.NoContent(),
        GrantRemovalOutcome.LastAdminister => Results.Json(
            new { error = "cannot remove the last administer grant" }, statusCode: 409),
        _ => Results.NotFound()
    });
```

(Task 22 revisits the `is JsonFileGrantSource` checks when the bootstrap decorator wraps the store — extract a helper then.)

- [ ] **Step 4: Run to verify pass.**
- [ ] **Step 5: Commit** — `git commit -m "feat: grant admin and capabilities endpoints, administer-gated"`.

### Task 9: IdP-claims grant source

**Files:**
- Modify: `src/examples/Motiv.RulesEngine.Sample/GrantSources.cs`, `Program.cs`, create `src/examples/Motiv.RulesEngine.Sample/appsettings.json`
- Test: `src/examples/Motiv.RulesEngine.Sample.Tests/ClaimsGrantSourceTests.cs`

**Interfaces:**
- Produces:

```csharp
internal sealed record ClaimsGrantMapping(string ClaimType, string ClaimValue, string Prefix, string Verb);

/// <summary>Maps IdP group/role claims to namespace grants via app config — the IdP does not
/// know Motiv's namespaces, so the mapping lives here. Administered in the IdP, so no in-app
/// administration surface.</summary>
internal sealed class ClaimsGrantSource(IReadOnlyList<ClaimsGrantMapping> mappings) : IGrantSource
{
    public bool SupportsAdministration => false;
    public IReadOnlyCollection<string> KnownRoles { get; } // distinct mapping ClaimValues
    // GrantsFor: mappings whose (ClaimType, ClaimValue) the principal holds, Verb != "administer"
    // IsAdministrator: any held mapping with Verb == "administer"
}
```

Config shape (checked into `appsettings.json`, inert until `Motiv:Grants:Source=claims` — Phase 5's compose activates it):

```json
{
  "Motiv": {
    "Grants": {
      "ClaimsMapping": [
        { "claimType": "role", "claimValue": "motiv-pricing-author", "prefix": "pricing", "verb": "author" },
        { "claimType": "role", "claimValue": "motiv-pricing-publisher", "prefix": "pricing", "verb": "publish" },
        { "claimType": "role", "claimValue": "motiv-admin", "prefix": "", "verb": "administer" }
      ]
    }
  }
}
```

(`"role"` is normalized to `ClaimTypes.Role` when binding the mapping — Keycloak roles arrive flattened there via Task 23's `OnTokenValidated`.)

- [ ] **Step 1: Write the failing tests** — a principal with role claim `motiv-pricing-author` gets `author` on `pricing` and is not an administrator; a `motiv-admin` role-holder `IsAdministrator`; `KnownRoles` lists all three mapped values.
- [ ] **Step 2: Run to verify failure.**
- [ ] **Step 3: Implement** the source and the `Program.cs` `"claims"` branch (bind `Motiv:Grants:ClaimsMapping` with `builder.Configuration.GetSection(...).Get<List<ClaimsGrantMapping>>()`).
- [ ] **Step 4: Run to verify pass.**
- [ ] **Step 5: Commit** — `git commit -m "feat: IdP-claims grant source with config-owned claims-to-prefix mapping"`.

### Task 10: Admin pane in the UI, rendered only for a mutable source

**Files:**
- Create: `ui/apps/demo/src/panes/AdminPage.tsx`
- Modify: `ui/apps/demo/src/App.tsx`, `ui/apps/demo/src/panes/AppBar.tsx` (nav link), `ui/apps/demo/src/routing/useHashRoute.ts` (add `admin` route)
- Test: `ui/apps/demo/test/admin-page.test.tsx`

**Interfaces:**
- Consumes: Task 8's `/api/admin/capabilities` and `/api/admin/grants` (plain `fetch` — they are app endpoints, not part of `@motiv-rules/core`'s SDK client).
- Produces: `<AdminPage />`; the Admin nav item renders only when capabilities report `grantAdministration && administrator`.

- [ ] **Step 1: Write the failing component test** (Vitest + Testing Library, mock `fetch`): renders the grants table when capabilities allow; renders nothing (and no nav link) when `grantAdministration` is false; posting the add-grant form calls `POST /api/admin/grants` with `{subject, prefix, verb}`.
- [ ] **Step 2: Run to verify failure** — `pnpm -C ui/apps/demo test`.
- [ ] **Step 3: Implement** `AdminPage` (fetch capabilities on mount; grants table with subject/prefix/verb columns and a delete button per row; a three-field add form; 409 from delete surfaces the "last administer" message inline). Follow the existing pane structure/styles in `ui/apps/demo/src/panes/` — copy `PropositionsPage`'s layout idioms rather than inventing new ones.
- [ ] **Step 4: Run `pnpm -C ui/apps/demo test` and `pnpm -C ui/apps/demo typecheck` to verify pass.**
- [ ] **Step 5: Commit** — `git commit -m "feat: grant admin pane, shown only for a mutable grant source"`.

**Phase 2 checkpoint:** run the full solution suite + `pnpm -r test`; spawn the `code-simplifier` agent over Phase 1–2 changes; apply findings; re-run affected tests; commit.

---

# Phase 3 — The envelope and the gate (ticket 13)

### Task 11: Governance domain model

**Files:**
- Create: `src/Motiv.Serialization/Governance/ChangeRequest.cs`
- Test: `src/Motiv.Serialization.Tests/Governance/ChangeRequestTests.cs`

**Interfaces (produces — verbatim, used by every later task):**

```csharp
namespace Motiv.Serialization;

public enum ChangeTargetKind { Rule, Proposition }

public sealed record ChangeTarget(ChangeTargetKind Kind, string Name)
{
    /// <summary>The parent namespace of the dotted name ("" when the name has no dot).</summary>
    public string Namespace { get; } // computed in ctor: substring before the last '.'
}

public sealed record Approval(string Approver, DateTimeOffset TimestampUtc, IReadOnlyList<string> Roles);

/// <summary>Workflow lifecycle — distinct from any version-row status (ticket 11 vs 13).</summary>
public enum ChangeRequestStatus { Draft, InReview, Approved, Published, Rejected, Withdrawn }

/// <summary>Derived from the diff except rollback, which is stored intent (a rollback and a
/// coincidentally-identical authoring share a diff but are different governance events).</summary>
public sealed record ChangeClassification(
    bool IsCreation, bool IsDeletion, bool IsMetadataOnly, bool TouchesAsyncSpec,
    bool IsRollback, int? RollbackOfVersion);

/// <summary>One artefact's proposed new state. Null document = deletion / revert to compiled default.</summary>
public sealed record ProposedChange(
    ChangeTarget Target, string? ProposedDocumentJson, int BaseVersion, ChangeClassification Classification);

/// <summary>The governance envelope: 1..N proposed changes that publish atomically.</summary>
public sealed class ChangeRequest
{
    public ChangeRequest(Guid id, string author, string changeNote, IReadOnlyList<ProposedChange> proposedChanges);
    public Guid Id { get; }
    public string Author { get; }
    public string ChangeNote { get; }
    public IReadOnlyList<ProposedChange> ProposedChanges { get; }
    public IReadOnlyList<Approval> Approvals { get; }          // accumulating positive assents
    public ChangeRequestStatus Status { get; }                  // Draft on creation
    public string? RejectionReason { get; }                     // a rejection is terminal-with-reason, not an approval row
    public bool PublishedUnderBreakGlass { get; }
    internal void AddApproval(Approval approval);               // Draft/InReview only; moves Draft → InReview
    internal void MarkPublished(bool underBreakGlass);
    internal void MarkRejected(string reason);
    internal void MarkWithdrawn();
}
```

- [ ] **Step 1: Write the failing tests** — `ChangeTarget("pricing.eu.vat").Namespace == "pricing.eu"`; a bare name yields `""`; a new request is `Draft` with no approvals; `AddApproval` moves it to `InReview` and accumulates; mutators throw `InvalidOperationException` from terminal states; `MarkPublished(underBreakGlass: true)` stamps the flag.
- [ ] **Step 2: Run to verify failure.**
- [ ] **Step 3: Implement** exactly the contract above (constructor requires ≥1 proposed change; empty change note allowed — workflow policy can gate it later).
- [ ] **Step 4: Run to verify pass.**
- [ ] **Step 5: Commit** — `git commit -m "feat: ChangeRequest governance envelope with ProposedChange and approvals"`.

### Task 12: Document structural diff

**Files:**
- Create: `src/Motiv.Serialization/Governance/RuleDocumentComparer.cs`
- Test: `src/Motiv.Serialization.Tests/Governance/RuleDocumentComparerTests.cs`

**Interfaces:**
- Consumes: internal `RuleDocument` / `RuleNode` (same assembly).
- Produces: `internal static class RuleDocumentComparer { public static bool StructurallyEqual(RuleDocument left, RuleDocument right) }` — same operator tree, spec references, expressions, quantifier settings; **ignores** `WhenTrueText`/`WhenFalseText`/payload elements, node `Name`, and parameter declarations' descriptions. Equal structure + different text = a metadata-only change.

- [ ] **Step 1: Write the failing tests** (parse fixture documents with `new RuleDocumentParser(new RuleSerializerOptions()).Parse(json, errors)` — match the actual `RuleSerializerOptions` construction used in `RuleSerializer`): identical docs equal; same tree with changed `whenTrue` text equal (the metadata-only case); swapped operator (`and`→`or`) unequal; added child unequal; changed spec name unequal; changed higher-order `n` unequal.
- [ ] **Step 2: Run to verify failure.**
- [ ] **Step 3: Implement**

```csharp
namespace Motiv.Serialization;

/// <summary>
/// Structural equality of two rule documents: the logic tree without its display metadata.
/// Feeds change.is-metadata-only — a typo fix in an assertion string deserves a lighter gate
/// than a logic change.
/// </summary>
internal static class RuleDocumentComparer
{
    public static bool StructurallyEqual(RuleDocument left, RuleDocument right) =>
        NodesEqual(left.Root, right.Root);

    private static bool NodesEqual(RuleNode? left, RuleNode? right)
    {
        if (left is null || right is null)
            return left is null && right is null;
        if (left.Operator != right.Operator
            || left.SpecName != right.SpecName
            || left.ExpressionText != right.ExpressionText
            || left.N != right.N
            || left.NParameterName != right.NParameterName
            || left.PathText != right.PathText
            || left.Children.Count != right.Children.Count)
            return false;
        for (var i = 0; i < left.Children.Count; i++)
            if (!NodesEqual(left.Children[i], right.Children[i]))
                return false;
        return true;
    }
}
```

(Recursion depth mirrors the parser's own guarded nesting depth, so parser-accepted documents cannot overflow here. Task 14 adds `Args` to this comparison — a parameter change is a logic change.)

- [ ] **Step 4: Run to verify pass.**
- [ ] **Step 5: Commit** — `git commit -m "feat: structural rule-document diff for metadata-only classification"`.

### Task 13: `ChangeClassifier`

**Files:**
- Create: `src/Motiv.Serialization/Governance/ChangeClassifier.cs`
- Test: `src/Motiv.Serialization.Tests/Governance/ChangeClassifierTests.cs`

**Interfaces:**
- Consumes: `RuleDocumentComparer` (12), `RuleDocumentParser`, `DocumentReferences.From(document)`.
- Produces:

```csharp
internal static class ChangeClassifier
{
    /// <summary>Pure function of the proposed and base documents — storing derivable facts invites
    /// drift. Rollback intent is the exception the diff cannot recover.</summary>
    public static ChangeClassification Classify(
        string? proposedDocumentJson,
        string? baseDocumentJson,
        bool targetExists,
        Func<string, bool> specIsAsync,
        int? rollbackOfVersion);
}
```

Semantics: `IsCreation = !targetExists`; `IsDeletion = targetExists && proposedDocumentJson is null`; `IsMetadataOnly` = both documents present, both parse cleanly, and `StructurallyEqual`; `TouchesAsyncSpec` = any referenced spec name of the proposed document satisfies `specIsAsync`; `IsRollback = rollbackOfVersion.HasValue`. Unparseable documents classify as all-false (validation elsewhere rejects them before publish).

- [ ] **Step 1: Write the failing tests** — creation, deletion, metadata-only (text-only change), logic change (not metadata-only), async-touching (`specIsAsync` true for one referenced name), rollback intent stored.
- [ ] **Step 2: Run to verify failure.**
- [ ] **Step 3: Implement** per the semantics above (parse each side once with `RuleDocumentParser`; short-circuit when a side is null).
- [ ] **Step 4: Run to verify pass.**
- [ ] **Step 5: Commit** — `git commit -m "feat: derive change classification from the document diff"`.

### Task 14: Parameterised spec registration + spec-node `args`

**Files:**
- Modify: `src/Motiv.Serialization/SpecRegistry.cs`, `SpecRegistryEntry.cs`, `RuleNode.cs`, `RuleDocumentParser.cs`, `RuleErrorCode.cs`, `RuleBinder.cs` (+ the same one-line resolution swap in `MetadataRuleBinder.cs`, `AsyncRuleBinder.cs`, `AsyncMetadataRuleBinder.cs`), `Governance/RuleDocumentComparer.cs`
- Test: `src/Motiv.Serialization.Tests/ParameterisedSpecTests.cs`

**Interfaces:**
- Produces:

```csharp
// SpecRegistry
public SpecRegistry RegisterParameterised<TModel>(
    string name,
    IReadOnlyList<RuleParameterDeclaration> parameters,
    Func<IReadOnlyDictionary<string, object?>, SpecBase<TModel, string>> factory,
    string? description = null);
```

Document form: `{"spec": "change.approver-count-at-least", "args": {"n": 2}}` — `args` is an optional sibling of `spec` holding scalar JSON values, parsed into `RuleNode.Args` (`Dictionary<string, object?>?`). At bind time a parameterised entry resolves `Args` against its declarations via the existing `RuleParameterResolver.Resolve` (missing/surplus/type-mismatch errors and defaults for free — this is why ticket 13 verified the resolver covers scalars), then calls the factory. `args` on a non-parameterised spec → new `RuleErrorCode.UnexpectedArguments`; a parameterised spec is composable by every binder (the factory result is a plain `SpecBase`).

- [ ] **Step 1: Write the failing tests**

```csharp
public class ParameterisedSpecTests
{
    [Fact]
    public void Should_bind_and_evaluate_a_parameterised_spec()
    {
        // Arrange
        var registry = new SpecRegistry().RegisterParameterised(
            "list.count-at-least",
            [new RuleParameterDeclaration("n", RuleParameterType.Integer, false, null)],
            values => Spec.Build((List<int> list) => list.Count >= (int)values["n"]!)
                .WhenTrue($"has at least {values["n"]} items")
                .WhenFalse($"has fewer than {values["n"]} items")
                .Create());
        var serializer = new RuleSerializer(registry);

        // Act
        var spec = serializer.Deserialize<List<int>>(
            """{"spec": "list.count-at-least", "args": {"n": 2}}""");

        // Assert
        spec.Evaluate([1, 2]).Satisfied.ShouldBeTrue();
        spec.Evaluate([1]).Satisfied.ShouldBeFalse();
    }

    [Fact]
    public void Should_reject_a_missing_argument()
    {
        // Arrange — registry as above
        // Act: Validate("""{"spec": "list.count-at-least"}""")
        // Assert: one error with RuleErrorCode.MissingParameter
    }

    [Fact]
    public void Should_reject_a_mistyped_argument()
    {
        // args: {"n": "two"} → RuleErrorCode.ParameterTypeMismatch
    }

    [Fact]
    public void Should_reject_args_on_a_plain_spec()
    {
        // {"spec": "customer.is-active", "args": {"n": 1}} → RuleErrorCode.UnexpectedArguments
    }

    [Fact]
    public void Should_apply_a_declared_default_when_the_argument_is_omitted()
    {
        // declaration (n, Integer, HasDefault: true, DefaultValue: 1); no args → binds with n = 1
    }
}
```

(Fill the sketched bodies with the same arrange shape as the first test. Match `RuleParameterDeclaration`'s actual constructor — it is an `internal sealed class` with a primary constructor; if its accessibility blocks the public `RegisterParameterised` signature, widen the declaration type to `public` in this task rather than inventing a parallel type.)

- [ ] **Step 2: Run to verify failure.**
- [ ] **Step 3: Implement**, smallest-change order:
  1. `RuleNode`: add `public Dictionary<string, object?>? Args { get; set; }`.
  2. Parser: in the `"spec"` node handling, accept an optional `"args"` property (object of string/number/bool/null scalars → `object?` values: `GetString()`, `int`/`long`/`double` per `TryGetInt32`/`TryGetInt64`, `GetBoolean()`); reject non-scalar values with `RuleErrorCode.ParameterTypeMismatch` at path `{path}.args.{name}`. Follow the parser's existing property-dispatch pattern so unknown-property errors keep working.
  3. `SpecRegistryEntry`: add `internal IReadOnlyList<RuleParameterDeclaration>? Parameters { get; }`; for parameterised entries `Spec` holds `Func<IReadOnlyDictionary<string, object?>, object>` (the factory, model-erased the same way plain entries erase the spec).
  4. `SpecRegistry.RegisterParameterised<TModel>` creating such an entry (`IsAsync: false`, `MetadataType: typeof(string)`).
  5. A shared internal helper, e.g. on `SpecRegistryEntry`: `internal object ResolveSpec(RuleNode node, List<RuleError> errors)` — non-parameterised: error if `node.Args != null` (`UnexpectedArguments`), else return `Spec`; parameterised: `RuleParameterResolver.Resolve(Parameters, node.Args, errors)` then invoke the factory (skip invocation when resolve reported errors, returning a placeholder failure). Swap each binder's spec-entry access to go through this helper — find the exact site by following how `RuleBinder` currently resolves `node.SpecName` to an entry's `Spec` object.
  6. `RuleDocumentComparer.NodesEqual`: also compare `Args` dictionaries (count + key/value equality).
- [ ] **Step 4: Run the full `Motiv.Serialization.Tests` project** — new tests pass, nothing else regresses.
- [ ] **Step 5: Commit** — `git commit -m "feat: parameterised spec registration with document-level args"`.

### Task 15: The built-in `change.*` gate catalogue

**Files:**
- Create: `src/Motiv.Serialization/Governance/GateSpecs.cs`
- Test: `src/Motiv.Serialization.Tests/Governance/GateSpecsTests.cs`

**Interfaces:**
- Consumes: Tasks 11 + 14, `NamespacePrefix` (3).
- Produces: `public static class GateSpecs { public static SpecRegistry CreateRegistry() }` with exactly these registrations (assertion strings verbatim — later tests match them):

| Name | Args | Satisfied when | WhenTrue / WhenFalse |
|---|---|---|---|
| `change.in-namespace` | `prefix: string` | **any** target name covered by prefix | `change touches namespace '{prefix}'` / `change does not touch namespace '{prefix}'` |
| `change.target-is-proposition` | — | any target kind is Proposition | `change targets a proposition` / `change targets no proposition` |
| `change.approver-count-at-least` | `n: int` | `Approvals.Count >= n` | `change has at least {n} approvals` / `change has fewer than {n} approvals` |
| `change.author-is-approver` | — | any approval by the author | `the author approved their own change` / `no self-approval` |
| `change.approver-has-role` | `role: string` | any approval whose roles contain role | `an approver holds role '{role}'` / `no approver holds role '{role}'` |
| `change.is-rollback` | — | any change classified rollback | `change is a rollback` / `change is not a rollback` |
| `change.is-creation` | — | any change is a creation | `change creates an artefact` / `change creates nothing` |
| `change.is-deletion` | — | any change is a deletion | `change deletes an artefact` / `change deletes nothing` |
| `change.is-metadata-only` | — | **all** changes metadata-only | `change is metadata-only` / `change alters logic` |
| `change.touches-async-spec` | — | any change touches an async spec | `change touches an async spec` / `change touches no async spec` |

All are unnamed explanation propositions (`.Create()` with no name) so the strings ARE the assertions — the gate's refusal `Justification` reads as prose, which is the whole product aesthetic.

- [ ] **Step 1: Write the failing tests** — build small `ChangeRequest` fixtures and assert each spec's satisfaction and assertion string through `new RuleSerializer(GateSpecs.CreateRegistry()).Deserialize<ChangeRequest>(...)`; include the parameterised three via `args`, and the maker-checker composition:

```csharp
[Fact]
public void Should_express_maker_checker_as_a_composition()
{
    // Arrange — maker-checker = approver-count-at-least(1) & !author-is-approver (ticket 12:
    // segregation of duties is a workflow property, not a grant)
    var serializer = new RuleSerializer(GateSpecs.CreateRegistry());
    var gate = serializer.Deserialize<ChangeRequest>(
        """
        {"and": [
            {"spec": "change.approver-count-at-least", "args": {"n": 1}},
            {"not": {"spec": "change.author-is-approver"}}
        ]}
        """);
    var selfApproved = Request(author: "alice", approvals: [new Approval("alice", Now, [])]);
    var peerApproved = Request(author: "alice", approvals: [new Approval("bob", Now, [])]);

    // Act & Assert
    gate.Evaluate(selfApproved).Satisfied.ShouldBeFalse();
    gate.Evaluate(peerApproved).Satisfied.ShouldBeTrue();
}
```

- [ ] **Step 2: Run to verify failure.**
- [ ] **Step 3: Implement** — ten registrations in `CreateRegistry()`; the parameterised ones via `RegisterParameterised`, e.g.:

```csharp
registry.RegisterParameterised(
    "change.in-namespace",
    [new RuleParameterDeclaration("prefix", RuleParameterType.String, false, null)],
    values =>
    {
        var prefix = (string)values["prefix"]!;
        return Spec.Build((ChangeRequest c) =>
                c.ProposedChanges.Any(p => NamespacePrefix.Covers(prefix, p.Target.Name)))
            .WhenTrue($"change touches namespace '{prefix}'")
            .WhenFalse($"change does not touch namespace '{prefix}'")
            .Create();
    },
    "Whether any proposed change lands under the given namespace prefix");
```

and the nullary ones via plain `Register` with `Spec.Build((ChangeRequest c) => ...)` per the table.

- [ ] **Step 4: Run to verify pass.**
- [ ] **Step 5: Commit** — `git commit -m "feat: built-in change.* gate spec catalogue"`.

### Task 16: `ApprovalGate` with permissive default + `IGateStore`

**Files:**
- Create: `src/Motiv.Serialization/Governance/ApprovalGate.cs`
- Test: `src/Motiv.Serialization.Tests/Governance/ApprovalGateTests.cs`

**Interfaces:**
- Produces:

```csharp
/// <summary>Persists the active gate document — the one seam governance needs before spec 2's
/// storage lands. A store is a dumb sink; it validates nothing.</summary>
public interface IGateStore
{
    string? Load();
    void Save(string? documentJson);
}

public sealed record GateDecision(
    bool MayPublish, string Reason, IReadOnlyList<string> Assertions, string Justification);

public enum GateUpdateOutcome { Updated, Invalid, WouldLockOut }

public sealed record GateUpdateResult(
    GateUpdateOutcome Outcome, IReadOnlyList<RuleError> Errors, GateDecision? PreCheck);

/// <summary>The may-publish Policy over ChangeRequest. Satisfied = may publish; an unsatisfied
/// result blocks and its Justification names the unmet conditions. Default: permissive — the
/// only lockout-safe bootstrap; access is still locked by grants, only the ceremony is opt-in.</summary>
public sealed class ApprovalGate
{
    public ApprovalGate(IGateStore? store = null);   // loads + binds any stored document
    public string? DocumentJson { get; }             // null = permissive default
    public GateDecision Evaluate(ChangeRequest change);
    public GateUpdateResult SetGate(string? documentJson, IReadOnlyCollection<string> knownRoles);
}
```

In this task `SetGate` validates + binds + persists (Updated/Invalid); Task 19 adds the synchronous-only guard and Task 20 the `WouldLockOut` pre-check.

- [ ] **Step 1: Write the failing tests** — default gate allows anything with reason `"no approval gate is configured"`; `SetGate` with the maker-checker document then `Evaluate` on an unapproved request → `MayPublish == false` and `Assertions` contains `"change has fewer than 1 approvals"`; invalid JSON → `Invalid` with errors; `SetGate(null, ...)` resets to permissive; a store-backed gate reloads its document in a second `ApprovalGate` instance (in-memory fake store).
- [ ] **Step 2: Run to verify failure.**
- [ ] **Step 3: Implement** — hold `RuleSerializer _serializer = new(GateSpecs.CreateRegistry());` and the bound `SpecBase<ChangeRequest, string>?`; `Evaluate` maps the Motiv result: `new GateDecision(result.Satisfied, result.Reason, [.. result.Assertions], result.Justification.ToString())` (match `Justification`'s actual type — string or tree — from `BooleanResultBase`); guard bind/evaluate with a lock so `SetGate` and `Evaluate` are safe under concurrent requests.
- [ ] **Step 4: Run to verify pass.**
- [ ] **Step 5: Commit** — `git commit -m "feat: ApprovalGate may-publish policy with permissive default"`.

### Task 17: `ChangeRequestSet` workflow + atomic publish

**Files:**
- Create: `src/Motiv.Serialization/Governance/ChangeRequestSet.cs` (includes `ChangeRequestPublisher`)
- Modify: `src/Motiv.Serialization/Rules/RuleSet.cs`, `src/Motiv.Serialization/Propositions/PropositionSet.cs` (extract lock-free cores)
- Test: `src/Motiv.Serialization.Tests/Governance/ChangeRequestSetTests.cs`

**Interfaces:**
- Consumes: Tasks 11, 13, 16; `RuleSet`, `PropositionSet`, `BindingScope.Locked<T>(Func<T>)`.
- Produces:

```csharp
public sealed record NewProposedChange(
    ChangeTargetKind Kind, string Name, string? DocumentJson, int BaseVersion, int? RollbackOfVersion);

public enum ChangeRequestOutcome { Ok, NotFound, InvalidState, GateBlocked, VersionConflict, Invalid }

public sealed record ChangeRequestResult(
    ChangeRequestOutcome Outcome,
    ChangeRequest? Change,
    GateDecision? Gate,                                  // set when GateBlocked
    IReadOnlyList<RuleError> Errors,
    ChangeTarget? FailedTarget,
    int? ConflictVersion,
    IReadOnlyDictionary<string, int>? PublishedVersions); // target name → new version, on publish

public sealed class ChangeRequestSet
{
    public ChangeRequestSet(ApprovalGate gate, RuleSet rules, PropositionSet? propositions);
    public IReadOnlyList<ChangeRequest> All { get; }
    public ChangeRequest? Find(Guid id);
    public ChangeRequestResult Create(string author, string changeNote, IReadOnlyList<NewProposedChange> changes);
    public ChangeRequestResult Approve(Guid id, string approver, IReadOnlyList<string> roles);
    public ChangeRequestResult Reject(Guid id, string reason);
    public ChangeRequestResult Withdraw(Guid id, string caller);      // author only
    public ChangeRequestResult Publish(Guid id, bool breakGlassActive);
}
```

`Create` classifies each change via `ChangeClassifier` against the current base document (rule: `RuleSet.FindEntry(name)?.DocumentJson`; proposition: `PropositionSet.DocumentJsonOf(name)`; `specIsAsync` from the effective catalogue's `IsAsync`). `Publish` = (unless break-glass) `gate.Evaluate` → `GateBlocked` with the decision; then atomic apply.

- [ ] **Step 1: Write the failing tests** — create→publish happy path over a two-change envelope (a proposition creation + a rule edit referencing it — the coordinated-change scenario ticket 13 was sharpened on) applies **both**; a stale `BaseVersion` on either change publishes **neither** (`VersionConflict`, versions unchanged — the atomicity test); gate blocking (maker-checker set, no approvals) → `GateBlocked` with a `GateDecision` naming the unmet condition and **no state change**; approve → publish succeeds; `Withdraw` by a non-author → `InvalidState`; publish of a `Rejected` request → `InvalidState`; `breakGlassActive: true` bypasses a blocking gate and stamps `PublishedUnderBreakGlass`.
- [ ] **Step 2: Run to verify failure.**
- [ ] **Step 3: Implement.**
  1. In `RuleSet`, extract the bodies of `Update`/`Revert` into `internal RuleUpdateResult UpdateCore(...)` / `RevertCore(...)` that assume the scope lock is held; the public methods become `Scope.Locked(() => UpdateCore(...))`. Mirror for `PropositionSet.Create/Update/Withdraw` (their cores keep the existing prospective-bind → dependents → persist → mutate ordering). Run the existing rule/proposition test suites immediately after this refactor, before continuing.
  2. `ChangeRequestPublisher` (private static within `ChangeRequestSet.cs`): inside one `rules.Scope.Locked(...)`, first **validate every change** (target exists/absent as its classification expects, `BaseVersion` matches the current version, non-null documents bind cleanly via the scope's serializer), returning `VersionConflict`/`Invalid`/`NotFound` with `FailedTarget` before touching anything; then **apply every change** through the cores. All-validate-then-all-apply under one lock is the atomicity mechanism.
  3. `ChangeRequestSet` methods: guard status transitions per Task 11's lifecycle; `Publish` on success calls `change.MarkPublished(breakGlassActive)` and returns `PublishedVersions`.
- [ ] **Step 4: Run the full `Motiv.Serialization.Tests` project** (the refactor touches the hot publish path — the rule and proposition suites must stay green).
- [ ] **Step 5: Commit** — `git commit -m "feat: ChangeRequestSet workflow with atomic gate-checked publish"`.

### Task 18: Governance endpoints + no-bypass rewiring

**Files:**
- Create: `src/Motiv.Serialization.AspNetCore/MotivGovernanceEndpoints.cs`, `GovernanceContracts.cs`
- Modify: `src/Motiv.Serialization.AspNetCore/MotivRulesServiceCollectionExtensions.cs` (`AddGovernance`), `MotivRulesEndpoints.cs`, `MotivPropositionEndpoints.cs`
- Modify: `src/examples/Motiv.RulesEngine.Sample/Program.cs` (call `AddGovernance` with a `JsonFileGateStore`), create `src/examples/Motiv.RulesEngine.Sample/JsonFileGateStore.cs`
- Test: `src/Motiv.Serialization.AspNetCore.Tests/GovernanceEndpointTests.cs`

**Interfaces:**
- Produces: `MotivRulesBuilder.AddGovernance(IGateStore? gateStore = null)` — registers `ApprovalGate` and `ChangeRequestSet` singletons (resolving `RuleSet`/`PropositionSet` from DI). Routes under the mounted group:
  - `GET /change-requests`, `GET /change-requests/{id}` — authenticated.
  - `POST /change-requests` (`ChangeRequestCreateRequest(string ChangeNote, IReadOnlyList<ProposedChangeRequest> Changes)`, `ProposedChangeRequest(string Kind, string Name, JsonElement Document, int BaseVersion, int? RollbackOfVersion)`) — requires `Author` grant on **every** target name; author = `PrincipalIdentity.Subject`.
  - `POST /change-requests/{id}/approvals` — requires `Publish` on every target (approve folded into publish); approver subject + role snapshot from the principal.
  - `POST /change-requests/{id}/rejection` (`{reason}`) — requires `Publish` on every target.
  - `POST /change-requests/{id}/withdrawal` — author only (the set enforces it).
  - `POST /change-requests/{id}/publish` — requires `Publish` on every target; `GateBlocked` → **403** `GateRefusalResponse(string Reason, IReadOnlyList<string> Assertions, string Justification)`; `VersionConflict` → 409; `Invalid` → 400; `Ok` → 200 with the request + `PublishedVersions`.
- **No bypass:** when a `ChangeRequestSet` is registered, `PUT/DELETE /rules/{name}` and proposition create/update/delete internally do `Create` (single change, author = caller) + `Publish` in one motion through the same gate — the permissive default keeps today's responses identical; a blocking gate turns them into 403 `GateRefusalResponse`.

- [ ] **Step 1: Write the failing tests** — full workflow over HTTP (create with author grant → 201; approve as a second principal via `X-Test-User` → 200; publish → 200 and the rule's GET reflects the new version); create without author grant on one target → 403; with maker-checker gate installed (seed via `ApprovalGate` resolved from `app.Services`), direct `PUT /rules/{name}` → 403 whose body contains `"no self-approval"`... (the refusal justification), and after a peer-approved change request the publish succeeds; direct PUT with permissive default behaves exactly as the pre-governance contract (assert the existing `RulePutResponse` shape).
- [ ] **Step 2: Run to verify failure.**
- [ ] **Step 3: Implement** — `AddGovernance` on the builder; `MotivGovernanceEndpoints.Map(group, json)` resolving services per-request; rewire the write handlers to branch on `http.RequestServices.GetService<ChangeRequestSet>()`; sample gains `JsonFileGateStore` (trivial `IGateStore` over a text file beside `propositions.json`) and `.AddGovernance(new JsonFileGateStore(gatePath))` in `Program.cs`.
- [ ] **Step 4: Run the AspNetCore + sample test projects, then `pnpm -C ui/apps/demo e2e`** (live-rules e2e must still pass — the permissive default preserves hot-swap).
- [ ] **Step 5: Commit** — `git commit -m "feat: change-request endpoints; direct writes publish through the gate"`.

**Phase 3 checkpoint:** full solution suite; `code-simplifier` over Phase 3 changes; apply + re-test; commit.

---

# Phase 4 — Lockout and layered recovery (ticket 14)

### Task 19: Gate config endpoints — administer-gated, synchronous-only

**Files:**
- Modify: `src/Motiv.Serialization.AspNetCore/MotivGovernanceEndpoints.cs`, `src/Motiv.Serialization/Governance/ApprovalGate.cs`, `RuleErrorCode.cs`
- Test: `src/Motiv.Serialization.AspNetCore.Tests/GateEndpointTests.cs`, extend `ApprovalGateTests.cs`

**Interfaces:**
- Produces routes: `GET /gate` (authenticated) → `{ "document": <json|null>, "permissiveDefault": bool }`; `PUT /gate` (`{document}`) and `DELETE /gate` (reset to permissive) → `GrantGate.RefuseUnlessAdministrator`, then `gate.SetGate(...)` passing `source?.KnownRoles ?? []`. The gate never governs itself: reconfiguration is an `administer` act at the authorization layer, **not** a `may-publish` act — no `ChangeRequest` is involved.
- `SetGate` gains the synchronous-only guard: after parsing, any referenced spec that resolves to an `IsAsync` registry entry → `Invalid` with new `RuleErrorCode.GateMustBeSynchronous`. (The built-in catalogue is all-sync, so this guards future registry extension — `IsAsync` is bind-visible, which is what makes the check enforceable. `change.touches-async-spec` remains a synchronous predicate *about* the governed change; the gate may ask it without being async.)

- [ ] **Step 1: Write the failing tests** — PUT as non-admin → 403 (gate unchanged); PUT as admin (fake source `IsAdministrator: true`) with maker-checker doc → 200 and `GET /gate` echoes it; DELETE resets to permissive; unit test: `SetGate` against a registry-with-an-async-entry document → `Invalid` + `GateMustBeSynchronous` (register a throwaway async spec into a test copy of the gate registry to prove the guard; use `GateSpecs.CreateRegistry()` plus one `Register(...)` of an async spec).
- [ ] **Step 2: Run to verify failure.**
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run to verify pass.**
- [ ] **Step 5: Commit** — `git commit -m "feat: administer-gated gate config restricted to synchronous specs"`.

### Task 20: The lockout pre-check

**Files:**
- Create: `src/Motiv.Serialization/Governance/SyntheticChangeRequests.cs`
- Modify: `src/Motiv.Serialization/Governance/ApprovalGate.cs`, `MotivGovernanceEndpoints.cs`
- Test: `src/Motiv.Serialization.Tests/Governance/LockoutPreCheckTests.cs`

**Interfaces:**
- Produces:

```csharp
public static class SyntheticChangeRequests
{
    /// <summary>
    /// The most approvable gate-change imaginable: 100 distinct approvers each holding every
    /// known role, not self-approved, a plain single-rule edit under "motiv.governance". If even
    /// this is blocked, no real change could pass. Sound but incomplete — arbitrary predicates
    /// make satisfiability undecidable — so this is a footgun-catcher, not a proof.
    /// </summary>
    public static ChangeRequest MaximallyApprovable(IReadOnlyCollection<string> knownRoles);
}
```

`SetGate` evaluates the candidate (bound, pre-persist) against the synthetic request; unsatisfied → `GateUpdateOutcome.WouldLockOut` with the refusing `GateDecision`, and nothing is saved. `PUT /gate` maps `WouldLockOut` → **422** with a `GateRefusalResponse` (the `Justification` names why — the engine detecting its own potential lockout by evaluating itself).

- [ ] **Step 1: Write the failing tests** — builder shape (100 approvals, all-roles snapshots, author `"synthetic-author"` not among approvers); `SetGate` of `change.approver-has-role("ghost")` with `knownRoles: ["motiv-dev"]` → `WouldLockOut` whose `PreCheck.Assertions` contains `no approver holds role 'ghost'` (verification obligation: a gate referencing a nonexistent role is refused); `SetGate` of maker-checker with any roles → `Updated` (the synthetic passes it); endpoint test: PUT of the ghost-role gate → 422.
- [ ] **Step 2: Run to verify failure.**
- [ ] **Step 3: Implement** (approvals timestamped `DateTimeOffset.UnixEpoch` — deterministic; classification all-false; `BaseVersion: 1`).
- [ ] **Step 4: Run to verify pass.**
- [ ] **Step 5: Commit** — `git commit -m "feat: sound-but-incomplete lockout pre-check on gate publish"`.

### Task 21: Break-glass

**Files:**
- Create: `src/Motiv.Serialization/Governance/BreakGlass.cs`
- Modify: `src/Motiv.Serialization.AspNetCore/MotivGovernanceEndpoints.cs`, `MotivRulesEndpoints.cs` (publish paths), `MotivRulesServiceCollectionExtensions.cs` (default `BreakGlass.Off`)
- Modify: `src/examples/Motiv.RulesEngine.Sample/Program.cs` (+ warning service in `DevIdentity.cs`'s pattern)
- Test: `src/Motiv.Serialization.AspNetCore.Tests/BreakGlassTests.cs`

**Interfaces:**
- Produces:

```csharp
/// <summary>The 3am escape: a deploy-time flag (env/appsettings — requires ops access, never an
/// in-app toggle) that disables the gate while active. An infra-layer privilege above any in-app
/// grant. Loud, audited, and time-boxable so a forgotten break-glass auto-expires.</summary>
public sealed record BreakGlass(bool Enabled, DateTimeOffset? ExpiresUtc)
{
    public static readonly BreakGlass Off = new(false, null);
    public bool Active(DateTimeOffset nowUtc) => Enabled && (ExpiresUtc is null || nowUtc < ExpiresUtc);
}
```

Config: `Motiv:BreakGlass:Enabled`, `Motiv:BreakGlass:ExpiresUtc`. Every publish path resolves `BreakGlass` from DI and passes `Active(DateTimeOffset.UtcNow)` into `ChangeRequestSet.Publish`. After a successful break-glass publish the endpoint emits the audit marker via `ILogger`:
`logger.LogWarning("MOTIV-AUDIT break-glass publish: change request {ChangeRequestId} by {Author} published with the approval gate DISABLED.", ...)` — and the request itself carries `PublishedUnderBreakGlass: true` in responses (the durable stamp; ticket 15's decision log formalizes the trail in spec 3). The sample registers a `BreakGlassWarningService` warning every 60s while active.

- [ ] **Step 1: Write the failing tests** — with a blocking gate and `BreakGlass(true, null)` registered: direct PUT succeeds, response/`GET /change-requests` shows `publishedUnderBreakGlass: true`, and a captured test `ILoggerProvider` recorded a warning containing `MOTIV-AUDIT break-glass publish` (verification obligation); with `BreakGlass(true, ExpiresUtc: past)` the gate blocks again (403).
- [ ] **Step 2: Run to verify failure.**
- [ ] **Step 3: Implement** (SDK: record + `TryAddSingleton(BreakGlass.Off)` in `AddGovernance`, endpoint plumbing + audit log; sample: config binding + warning service registration).
- [ ] **Step 4: Run to verify pass.**
- [ ] **Step 5: Commit** — `git commit -m "feat: audited, time-boxable break-glass that bypasses the gate"`.

### Task 22: Bootstrap-identity elevation

**Files:**
- Modify: `src/examples/Motiv.RulesEngine.Sample/GrantSources.cs`, `Program.cs` (incl. extracting the Task 8 admin-endpoint store checks into a helper that unwraps the decorator)
- Test: `src/examples/Motiv.RulesEngine.Sample.Tests/BootstrapGrantSourceTests.cs`

**Interfaces:**
- Produces:

```csharp
/// <summary>
/// Cold start for an empty app-owned store: a config-designated subject (Motiv:Bootstrap:Subject)
/// holds administer ONLY while the store contains no administer grant. A conditional seed, not a
/// standing superuser — once a real admin exists the elevation goes inert, so a leaked bootstrap
/// config does nothing thereafter. Never an unauthenticated first-run flow.
/// </summary>
internal sealed class BootstrapGrantSource(JsonFileGrantSource inner, string subject) : IGrantSource
{
    public JsonFileGrantSource Store => inner;   // admin endpoints reach the mutable store through this
    // SupportsAdministration/KnownRoles/GrantsFor delegate to inner;
    // IsAdministrator: inner.IsAdministrator(p) || (!inner.AnyAdministrators && subject matches p)
}
```

Wired only when the active source is the app-owned store and `Motiv:Bootstrap:Subject` is configured.

- [ ] **Step 1: Write the failing tests** — empty store: bootstrap subject `IsAdministrator` true, any other subject false; after `inner.Add(new GrantRecord("root", "", "administer"))`: bootstrap subject false (inert), `root` true; grants delegation unchanged.
- [ ] **Step 2: Run to verify failure.**
- [ ] **Step 3: Implement** (+ `Program.cs` wiring and the admin-endpoint helper `TryGetMutableStore(IGrantSource) → JsonFileGrantSource?` unwrapping the decorator).
- [ ] **Step 4: Run the sample test project to verify pass.**
- [ ] **Step 5: Commit** — `git commit -m "feat: conditional bootstrap administer elevation for cold start"`.

**Phase 4 checkpoint:** full solution suite; `code-simplifier`; apply + re-test; commit.

---

# Phase 5 — OIDC evidence: Keycloak profile + e2e (tickets 03/12, spec §7)

### Task 23: Keycloak compose profile + role-claim flattening

**Files:**
- Modify: `docker-compose.yml`
- Create: `keycloak/motiv-realm.json`
- Modify: `src/examples/Motiv.RulesEngine.Sample/Program.cs` (JwtBearer `OnTokenValidated`)
- Test: manual bring-up + Task 24's e2e (this task has no unit test; its proof is the e2e suite)

- [ ] **Step 1: Extend `docker-compose.yml`** — the default `demo` service is untouched (zero-config dev identity stays the default compose story); the auth profile adds Keycloak and an OIDC-configured app on :5101:

```yaml
  keycloak:
    image: quay.io/keycloak/keycloak:26.0
    profiles: ["auth"]
    command: start-dev --import-realm --http-port 8081
    ports: ["8081:8081"]
    environment:
      KC_BOOTSTRAP_ADMIN_USERNAME: admin
      KC_BOOTSTRAP_ADMIN_PASSWORD: admin
    volumes:
      - ./keycloak/motiv-realm.json:/opt/keycloak/data/import/motiv-realm.json:ro

  demo-auth:
    profiles: ["auth"]
    build:
      context: .
      dockerfile: src/examples/Motiv.RulesEngine.Sample/Dockerfile
    ports: ["5101:5100"]
    environment:
      Motiv__Oidc__Authority: "http://keycloak:8081/realms/motiv"
      Motiv__Oidc__Audience: "motiv-demo"
      Motiv__Grants__Source: "claims"
    depends_on: [keycloak]
```

- [ ] **Step 2: Create the realm** `keycloak/motiv-realm.json`: realm `motiv`; public client `motiv-demo` with `directAccessGrantsEnabled: true` and an audience mapper adding `motiv-demo`; realm roles `motiv-pricing-author`, `motiv-pricing-publisher`, `motiv-admin`; users `alice-author`, `paula-publisher`, `petra-publisher`, `root-admin`, password `password`, each with the matching role(s). (Author it by hand from Keycloak's realm-export shape; verify by importing — step 4 — rather than by eye.)

- [ ] **Step 3: Flatten Keycloak's nested roles** in the sample's JwtBearer options (Keycloak puts realm roles in `realm_access.roles`, which `JwtBearer` does not surface as role claims):

```csharp
o.Events = new JwtBearerEvents
{
    OnTokenValidated = context =>
    {
        // Keycloak nests realm roles under realm_access.roles; flatten them into role claims
        // so the claims→prefix mapping (and ClaimTypes.Role consumers) see them.
        if (context.Principal?.FindFirst("realm_access")?.Value is { } realmAccess
            && JsonDocument.Parse(realmAccess).RootElement.TryGetProperty("roles", out var roles)
            && context.Principal.Identity is ClaimsIdentity identity)
        {
            foreach (var role in roles.EnumerateArray())
                identity.AddClaim(new Claim(ClaimTypes.Role, role.GetString()!));
        }
        return Task.CompletedTask;
    }
};
// Containers talk to Keycloak over http — dev/demo only.
o.RequireHttpsMetadata = false;
```

- [ ] **Step 4: Verify the stack comes up**

```bash
docker compose --profile auth up -d --build
```
then `curl -s -o /dev/null -w '%{http_code}' http://localhost:5101/api/rules/catalog` → expect `401`, and a password-grant token from `http://localhost:8081/realms/motiv/protocol/openid-connect/token` for `alice-author` → `curl` with `Authorization: Bearer` → expect `200`.

- [ ] **Step 5: Commit** — `git commit -m "feat: opt-in Keycloak compose profile with claims-mapped grants"`.

### Task 24: e2e of the authenticated path

**Files:**
- Create: `ui/apps/demo/e2e/auth.spec.ts`
- Modify: `ui/apps/demo/package.json` (script `e2e:auth`)

**Interfaces:**
- Consumes: the `--profile auth` stack (Task 23); env `MOTIV_E2E_AUTH_URL` (e.g. `http://localhost:5101`) and `MOTIV_E2E_KEYCLOAK_URL` (default `http://localhost:8081`). The spec self-skips when `MOTIV_E2E_AUTH_URL` is unset, so the default `pnpm e2e` run is unaffected.

- [ ] **Step 1: Write the spec** — `test.describe.serial`, Playwright `request` contexts only, helper `token(user)` doing the password grant against `/realms/motiv/protocol/openid-connect/token` (`grant_type=password`, `client_id=motiv-demo`, password `password`). Cases (this session's lesson: both prior bugs lived in documented-but-unexercised seams — this suite is the load-bearing deliverable):
  1. Unauthenticated `GET /api/rules/catalog` → 401.
  2. `alice-author` reads the catalog → 200 (unfiltered read).
  3. `alice-author` PUTs a proposition under `pricing` → 403 (author ≠ publish — the ladder is real over real tokens).
  4. `paula-publisher` creates proposition `pricing.e2e-flag` → success; PUT under `fraud.` → 403 (prefix isolation).
  5. `root-admin` PUTs the maker-checker gate → 200; `alice-author` PUT of the gate → 403 (administer-gated).
  6. Maker-checker over the workflow: `paula-publisher` creates a change request for `pricing.e2e-flag`, self-publish → 403 whose body includes `no self-approval`; `petra-publisher` approves; `paula-publisher` publishes → 200 (segregation of duties as a workflow, not a grant).
  7. `root-admin` PUT of a gate requiring role `ghost` → 422 (lockout pre-check over the real role universe).
  8. Cleanup: `root-admin` DELETEs the gate and the created proposition (mirror `live-rules.spec.ts`'s revert discipline — this state is per-process).
- [ ] **Step 2: Run against the compose stack**

```bash
docker compose --profile auth up -d --build
MOTIV_E2E_AUTH_URL=http://localhost:5101 pnpm -C ui/apps/demo exec playwright test e2e/auth.spec.ts
```
Expected: all cases pass. Also run plain `MOTIV_E2E_PORT=5109 pnpm -C ui/apps/demo e2e` to confirm the default suite still skips/passes.
- [ ] **Step 3: Fix anything the suite surfaces** (diagnose in source, not in the spec), re-run to green.
- [ ] **Step 4: Commit** — `git commit -m "test: e2e coverage of the authenticated path against Keycloak"`.

### Task 25: Final verification sweep

- [ ] **Step 1: Verification obligations (spec §7)** — confirm each has a passing automated check and note where: e2e authenticated path (Task 24); ghost-role gate refused at publish (Tasks 20 + 24.7); break-glass publish carries its audit marker (Task 21); a Production host refuses the dev identity unless explicitly acknowledged (Task 2).
- [ ] **Step 2: Full suite**

```bash
export DOTNET_ROOT="$HOME/.dotnet" && export PATH="$HOME/.dotnet:$PATH" && dotnet test Motiv.slnx -f net10.0
```
plus `pnpm -r --dir ui build && pnpm -r --dir ui typecheck && pnpm -r --dir ui test` and both e2e runs. Everything green.
- [ ] **Step 3: `code-simplifier`** over Phases 4–5 changes; apply; re-test.
- [ ] **Step 4: Documentation** — per project convention this feature needs `README.md` (brief Core Features example: secure-by-default + the gate) and `docs/` pages (`docs/governance/index.md` + method pages + `toc.yml` entries + `docs/Overview.md`); `CONTEXT.md` already carries the glossary and `docs/adr/0001` the ADR. Write these against the *actual* shipped API.
- [ ] **Step 5: Commit** — `git commit -m "docs: governance and authentication documentation"`.

---

## Out of Scope (tracked elsewhere — do not build here)

- `MaxCompositionDepth` / structural caps / stack-safe traversal — spec 3 (ticket 19), despite appearing in spec 1 §2.
- `ChangeRequest`/version-row persistence, `IRuleStore`, audit/decision log — specs 2–3 (tickets 02/09/10/15/16).
- The `Motiv.Studio` rename and SPA login UI — spec 4 (tickets 08/17).
- TS schema (`schema.ts`) support for spec-node `args` and gate-authoring UI — follow-up under ticket 06's pinned-schema `$id` discipline; flag it in the PR description.
