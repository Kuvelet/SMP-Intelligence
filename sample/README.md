# Run the sample

Run and inspect the public C# sample locally. This guide covers setup, example workflows, tests, and where to read the code.

This is a small C# application you can run and inspect. It uses a fictional automotive catalog, a validator extracted from SMP Intelligence, a local SQLite database, and a browser interface.

**You need:** the [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) and a browser. Windows, macOS and Linux are supported by the sample. No SQL Server installation, company connection, API key or paid service is required. The first restore downloads packages from NuGet; normal sample use makes no external service calls.

From a terminal:

```sh
git clone https://github.com/Kuvelet/SMP-Intelligence.git
cd SMP-Intelligence
dotnet run --project sample/Smp.Sample.Web
```

Open **http://127.0.0.1:5080**. Stop the app with Ctrl+C. The app binds to your computer's loopback address; it is a local review tool.

If port 5080 is already in use, stop the earlier sample process before starting another instance. No database files need to be reset: each execution creates a private in-memory catalog.

## A two-minute tour

1. Choose **Find matching parts**. `DEMO-OE-1001` returns two parts with different fitment notes.
2. Choose **Follow up on the results**. The two parts return five vehicle application records.
3. Expand **Inspect the validated SQL**. You can see both the SQL Server query and the smaller SQLite query used locally.
4. Choose **Compare the catalog**. Distinct part counts are 3 for Example Motors, 3 for Sample Auto, and 2 for Fictional Vehicles. Multiple model years do not inflate these counts.
5. Open **Test the SQL rules**, choose **Try changing records**, then **Validate & run**. The DELETE is blocked before execution.
6. Try `DEMO-OE-9999` or `DEMO-PART-108` in the lookup. Both produce an empty result for different reasons: an unknown reference and a part without recorded fitment.

The question planner accepts complete example questions, not arbitrary natural language. **It is not connected to a live AI model.** Supported forms are:

- `DEMO-OE-1001` or `Which parts match DEMO-OE-1001?`
- `DEMO-PART-101` or `Show applications for DEMO-PART-101`
- Up to eight distinct part numbers separated by commas or `and`, optionally preceded by `Show applications for`
- `Which vehicles do these parts fit?`, `Which vehicles do these fit?`, `Which vehicles fit?`, or `What applications does it fit?` after a successful lookup
- `Count distinct parts by make`

Letter case, surrounding whitespace, and an optional final question mark are accepted. Other wording or additional conditions require clarification before any SQL executes. For example, `Which parts match DEMO-OE-1001 under $40?` returns an explanation with no records, because price filters are unsupported. The sample never treats that question as an unfiltered reference lookup. The fictional catalog contains references `DEMO-OE-1001` through `DEMO-OE-1007` and parts `DEMO-PART-101` through `DEMO-PART-108`.

## Run the tests

From the repository root:

```sh
dotnet test SmpSample.slnx
```

The suite has 136 test cases, including the prototype's 75 SQL validation cases with fictional identifiers, real database result checks, multi-query rejection, follow-up handling, concurrency and HTTP responses. CI runs the same suite on Windows, Linux and macOS.

For a reproducible restore using the committed dependency lock files:

```sh
dotnet restore SmpSample.slnx --locked-mode
dotnet test SmpSample.slnx --no-restore --configuration Release
```

## Where to read the code

| File | What to look for |
| --- | --- |
| [SqlGuard.cs](Smp.Sample.Core/SqlGuard.cs) | Parser-based validation from the prototype; one read-only statement, approved tables and allowed SQL features |
| [QueryPipeline.cs](Smp.Sample.Core/QueryPipeline.cs) | Validate every query before allowing any execution |
| [SqliteDialect.cs](Smp.Sample.Core/SqliteDialect.cs) | A narrow adapter that rejects unsupported features before converting known syntax |
| [SampleCatalog.cs](Smp.Sample.Core/SampleCatalog.cs) | Parameterized fixture loading, query-only mode, real records and result limits |
| [ExamplePlanner.cs](Smp.Sample.Core/ExamplePlanner.cs) | Explicit offline replacement for the private model integration |
| [LookupService.cs](Smp.Sample.Core/LookupService.cs) | Planning, execution and follow-up part extraction |
| [Program.cs](Smp.Sample.Web/Program.cs) | Small HTTP API and error handling |
| [SqlGuardTests.cs](Smp.Sample.Tests/SqlGuardTests.cs) | 75 inherited query examples and normalization checks |
| [PipelineTests.cs](Smp.Sample.Tests/PipelineTests.cs) | Expected business results and execution boundaries |
| [HttpTests.cs](Smp.Sample.Tests/HttpTests.cs) | Endpoint behavior, rejection details and invalid requests |

## SQL sandbox scope

The input is **SQL Server syntax**, matching the prototype's validator. Use `dbo.Interchange` and `dbo.ApplicationViO`, or their unqualified names. The sample permits a single SELECT, filters, INNER/LEFT joins across at most two table references, DISTINCT, GROUP BY, HAVING and ORDER BY. Supported functions are COUNT, COUNT_BIG, SUM, AVG, MIN, MAX, LOWER, UPPER, LTRIM, RTRIM, plus COALESCE and NULLIF. Single-argument TRIM is normalized by the inherited guard.

The local executor intentionally excludes subqueries, set operations, TOP, window functions, conversions and arbitrary arithmetic expressions. Some of these pass the broader prototype guard but are rejected at the **Sample SQL compatibility** stage. SQLite `LIMIT` is not valid input here; the executor caps displayed results at 100 rows and reports truncation.

`Price` and `VIO` are fictional numeric values. VIO means vehicles in operation in the original domain. The sample values must not be summed as a unique fleet total: different parts can apply to the same vehicles. The two-table sample is deliberately smaller and more strongly typed than the original imported catalog.

[Design decisions and differences from the prototype](../docs/sample-design.md)

[Browser build and GitHub Pages deployment](../docs/live-demo.md)
