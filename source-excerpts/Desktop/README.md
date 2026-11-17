# Original desktop source excerpts

These files come from the original WPF client shown in the recording.

- `Transparency.cs` attaches the SQL and model-exchange views to each answer. It depends on the full internal MainWindow and is included for reading, not as a standalone application.
- `TableFilter.cs` implements literal table search. It has no WPF dependency, is linked into the compiled review library, and is tested with special characters.

See the [source guide](../../docs/source-guide.md) for provenance and omissions.
