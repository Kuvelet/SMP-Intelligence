# SMP Intelligence

**A conversational automotive catalog tool built around real business workflows.**

I built SMP Intelligence to help users find matching parts and vehicle applications without writing SQL. The existing workflow involved Microsoft Access, SQL Server, and Excel. The Windows application brings supported catalog questions, follow-ups, result tables, and query inspection into one place.

**Status:** Working prototype developed for internal use and demonstrated to stakeholders.

**Original stack:** C# · .NET 8 · WPF · SQL Server · OpenAI API

This repository is a case study with **sanitized original source and executable tests**. It documents the implementation behind the recorded application; it is not a separately rebuilt demo or a downloadable company deployment.

## The application

![The real SMP application answering a parts question and a vehicle follow-up, with sensitive values blurred](assets/smp-demo.gif?v=live-recording-4)

The recording asks “What parts do we have that cross to OE number 12692048?” and then “what applications does it fit?” It shows the actual interface and live responses. Company part numbers, usernames, and Canada/Mexico/US/VIO values are blurred. [Recording details](assets/README.md)

[Watch the two-minute walkthrough](https://kuvelet.github.io/portfolio/walkthrough/) · [Follow the original source](docs/source-guide.md)

## My contribution

I initiated and developed the prototype, combining automotive catalog knowledge with application development:

- Defined supported catalog workflows and directed implementation and testing.
- Built the desktop experience for questions, follow-ups, tables, and SQL inspection.
- Connected OpenAI query planning to SQL Server retrieval, with validation before execution.
- Added request tracking, usage controls, and Windows installation and update tools.

I used AI coding assistance while defining requirements, directing iterations, and reviewing application behavior.

## How it works

The application sends the question, recent questions, catalog instructions, and approved schema to OpenAI. The model returns a structured plan: query, clarify, or unsupported. Proposed queries pass through an independent SQL validator before the database runs them. Results are formatted locally; database result rows are not sent back to the model in this implementation.

Three engineering decisions matter:

- **Validate the whole plan first.** If a second query is invalid, the first query must not execute either.
- **Make answers inspectable.** Users can examine proposed SQL, result tables, and model exchanges. Authentication headers are redacted from captured requests.
- **Work within the existing Windows environment.** WPF and shared-folder messaging fit the prototype's environment, with request IDs, bounded retries, and application update checks.

[Architecture and tradeoffs](docs/architecture.md)

## Inspect the implementation

| Start here | What it demonstrates |
| --- | --- |
| [AI planning and answer handling](src/Smp.Core/Lookup.cs) | Original Responses API request, structured plan parsing, context, trace capture, and local result summaries |
| [SQL validator](src/Smp.Core/SqlGuard.cs) | Parser-based allowlist, approved tables, column normalization, and logical-to-physical mapping |
| [Database execution](src/Smp.Core/Database.cs) | Whole-plan validation before connection creation, SQL Server execution, limits, and display rules |
| [Desktop transparency](source-excerpts/Desktop/Transparency.cs) | Original code behind the SQL and model-exchange views |
| [Tests](tests/Smp.Core.Tests) | Repeatable checks against the extracted original components |

Company connections, internal instructions, operational history, and deployment infrastructure are excluded. The source guide identifies retained code, sanitization changes, and test-only substitutes. No company records were used for the test fixtures.

## Run the checks

With the .NET 10 SDK installed:

```sh
dotnet restore SmpCaseStudy.slnx --locked-mode
dotnet test SmpCaseStudy.slnx --no-restore --configuration Release
```

The **106 offline test cases** cover supported and prohibited SQL, AI request construction and response handling, rejection before database access, trace isolation and credential redaction, result display rules, and concurrent usage accounting. No API key, SQL Server installation, or company access is required. [CI results](https://github.com/Kuvelet/SMP-Intelligence/actions/workflows/source-ci.yml)

These checks establish specific software behaviors, not live-model answer accuracy or employee adoption. The next evidence to collect is an answer-quality evaluation against reference results, followed by a limited employee pilot. [Validation evidence and limits](docs/validation.md)

Built by [Can Kuvelet](https://kuvelet.github.io/portfolio/).
