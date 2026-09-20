using System.ComponentModel;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Motiv.Serialization.AspNetCore;

/// <summary>
/// The tools a coding agent reaches the decision log through. Every tool calls the service the
/// matching HTTP endpoint calls and checks the same grant; a rule or decision the caller may not
/// read is <em>not found</em>, with the same message as one that does not exist, so a tool result
/// never says which rules a caller cannot see. Results are the host's JSON — a model crosses only
/// as what was captured or resolved, and a reference as its key.
/// </summary>
[McpServerToolType]
public sealed class MotivMcpTools(
    IHttpContextAccessor http,
    MotivRulesOptions options,
    RuleSet rules,
    IRuleStore? ruleStore = null,
    PropositionSet? propositions = null,
    IPropositionStore? propositionStore = null,
    IDecisionSource? decisions = null,
    DecisionReproducer? reproducer = null,
    IScenarioStore? scenarios = null)
{
    private const string FidelityVocabulary =
        "Read `fidelity` first: `isExact` means every anchor was honoured; otherwise each note names what slipped " +
        "(RuleVersionMissing, BuildMismatch, PropositionVersionMissing, PropositionBindFailed, ModelRedacted, ModelUnresolved, OutcomeDiverged).";

    [McpServerTool(Name = "get_decision", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("One logged decision by its id: the rule and version that decided, the build, the proposition versions it resolved through, the captured input (a reference capture is its key only), and the outcome with its justification.")]
    public async Task<JsonElement> GetDecision(
        [Description("The decision id.")] Guid id, CancellationToken cancellationToken)
    {
        var record = await ReadableDecisionAsync(id, cancellationToken);
        return Json(DecisionsContracts.Entry(record, options.JsonSerializerOptions));
    }

    [McpServerTool(Name = "list_decisions", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("Logged decisions, newest first, filtered by rule, verdict and window. Only decisions of rules the caller may read are listed.")]
    public async Task<JsonElement> ListDecisions(
        [Description("A rule name, or omitted for every rule.")] string? ruleName = null,
        [Description("true for satisfied decisions only, false for unsatisfied only.")] bool? satisfied = null,
        [Description("The inclusive start of the window, ISO 8601.")] DateTimeOffset? fromUtc = null,
        [Description("The inclusive end of the window, ISO 8601.")] DateTimeOffset? toUtc = null,
        [Description("The most decisions to return; 20 by default, 200 at most.")] int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var source = decisions ?? throw new McpException("this host does not read its decision log back; register a decision source with AddDecisionSource");
        var query = new DecisionQuery
        {
            RuleName = ruleName, Satisfied = satisfied, FromUtc = fromUtc, ToUtc = toUtc, Limit = Math.Clamp(limit ?? 20, 1, 200),
        };
        var records = await source.QueryAsync(query, cancellationToken);
        return Json(records
            .Where(record => Granted(GrantVerb.Read, record.RuleName))
            .Select(record => DecisionsContracts.Entry(record, options.JsonSerializerOptions))
            .ToArray());
    }

    [McpServerTool(Name = "reproduce_decision", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("Re-runs a logged decision under the rule and proposition versions that decided it. " + FidelityVocabulary +
                 " When `model.kind` is `Reference` the model could not be resolved: scaffold the model from its key in the generated test, never invent field values. " +
                 "`csharp` is the rule printed as C# for adoption; `csharpWarnings` say where it needs a person. Follow the target repository's test conventions.")]
    public async Task<JsonElement> ReproduceDecision(
        [Description("The decision id.")] Guid id, CancellationToken cancellationToken)
    {
        await ReadableDecisionAsync(id, cancellationToken);
        var engine = reproducer ?? throw new McpException("this host cannot reproduce decisions; call AddRuleStore() and AddPropositions() beside AddDecisionSource()");
        var reproduction = await engine.ReproduceAsync(id, cancellationToken);
        return Json(DecisionsContracts.Entry(reproduction, options.JsonSerializerOptions));
    }

    [McpServerTool(Name = "print_rule", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("A rule or an authored proposition printed as the C# builder chain that compiles to the same specification — the code to adopt. `warnings` name every place the print needs a person: a collection selector, an object payload, an expression leaf, a reference to a proposition that is not compiled code yet.")]
    public async Task<JsonElement> PrintRule(
        [Description("The rule or proposition name.")] string name,
        [Description("A version from the log, or omitted for the live one.")] int? version = null,
        CancellationToken cancellationToken = default)
    {
        var (documentJson, printOptions) = await PrintableAsync(name, version, cancellationToken);
        if (documentJson is null)
            throw new McpException($"'{name}' v{version} is a recorded revert: the rule ran its compiled default, and there is no document to print");

        try
        {
            var printed = CSharpPrinter.Print(documentJson, printOptions);
            return Json(new CSharpEntry(printed.Source, printed.Warnings));
        }
        catch (RuleSerializationException exception)
        {
            throw new McpException($"'{name}' does not print: {string.Join("; ", exception.Errors.Select(error => error.Message))}");
        }
    }

    [McpServerTool(Name = "get_rule", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("A rule's or an authored proposition's document at a version — the live one by default — with its provenance row when the host keeps a version log.")]
    public async Task<JsonElement> GetRule(
        [Description("The rule or proposition name.")] string name,
        [Description("A version from the log, or omitted for the live one.")] int? version = null,
        CancellationToken cancellationToken = default)
    {
        RequireReadable(name);
        if (rules.FindEntry(name) is { } rule)
        {
            if (ruleStore is not null && await RuleRowAsync(name, version ?? rule.Version, cancellationToken) is { } row)
                return Json(DecisionsContracts.Entry(row));
            if (version is null || version == rule.Version || version == 1)
                return Json(new { name, version = version ?? rule.Version, document = EndpointResponses.DocumentElement(version == 1 ? null : rule.DocumentJson) });
            throw new McpException($"rule '{name}' has no version {version}");
        }

        if (propositions?.Find(name) is { } proposition)
        {
            if (propositionStore is not null && await PropositionRowAsync(name, version ?? proposition.Version, cancellationToken) is { } row)
                return Json(DecisionsContracts.Entry(row));
            if (version is null || version == proposition.Version)
                return Json(new { name, version = proposition.Version, modelType = proposition.ModelType, document = EndpointResponses.DocumentElement(propositions.DocumentJsonOf(name)) });
            throw new McpException($"proposition '{name}' has no version {version}");
        }

        throw NotKnown(name);
    }

    [McpServerTool(Name = "list_scenarios", ReadOnly = true, Idempotent = true, UseStructuredContent = true)]
    [Description("A rule's stored scenarios: named sample models, each with an optional expected verdict and the decision it was saved from. Test data a generated theory can run over.")]
    public async Task<JsonElement> ListScenarios(
        [Description("The rule name.")] string rule, CancellationToken cancellationToken)
    {
        RequireReadable(rule);
        var store = scenarios ?? throw new McpException("this host stores no scenarios; call AddScenarios()");
        var rows = await store.ForRuleAsync(rule, cancellationToken);
        return Json(rows.Where(row => !row.Id.StartsWith("__", StringComparison.Ordinal)).Select(Entry).ToArray());
    }

    [McpServerTool(Name = "save_scenario", ReadOnly = false, Destructive = false, Idempotent = false, UseStructuredContent = true)]
    [Description("Saves a named sample model for a rule as test data. It never changes what the rule decides and is not subject to the approval gate. Needs the 'author' grant on the rule's namespace.")]
    public async Task<JsonElement> SaveScenario(
        [Description("The rule name.")] string rule,
        [Description("The scenario's name.")] string name,
        [Description("The model as JSON text.")] string model,
        [Description("The verdict this scenario expects, or omitted for a sample to look at.")] bool? expectedSatisfied = null,
        [Description("The id of the logged decision this scenario was saved from, when it was.")] string? sourceDecisionId = null,
        CancellationToken cancellationToken = default)
    {
        RequireReadable(rule);
        if (!Granted(GrantVerb.Author, rule))
            throw new McpException($"saving a scenario for '{rule}' requires the 'author' grant on its namespace");
        var store = scenarios ?? throw new McpException("this host stores no scenarios; call AddScenarios()");
        if (string.IsNullOrWhiteSpace(name))
            throw new McpException("a scenario needs a name");
        if (model is null)
            throw new McpException("a scenario needs a model");

        var scenario = new StoredScenario(
            rule, Guid.NewGuid().ToString("N"), name, model, expectedSatisfied, sourceDecisionId,
            Version: 0, PrincipalIdentity.Subject(Context.User), DateTimeOffset.MinValue);
        var written = await store.PutAsync(scenario, baseVersion: 0, cancellationToken);
        if (written.IsConflict)
            throw new McpException($"the scenario id collided; try again");
        var saved = (await store.ForRuleAsync(rule, cancellationToken)).First(row => row.Id == scenario.Id);
        return Json(Entry(saved));
    }

    private HttpContext Context => http.HttpContext ?? throw new McpException("the tool was called outside an HTTP request");

    private bool Granted(GrantVerb verb, string name) => GrantGate.IsGranted(Context, verb, name);

    /// <summary>Unreadable and unknown are the same answer, so a refusal never says which rules exist.</summary>
    private void RequireReadable(string name)
    {
        if (!Granted(GrantVerb.Read, name))
            throw NotKnown(name);
    }

    private static McpException NotKnown(string name) => new($"no rule or proposition '{name}' is known to this host");

    private async Task<DecisionRecord> ReadableDecisionAsync(Guid id, CancellationToken cancellationToken)
    {
        var source = decisions ?? throw new McpException("this host does not read its decision log back; register a decision source with AddDecisionSource");
        var record = await source.FindAsync(id, cancellationToken);
        if (record is null || !Granted(GrantVerb.Read, record.RuleName))
            throw new McpException($"no decision '{id}' is in the log; retention may have purged it");
        return record;
    }

    /// <summary>The document to print and how — a rule's, or a proposition's over its model type.</summary>
    private async Task<(string? DocumentJson, CSharpPrintOptions Options)> PrintableAsync(string name, int? version, CancellationToken cancellationToken)
    {
        RequireReadable(name);
        var serializerOptions = propositions?.Options ?? rules.Options;
        if (rules.Find(name) is { } rule && rules.FindEntry(name) is { } live)
        {
            var documentJson = version is null || version == live.Version ? live.DocumentJson
                : ruleStore is not null && await RuleRowAsync(name, version.Value, cancellationToken) is { } row ? row.DocumentJson
                : version == 1 ? null
                : throw new McpException($"rule '{name}' has no version {version}");
            return (documentJson, RulePrintOptions.For(rule, rules.Scope.Registry, serializerOptions));
        }

        if (propositions?.Find(name) is { } proposition)
        {
            var errors = new List<RuleError>();
            var modelType = propositions.ResolveModel(proposition.ModelType, errors)?.ModelType
                ?? throw new McpException($"proposition '{name}' is over '{proposition.ModelType}', which this host does not register");
            var documentJson = version is null || version == proposition.Version ? propositions.DocumentJsonOf(name)
                : propositionStore is not null && await PropositionRowAsync(name, version.Value, cancellationToken) is { } row ? row.DocumentJson
                : throw new McpException($"proposition '{name}' has no version {version}");
            return (documentJson, RulePrintOptions.For(name, modelType, rules.Scope.Registry, serializerOptions, "Proposition"));
        }

        throw NotKnown(name);
    }

    private async Task<StoredRuleVersion?> RuleRowAsync(string name, int version, CancellationToken cancellationToken) =>
        (await ruleStore!.HistoryAsync(name, cancellationToken)).FirstOrDefault(row => row.Version == version);

    private async Task<StoredPropositionVersion?> PropositionRowAsync(string name, int version, CancellationToken cancellationToken) =>
        (await propositionStore!.HistoryAsync(name, cancellationToken)).FirstOrDefault(row => row.Version == version && !row.IsTombstone);

    private static ScenarioEntry Entry(StoredScenario row) =>
        new(row.Id, row.Name, row.ModelJson, row.ExpectedSatisfied, row.SourceDecisionId, row.Version, row.Author, row.TimestampUtc);

    private JsonElement Json<T>(T value) => JsonSerializer.SerializeToElement(value, options.JsonSerializerOptions);
}
