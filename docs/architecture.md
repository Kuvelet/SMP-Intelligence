# SMP Intelligence architecture and decisions

This document describes the implemented Windows desktop prototype. The public repository contains a sanitized extraction of the original backend and selected desktop source, with offline tests. An employee web application with company sign-in remains a future proposal.

## Components

| Component | Responsibility |
| --- | --- |
| Desktop client | Collects questions and displays conversations, tables, SQL, and model exchanges |
| Shared-folder protocol | Exchanges request, status, and response files between the client and backend |
| Backend worker | Checks requests, schedules work, tracks usage, and records diagnostics |
| Query planner | Sends instructions, schema, and questions to the OpenAI API and reads its structured response |
| SQL validator | Parses and restricts proposed SQL before producing normalized SQL |
| Database layer | Executes validated plans and constructs bounded result tables |
| Setup and launcher | Installs the local launcher and checks versioned application releases |

## Request lifecycle

1. The client writes a request containing a question, request ID, Windows identity information, and optional recent questions.
2. The backend claims a request through the shared-folder queue. It validates its shape and checks file ownership against the claimed identity.
3. The planner builds a request from the approved instruction file, schema description, current question, and up to three previous questions.
4. The model returns a structured plan with an action of query, clarify, or unsupported.
5. For a query plan, the database layer validates every proposed statement before opening the execution connection.
6. The validator parses SQL using Microsoft ScriptDom, checks supported syntax and table references, and produces normalized SQL.
7. SQL Server supplies the records. The backend applies presentation rules, records details, and writes the reply.
8. The client matches the reply to its request and provides controls for inspecting tables, proposed SQL, and captured exchanges.

A plan can contain one to three result queries. The backend executes their accepted, normalized text. The SQL view also preserves the originally proposed SQL for inspection; these two representations should not be confused.

## Data sent to the model

| Included in the planning request | Handling |
| --- | --- |
| Catalog instructions | Loaded from a version-checked local instruction file |
| Table structure | Describes columns, types, and schema metadata for the supported tables |
| Current question | User-supplied text |
| Prior questions | Up to three questions for conversational context |
| Database result rows | Remain in the local result flow in this implementation |

Question text is not automatically stripped of confidential content. Prior-question context supports conversational references but cannot guarantee that every follow-up preserves the user's intended constraints.

## Why the SQL boundary matters

The model's proposed SQL passes through an independent validation step. The validator checks the parsed syntax tree against an allowlist, constrains table references, restricts functions and features, and rejects multiple statements within a result query.

The implementation also handles concrete database compatibility issues: legacy trimming syntax, imported column-name prefixes, and mapping approved logical table names to configured warehouse tables.

These controls limit operations and query shape. They do not prove a join is semantically correct, a count uses the intended grain, or a fitment qualifier has been interpreted properly. Reference-answer evaluation is a separate requirement.

## Operational behavior

The worker allows up to ten active requests. Additional requests wait in the file queue. Usage reservation is protected by an in-process lock and recorded before processing begins. That is a single-worker coordination mechanism, not a distributed quota system.

File operations use temporary files, atomic replacement, and bounded retries for transient access errors. Requests and replies carry IDs so the client can match the response to its question. Captured exchanges stay associated with the corresponding answer.

The database layer uses a 40-second command timeout and limits displayed results to 5,000 rows per table, with additional character and cell limits. Display limits do not bound all database work: joins, sorting, and aggregation may still be expensive.

The API key is read through Windows Credential Manager. SQL uses Windows integrated authentication under the backend process identity. Per-employee SQL impersonation is not implemented.

## Deployment

The setup program installs a local launcher. The launcher reads a release manifest, retrieves a missing application version, verifies its expected size and SHA-256, and opens the local application.

The manifest and release location remain part of the trust boundary. A checksum demonstrates agreement with the manifest; it does not independently authenticate the publisher.

## Decisions worth discussing

| Choice | Benefit in this prototype | Limitation or next design question |
| --- | --- | --- |
| WPF desktop client | Integrates with the existing Windows environment | Requires desktop distribution and updates |
| Shared-folder messaging | Uses the existing shared network and file permissions | Couples availability and access to that environment |
| Two logical catalog tables | Keeps the initial task and validation scope concrete | Adding data domains requires new semantics and evaluation |
| SQL generation with parser validation | Supports varied questions within a constrained query language | Business correctness needs separate checks |
| Local result presentation | Gives users inspectable database records | A natural-language interpretation can still be wrong |
| Stored request and response traces | Supports debugging and transparent review | Records need deliberate access and retention decisions |

## Public source extraction

The [source guide](source-guide.md) maps the retained original planner, validator, database code, and desktop excerpts. The compiled public project is a review library with offline tests, not a hosted demo or a complete employee application. The shared-folder worker and deployment infrastructure described above are documented as original-system behavior and are not included in the public build.

## Proposed web evolution

A future internal web application could replace the desktop transport with an authenticated HTTP API while retaining the separation between planning, validation, and execution. Company sign-in, authorization, a dedicated backend identity, and hosting would need to be implemented and tested.

A public catalog tool would require a separately defined public data scope and access model. Neither web design is claimed as part of the completed prototype.

