# Spec 3D — The Durable Decision Sink — Implementation Plan

**Design:** [2026-08-25-spec-3d-durable-decision-sink-design.md](../specs/2026-08-25-spec-3d-durable-decision-sink-design.md)
**Ticket:** [#138](https://github.com/karlssberg/Motiv/issues/138), resolving the durable half of
[15](https://github.com/karlssberg/Motiv/issues/115)

## Global constraints

- **TDD throughout.** Failing test → confirm it fails for the right reason → minimum code → green.
- **Purely additive.** Nothing that compiles today stops compiling. `IDecisionSink`,
  `InMemoryDecisionSink`, `DecisionLog` and every option on it are untouched; this slice is a second
  implementation of an existing seam plus its own package.
- **No provider reference in the library.** `Motiv.Serialization.Sql` builds against
  `System.Data.Common` alone. The moment a `Microsoft.Data.Sqlite` using-directive appears in `src/`
  rather than in the test project, the "any engine" claim is dead.
- **Behavioural tests on SQLite, structural proof on three dialects.** The same split
  `ProviderSchemaTests` uses for the authoring store, and for the same reason: nothing in the write
  path inspects a provider error code, so what is left unproven by a SQLite-only suite is the SQL text
  itself.
- **Run the whole solution at the end.** Per CLAUDE.md the example projects assert justification
  strings, and this slice edits the sample host.

## File structure

```
src/Motiv.Serialization.Sql/Motiv.Serialization.Sql.csproj          (new)
src/Motiv.Serialization.Sql/SqlDecisionSink.cs                      (new)
src/Motiv.Serialization.Sql/SqlDecisionSinkOptions.cs               (new)
src/Motiv.Serialization.Sql/DecisionSqlDialect.cs                   (new)
src/Motiv.Serialization.Sql/DecisionSchema.cs                       (new, internal — names + statements)
src/Motiv.Serialization.Sql/DecisionQuery.cs                        (new)
src/Motiv.Serialization.Sql/DecisionPurgeReport.cs                  (new)
src/Motiv.Serialization.Sql.Tests/*                                 (new suites)
Motiv.slnx                                                          (both projects)
Directory.Packages.props                                            (Microsoft.Data.Sqlite, tests only)
src/examples/Motiv.RulesEngine.Sample/Program.cs                    (durable sink, /api/decisions)
docs/decision-log/durable.md, toc.yml                               (new page)
docs/decision-log/sink.md, README.md                                (point at it)
```

## Sequence

1. **Scaffold.** Both projects, solution entries, the SQLite package version for the test project.
2. **Retention refuses to be omitted.** The first test: constructing a sink with no window throws, and
   the message says what to set. Then finite/positive validation.
3. **Schema.** `EnsureSchemaAsync` is idempotent; a second call over a live table is a no-op.
4. **Round-trip.** A batch through `WriteAsync` comes back through `ReadAsync` with all three anchors,
   the caller, the correlation id, the outcome and each of the three input postures intact.
5. **Gaps.** `WriteGapAsync` lands in its own table and reads back; a gap is never returned among
   records.
6. **Purge.** Past the window goes, inside stays, gaps age out on `LastDroppedUtc`, and a purge larger
   than one batch takes several passes rather than one long lock.
7. **Lifecycle.** Disposal stops the purge loop and leaves the write path working.
8. **End to end.** A real `DecisionLog` over a real SQLite file: enqueue, dispose, and find the records
   in the database — the crash-loss window closed on purpose.
9. **Three dialects.** Every statement the sink issues is generated for SQLite, PostgreSQL and SQL
   Server and asserted structurally.
10. **Sample.** A second SQLite file, `motiv-decisions.db`, so the separateness invariant is a fact of
    the reference deployment rather than a paragraph. `/api/decisions` reads the durable sink.
11. **Docs**, then the full solution run, then the mandatory `code-simplifier` pass.
