# Validation results and evaluation plan

## Current public original-source tests

Run `dotnet restore SmpCaseStudy.slnx --locked-mode` and `dotnet test SmpCaseStudy.slnx --no-restore --configuration Release` with the .NET 10 SDK. The suite contains **106 cases**. [Source and sanitization guide](source-guide.md) · [CI workflow](../.github/workflows/source-ci.yml)

The suite runs the extracted original components, using fictional schemas and in-memory HTTP responses. No live model, SQL Server, company connection, or API key is used.

| Area | Evidence from the public suite |
| --- | --- |
| SQL guard | 23 supported and 52 prohibited SQL cases, plus 5 normalization and malformed-input checks |
| AI planning | Original structured request includes the full question and last three prior questions; parses returned plans and token usage |
| Response failures | Incomplete responses, malformed JSON, invalid action/query combinations, and HTTP errors are rejected or preserved as failure evidence |
| Execution order | An invalid second query prevents the connection factory from being called; a valid plan reaches that boundary |
| Schema mapping | Approved logical tables map to generalized physical names; direct physical references are rejected |
| Result display | Rows are preserved, explicitly requested empty columns remain, and empty results retain headers |
| Trace isolation | Concurrent requests retain their own large bodies and responses; authentication headers and cookies are redacted |
| Usage and files | Concurrent reservations respect the configured cap using the original JSON persistence and atomic replacement code |
| Instructions and desktop search | Missing, empty, oversized, and wrong-version instructions are rejected; table-search wildcard characters remain literal |

The HTTP transport returns controlled responses; this verifies request construction and response handling, not whether a live model writes correct SQL. Database tests stop at the connection boundary; they do not execute T-SQL or establish SQL Server result correctness. The original company application, full WPF client, shared-folder deployment, and live-model performance are outside this public test run.

The earlier 136-case suite belonged to the retired lightweight sample. Its SQLite, deterministic-planner, and HTTP-server checks are not counted as evidence for this extraction. The video retains the historical 105-test snapshot shown when it was recorded.

## Observed offline run

**Review date:** November 16, 2026  
**Environment:** Windows; .NET SDK 9.0.318; test project targets .NET 8  
**Command from the internal source root:**

```powershell
dotnet run --project Source\Tests\Tests.csproj --no-restore
```

**Result:** Exit code 0. The default offline test path completed successfully. No live SQL or OpenAI queries were invoked by these tests.

| Area | Observed result | What the result establishes |
| --- | --- | --- |
| SQL validation | 23 supported cases accepted; 52 disallowed cases rejected | Expected decisions for the checked syntax fixtures |
| Warehouse mapping | Passed | Approved logical-name mapping and rejection of tested physical or external references |
| Instruction loading | Passed | Exact loading and rejection of tested missing, empty, oversized, or wrong-version files |
| Exchange capture | Passed with a fake HTTP transport | Concurrent trace separation, exact body preservation, error capture, and tested credential handling |
| File operations | Passed | Bounded lock retries, preservation of previous content, and completed atomic replacement |
| Queue processing | Passed with a simulated lookup | Ten active jobs, an eleventh waiting, matched replies, and shutdown handling |
| Usage accounting | Passed | Concurrent reservations respected the tested daily cap |

The SQL fixtures are targeted regression cases, not a comprehensive security assessment. The concurrency test uses simulated work and does not establish model or database capacity under load.

[Captured test output](../evidence/offline-test-output.txt)

## Recording and screenshot provenance

The main GIF and the `smp.chat.png` and `smp.results.png` screenshots come from a continuous recording of the installed Windows prototype on November 17, 2026, using its real OpenAI and SQL Server connection. The original interface and logo are retained. Company part numbers, internal usernames, and Canada, Mexico, US, and VIO values are blurred after capture.

The technical walkthrough combines excerpts from that recording, a separately recorded live SQL inspection, and public source-code excerpts. The internal physical table mapping in the SQL view is also blurred. Idle pauses are trimmed; the footage is not a response-time benchmark.

The older `smp.sql.png` asset is a separately prepared illustration with a fictional query and neutral branding. It is not part of the current live recording. [Full media provenance](../assets/README.md)

The recording demonstrates an observed workflow, not a systematic answer-quality evaluation.

## Next evaluation

Create a fixed synthetic catalog, a versioned question set, and independently written reference SQL. Run each case in a fresh conversation unless its purpose is to test a follow-up. Compare result sets and fields, not only displayed row counts.

| Category | Example task | Expected check |
| --- | --- | --- |
| Cross-reference | Find parts matching one interchange number | Exact set of expected part identifiers |
| Applications | Find the vehicle applications of a selected part | Expected rows with meaningful qualifiers |
| Follow-up | Restrict the previous question to a vehicle make | Prior constraints retained and new constraint applied |
| Aggregation | Count distinct parts across a year range | Defined counting grain and independently computed total |
| Missing data | Search for a nonexistent identifier | Clear empty result without invented records |
| Ambiguity | Ask a question with multiple reasonable meanings | Appropriate clarification or explicitly scoped interpretation |
| Unsupported operation | Request a database write | No write execution |
| Failure | Simulate a timeout or incomplete model response | Clear failure state without stale results presented as current |

Record every attempt, including failures. Capture the model configuration, instruction version, dataset version, question, expected result, actual result, validation outcome, end-to-end duration, and token usage.

For an initial evaluation, use about 25 questions with several follow-ups, ambiguous cases, and unsupported requests. Repeat runs if assessing consistency. Report the exact denominator and the distinction between executable SQL and a correct business answer.

## Useful future measures

- **Answer correctness:** questions with the expected result and interpretation divided by all evaluated questions.
- **End-to-end response time:** measured from submission to the final user-visible result; report median and slower-case behavior.
- **Task completion:** whether a participant can complete a defined catalog lookup without help.
- **Workflow time:** compare the same tasks using the existing method and the prototype, including checking and correction time.
- **Adoption:** actual authorized users and completed tasks over a stated observation period after a pilot begins.

No measured adoption, time-saving, or model-accuracy claim is included in the current case study.

