// Public packaging adapter. This file is new, not extracted from the internal application.
internal static class ReviewConfiguration {
    internal static string Required(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value
            : throw new InvalidOperationException($"{name} is not configured. The public tests do not require live services.");
}
