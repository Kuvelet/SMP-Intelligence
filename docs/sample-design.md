# Public sample: design and provenance

The public sample makes a focused part of SMP Intelligence runnable without the internal Windows environment. It demonstrates query validation, execution order, catalog lookups, follow-up context and inspectable results.

## What comes from the prototype

`sample/Smp.Sample.Core/SqlGuard.cs` is adapted from the prototype's parser-based validator. It retains the deny-by-default syntax allowlist, read-only single-statement rule, table and function checks, imported-column normalization, local qualifier handling, complexity bounds and legacy TRIM normalization.

The extraction replaces the internal database qualifier with `SampleCatalog`, removes internal diagnostics dependencies, and removes warehouse-specific physical table remapping. It does not include connection strings, credentials, internal paths, model prompts, the Windows client, installation tools, company records or the worker's shared-folder transport.

The 23 supported SQL examples and 52 prohibited examples are retained as independently reported xUnit cases with fictional identifiers. They test the wider SQL Server validator. They are separate from the tests of the narrower SQLite execution path.

The pipeline preserves an important original behavior: **validate the whole plan before executing the first query**. An invalid second query rejects the entire plan. The public implementation represents the successful validation result with a `ValidatedQuery` object whose constructor and execution text are internal to the core assembly.

## What is new for this public sample

| Component | Public sample | Original prototype |
| --- | --- | --- |
| Interface | Local browser UI with two modes | WPF Windows application |
| Runtime | .NET 10 | .NET 8 |
| Planning | Explicit deterministic example planner | OpenAI-generated query plans |
| Data | 8 fictional parts and 12 fictional vehicle applications | Internal catalog tables |
| Execution | Private, in-memory SQLite database per query | SQL Server under the backend identity |
| Query scope | Deliberately narrow portable subset | Broader approved T-SQL subset |
| Transport | Local HTTP requests | Shared-folder requests and replies |
| Context | Previous returned part numbers, held in the browser | Recent questions sent to the model |

The browser sample is a portfolio demonstration, not the planned employee web application. It has no company sign-in, shared worker queue, model accuracy evaluation or production deployment configuration.

## Data provenance

All records in [catalog.json](../sample/Smp.Sample.Core/Data/catalog.json) were invented for this repository. No company database was queried for the sample, and no production rows were copied or transformed. Part references, manufacturers, models, notes, prices and vehicle counts are fictional.

The dataset includes two matches for one reference, multiple vehicle years for one part, a missing note, an unknown-reference scenario, and a catalog part without a vehicle application. These make it possible to demonstrate useful behavior beyond a single successful lookup.

## Execution boundary

1. The offline planner creates a plan, or asks for clarification without touching the database.
2. The pipeline validates all proposed statements with ScriptDom and the original-style guard.
3. The SQLite adapter checks a smaller explicit syntax allowlist. It removes only validated local table qualifiers, changes national string literals to SQLite strings, and maps COUNT_BIG to COUNT. Unsupported syntax is rejected rather than guessed at.
4. The executor creates an isolated in-memory database, loads the fixture using parameters, enables SQLite `query_only`, and runs the adapted statement.
5. The browser displays actual rows, truncation state, and both query representations. It inserts all dynamic content as text rather than HTML.

The fixture is embedded in the application. Visitors cannot supply a connection string, database path or SQL seed file. There is no global conversation state. Concurrent lookups use independent connections and explicit request context.

## Limits worth discussing in an interview

- Syntax validation does not establish business correctness. A legal join can still answer the wrong question. Tests assert expected records and distinct counts as well as validation decisions.
- The inherited guard checks known column names but does not fully resolve every SQL binding. A valid-looking column on the wrong table can pass validation and then fail at execution. The API reports this as an execution error rather than claiming it was rejected before execution.
- SQLite and SQL Server differ in types, comparison rules, functions and query planning. The sample deliberately supports less SQL; it is not a general T-SQL compatibility layer.
- The 100-row display cap does not bound all database work. The small, fixed fixture and restricted single-query shape keep this local demonstration limited. A production service needs appropriate database permissions, workload budgets, cancellation, authentication and deployment controls.
- The offline planner recognizes selected examples and sample identifiers. It does not measure AI reasoning or natural-language accuracy. Replacing it with a model would require a separate integration and evaluation effort.
- `Price` uses SQLite REAL for a simple display example. Financial calculations would require an explicit decimal or integer-minor-unit design.

## Verification

The public suite contains 105 test cases. It checks accepted and blocked SQL, normalization, reference lookups, expected vehicle records, distinct-part counting, empty results, forged follow-up context, validation before execution, portable SQL restrictions, result truncation, concurrent requests and HTTP errors.

Run `dotnet test SmpSample.slnx` from the root. The [GitHub Actions workflow](../.github/workflows/sample-ci.yml) restores pinned dependencies and runs the suite on Windows, Linux and macOS. Passing these tests demonstrates the checked cases, not a comprehensive security assessment or employee adoption.
