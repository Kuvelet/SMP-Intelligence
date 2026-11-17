# Live demo

**https://kuvelet.github.io/SMP-Intelligence/**

The live demo runs in the visitor's browser. No sign-in, installation, company database or API key is required. GitHub Pages serves the application files; it does not receive catalog questions or result rows through an application API.

## Actual C# in the browser

The WebAssembly project compiles the existing `ExamplePlanner`, `SqlGuard`, `SqliteDialect`, `QueryPipeline`, `LookupService` and model files directly from `Smp.Sample.Core`. These are linked source files, not a JavaScript reimplementation of the validator. ScriptDom still parses and validates the proposed T-SQL.

Only the database transport changes. `BrowserCatalog.cs` calls a browser-local SQLite engine provided by sql.js. Each execution creates a new in-memory database, loads the same fictional fixture with parameterized inserts, enables query-only mode, and returns up to 100 rows. C# retains the plan validation and result processing. The interface exposes the validated T-SQL and adapted SQLite query.

The HTML, CSS and interface JavaScript are generated from the local sample's shared assets. `prepare-browser.py` changes the hosting text and loads the browser transport. No second independent UI is maintained.

## Build it

Prerequisites for developers: .NET 10 SDK, Python 3 and Node.js 24. Visitors need only a current browser.

```sh
python scripts/prepare-browser.py --base-path /SMP-Intelligence/
node --test scripts/browser-database.test.mjs
dotnet restore sample/Smp.Sample.Browser --locked-mode
dotnet publish sample/Smp.Sample.Browser --no-restore -c Release -o artifacts/browser
```

The deployable static files are in `artifacts/browser/wwwroot`. Serve them under `/SMP-Intelligence/`, matching the base path. For a local static server mounted at the root, prepare with `--base-path /`, publish, and serve that directory instead. Generated assets, package downloads and build output are ignored by Git.

The SQLite package version and SHA-512 integrity are committed in `scripts/browser-dependency.json`. The build fetches and verifies the package, then includes its JavaScript, WebAssembly and license locally. The deployed demo does not depend on a third-party CDN. .NET dependencies have a committed NuGet lock file.

## Deployment

The [Live demo workflow](../.github/workflows/live-demo.yml) tests the shared C# code and browser database adapter, builds the WebAssembly app, and deploys the output to GitHub Pages. The repository's Pages source is **GitHub Actions**. The deployment uses GitHub's generated Pages token; there are no stored hosting credentials or model secrets.

The live link is also listed in the main README and the first card on [Can Kuvelet's portfolio](https://kuvelet.github.io/portfolio/).

## What the demo establishes

- The real C# validator can be exercised through the UI, including rejecting writes and unsupported tables.
- Reference lookups, vehicle follow-ups and distinct-part counts run against real SQLite queries over fictional records.
- The five browser database tests check the same expected rows, query-only mode, empty-result headers and display cap as the local executor. The existing 105 C# tests remain in place.

The offline question planner is intentionally limited to example questions and sample identifiers. This demo does not evaluate a live AI model.

Browser-side code is inspectable and modifiable by its visitor. The validator illustrates application behavior; it is not a server-side security boundary protecting a shared database. Each visitor has only their own fictional, in-memory catalog. No account, production data or shared writable backend is connected.

The first visit downloads the .NET and SQLite WebAssembly runtimes as ordinary website assets. Loading may take a few moments. Trimming is disabled to preserve ScriptDom's reflection-based AST traversal; this favors faithful behavior over minimum download size.
