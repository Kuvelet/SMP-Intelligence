# Guide to the original source

This repository contains a connected extraction of the original SMP implementation for technical review. The compiled backend library and tests target .NET 10; the internal application targets .NET 8 on Windows. It is not a complete deployment of the company application, and there is no replacement browser interface or deterministic question planner.

## Follow a question through the code

1. [`Lookup.Plan`](../src/Smp.Core/Lookup.cs) obtains schema metadata, loads instructions, and sends an actual OpenAI Responses API request with a strict JSON-schema output format. [`PlanningKnowledge.Input`](../src/Smp.Core/PlanningKnowledge.cs) includes the current question, schema, and up to three prior questions.
2. The same planner parses the response, checks completion, and validates its action. Query plans proceed to [`Database.Execute`](../src/Smp.Core/Database.cs); clarification and unsupported actions return without database execution.
3. `Database.ValidateQueries` validates every query through [`SqlGuard`](../src/Smp.Core/SqlGuard.cs) before creating the execution connection. Approved logical table names are mapped to fixed physical names after validation.
4. The database code executes normalized T-SQL using Microsoft.Data.SqlClient. It preserves result rows, applies display limits, and handles explicitly requested empty columns. `Lookup.AskDatabase` formats the result summary locally.
5. [`Transparency.cs`](../source-excerpts/Desktop/Transparency.cs) shows how the original WPF client presents the SQL and captured model exchanges. It is a source excerpt requiring the omitted MainWindow controls; it is not a standalone executable. The original [`TableFilter.cs`](../source-excerpts/Desktop/TableFilter.cs) is portable and is compiled into the review library for its literal-search test.

## Provenance and changes

| File | Origin and public changes |
| --- | --- |
| `Lookup.cs` | Original planner, response parsing, answer handling, trace capture and usage accounting. Added injectable HTTP transport, schema, credential and instruction readers so tests can use fictional fixtures. Replaced company credential access with explicit environment configuration. |
| `Database.cs` | Original schema retrieval, validation and SQL Server execution. Generalized connection and physical identifiers; added a connection-factory seam for testing execution order. |
| `SqlGuard.cs` | Original validator, including warehouse mapping. Database names and normalized column prefixes generalized; no SQLite translation or substitute pipeline. |
| `PlanningKnowledge.cs` | Original instruction loading and context serialization. Version marker changed to distinguish the fictional public fixture from the internal prompt. |
| `RequestDiagnostics.cs` | Original timing and per-request diagnostic collection. |
| `Contracts.cs` | Original answer, table and trace records; only the settings used by this extraction are retained. The unused legacy source field remains for contract fidelity. |
| `QueueFiles.cs` | Original JSON persistence, bounded file retries and atomic replacement used by usage accounting. Company queue identity/access rules are excluded. |
| Desktop excerpts | Original transparency and table-filter source. The full WPF client and company launcher are not distributed. |
| `ReviewConfiguration.cs`, project files and tests | Public packaging and test infrastructure. These additions are identified as such, rather than attributed to the internal application. |

[`source-manifest.json`](source-manifest.json) records original relative paths, original-file fingerprints, public-file fingerprints, and the changes for each extraction. It provides an explicit origin map; it is not an independent authenticity audit. No internal Git history is imported.

The public projects retain the original ScriptDom and SqlClient package versions. A direct patched `System.Security.Cryptography.Pkcs` dependency overrides the vulnerable version inherited through SqlClient. Lock files pin the resulting dependency graph.

## What is deliberately excluded

- Company server addresses, network paths, account restrictions, Windows credential names, production settings, and real records.
- The original catalog instruction text, operational logs, recorded API exchanges, and employee usage history.
- The shared-folder worker host, full desktop application, company updater, installer, and release infrastructure.

The illustrative instruction file and schemas in the tests were written for fictional examples. They do not reproduce the full internal catalog rules. Sanitization replaces database qualifiers with `ExampleCatalog`, physical suffixes with `_ExampleCatalog`, and normalized column prefixes with `Catalog_`.

## What runs here

Run the commands in the [README](../README.md) to compile the extracted backend and execute offline tests. Tests supply in-memory HTTP responses and schema metadata to the original methods. They do not call OpenAI or connect to SQL Server. The first package restore needs NuGet access.

The library retains the real API and SQL Server implementations for inspection. Their default paths require explicitly supplied `SMP_REVIEW_OPENAI_API_KEY`, `SMP_REVIEW_SQL_CONNECTION`, a model in `Settings`, and an approved instruction file. No values or default company connections are supplied, and there is no packaged application entry point. Supplying those settings is not required for review or testing and does not recreate the internal deployment.

## Why the old sample is gone

The earlier offline planner, SQLite catalog, browser interface and WebAssembly deployment were separate demonstration code. They have been removed from the current source tree so reviewers can focus on the original implementation. Earlier commits and the unchanged walkthrough still describe that historical sample. The former demo URL now points visitors to this case study.
