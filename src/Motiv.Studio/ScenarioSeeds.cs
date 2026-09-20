using Motiv.Serialization;

/// <summary>
/// The four sample customers every demo rule starts with — the rows Studio's Evaluate table used to
/// seed in the browser. Seeded once per rule: a rule whose scenarios an operator emptied stays
/// empty, because the marker row remains. The marker is a scenario too, under a reserved id the
/// endpoints never list, so no store needs to know about seeding.
/// </summary>
internal static class ScenarioSeeds
{
    internal const string MarkerId = "__seeded";

    private static readonly (string Name, string Model)[] Seeds =
    [
        ("Active adult, 3 orders", """{ "customerId": "cust-42", "age": 30, "isActive": true, "orderCount": 3, "orders": [{ "total": 120 }] }"""),
        ("Minor", """{ "customerId": "cust-7", "age": 16, "isActive": true, "orderCount": 1, "orders": [{ "total": 20 }] }"""),
        ("Dormant account", """{ "customerId": "cust-9", "age": 41, "isActive": false, "orderCount": 12, "orders": [{ "total": 300 }] }"""),
        ("New, no orders", """{ "customerId": "cust-1", "age": 25, "isActive": true, "orderCount": 0 }"""),
    ];

    /// <summary>Writes the seeds for every rule that has never been seeded. Returns how many scenarios were written.</summary>
    public static async Task<int> SeedAsync(
        IScenarioStore store, IEnumerable<string> ruleNames, CancellationToken cancellationToken)
    {
        var seeded = 0;
        foreach (var rule in ruleNames)
        {
            var existing = await store.ForRuleAsync(rule, cancellationToken);
            if (existing.Any(row => row.Id == MarkerId))
                continue;

            foreach (var (name, model) in Seeds)
            {
                await store.PutAsync(
                    new StoredScenario(rule, Guid.NewGuid().ToString("N"), name, model, null, null, 0, "system", DateTimeOffset.MinValue),
                    baseVersion: 0, cancellationToken);
                seeded++;
            }

            await store.PutAsync(
                new StoredScenario(rule, MarkerId, "seeded", "{}", null, null, 0, "system", DateTimeOffset.MinValue),
                baseVersion: 0, cancellationToken);
        }

        return seeded;
    }
}
