using Motiv.Serialization;

/// <summary>
/// The four sample customers every demo rule starts with — the rows Studio's Evaluate table used to
/// seed in the browser. Seeded once per rule: a rule whose scenarios an operator emptied stays
/// empty, because the marker row remains. The marker is a scenario too, under a reserved id the
/// endpoints never list, so no store needs to know about seeding. Each seed has a fixed id, so two
/// replicas seeding at once — or a pass cut short before its marker — collide on the key rather
/// than landing a second copy beside the first.
/// </summary>
internal static class ScenarioSeeds
{
    internal const string MarkerId = "__seeded";

    private static readonly (string Id, string Name, string Model)[] Seeds =
    [
        ("seed-active-adult", "Active adult, 3 orders", """{ "customerId": "cust-42", "age": 30, "isActive": true, "orderCount": 3, "orders": [{ "total": 120 }] }"""),
        ("seed-minor", "Minor", """{ "customerId": "cust-7", "age": 16, "isActive": true, "orderCount": 1, "orders": [{ "total": 20 }] }"""),
        ("seed-dormant", "Dormant account", """{ "customerId": "cust-9", "age": 41, "isActive": false, "orderCount": 12, "orders": [{ "total": 300 }] }"""),
        ("seed-new-customer", "New, no orders", """{ "customerId": "cust-1", "age": 25, "isActive": true, "orderCount": 0 }"""),
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

            foreach (var (id, name, model) in Seeds)
            {
                var written = await store.PutAsync(
                    new StoredScenario(rule, id, name, model, null, null, 0, "system", DateTimeOffset.MinValue),
                    baseVersion: 0, cancellationToken);
                if (!written.IsConflict)
                    seeded++;
            }

            await store.PutAsync(
                new StoredScenario(rule, MarkerId, "seeded", "{}", null, null, 0, "system", DateTimeOffset.MinValue),
                baseVersion: 0, cancellationToken);
        }

        return seeded;
    }
}
