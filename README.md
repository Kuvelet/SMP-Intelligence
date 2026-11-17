# SMP Intelligence

**Ask questions about automotive parts in everyday language.**

I built SMP Intelligence to make automotive catalog questions easier to answer without writing SQL. The Windows prototype lets users find matching parts, explore which vehicles they fit, and inspect the database queries behind each result.

The existing workflow involved Microsoft Access, SQL Server, and Excel. I combined automotive catalog knowledge with C#/.NET development and OpenAI integration to bring supported lookups and follow-up questions into one application.

**Status:** Working prototype developed for internal use and demonstrated to stakeholders.

**Original application:** C# · .NET 8 · WPF · SQL Server · OpenAI API

**[Watch the two-minute walkthrough →](https://kuvelet.github.io/portfolio/walkthrough/)** · **[Explore the runnable C# sample →](sample/README.md)**

## My contribution

I initiated and developed the prototype, applying my automotive catalog knowledge throughout the project:

- Defined the supported catalog workflows and directed implementation and testing.
- Built the desktop experience for questions, follow-ups, result tables, and SQL inspection.
- Connected AI query planning to database retrieval, with validation before execution.
- Added request tracking, usage controls, and Windows installation and update tools.

I used AI coding assistance while defining requirements, directing iterations, and reviewing application behavior.

## See the real application

![SMP Intelligence performing a parts lookup and vehicle follow-up, with sensitive values blurred](assets/smp-demo.gif?v=live-recording-4)

*Recorded from the working Windows prototype with its original logo and live responses. Company part numbers, usernames, and Canada/Mexico/US/VIO values are blurred. [Recording details](assets/README.md)*

The recorded workflow starts with “What parts do we have that cross to OE number 12692048?” and follows with “what applications does it fit?” Users can inspect both the returned records and the proposed SQL. The narrated walkthrough also shows the public code and tests.

## Engineering decisions

**Validate the entire plan before executing it.** The model proposes SQL; a parser checks permitted operations, tables, and syntax. Every query in a plan must pass validation before the first database call. The public tests verify that an invalid second query prevents either query from running.

**Keep answers inspectable.** SQL Server supplies the records, which the application formats locally. Users can review result tables, proposed queries, and model exchanges. Database result rows are not sent back to the model in this implementation.

**Design for the existing Windows environment.** The prototype uses WPF and shared-folder messaging, with request IDs, bounded file retries, and application update checks. This fits the current environment while leaving authentication and an HTTP service as future web-development work.

[Architecture, tradeoffs, and limitations](docs/architecture.md)

## Public code sample

The repository includes a runnable sample for inspecting and testing selected engineering behavior without company access.

| | Original prototype | Public sample |
| --- | --- | --- |
| Planning | OpenAI-generated query plans | Offline example planner |
| Interface and data | WPF and internal SQL Server catalog | Local browser interface and fictional SQLite catalog |
| Source | Internal application | Adapted query validator, sample pipeline, and tests |

The complete internal application and company database are not included. [What was extracted and what changed](docs/sample-design.md)

With the **.NET 10 SDK** installed:

```sh
git clone https://github.com/Kuvelet/SMP-Intelligence.git
cd SMP-Intelligence
dotnet run --project sample/Smp.Sample.Web
```

Open **http://127.0.0.1:5080**. No API key or SQL Server installation is required.

[Setup and example workflows](sample/README.md) · [Query validator](sample/Smp.Sample.Core/SqlGuard.cs) · [Execution pipeline](sample/Smp.Sample.Core/QueryPipeline.cs)

## Evidence and next step

The public suite contains **136 automated test cases** covering supported and prohibited SQL, expected catalog records, follow-ups, validation before execution, and HTTP behavior. Run `dotnet test SmpSample.slnx`. [Tests](sample/Smp.Sample.Tests) · [CI results](https://github.com/Kuvelet/SMP-Intelligence/actions/workflows/sample-ci.yml)

These checks establish specific software behaviors; they do not establish the accuracy of every AI-generated answer. The next priority is a documented answer-quality evaluation against reference results, followed by a limited employee pilot to measure usefulness and task completion. [Validation evidence and evaluation plan](docs/validation.md)

Built by [Can Kuvelet](https://kuvelet.github.io/portfolio/).
