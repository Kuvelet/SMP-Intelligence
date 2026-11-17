using System.Diagnostics;
using System.Text.Json;

public sealed class RequestDiagnostics {
    static readonly AsyncLocal<RequestDiagnostics?> current = new();
    readonly List<object> events = [];
    public static RequestDiagnostics? Current { get => current.Value; set => current.Value = value; }
    public void Add(string stage, double milliseconds, object? detail = null) {
        lock(events) events.Add(new { Stage = stage, Milliseconds = Math.Round(milliseconds, 3), Detail = detail });
    }
    public object[] Snapshot() { lock(events) return events.ToArray(); }
    public string Json() => JsonSerializer.Serialize(Snapshot());
    public static IDisposable Measure(string stage, object? detail = null) => new Timing(Current, stage, detail);
    sealed class Timing(RequestDiagnostics? owner, string stage, object? detail) : IDisposable {
        readonly Stopwatch watch = Stopwatch.StartNew();
        public void Dispose() => owner?.Add(stage, watch.Elapsed.TotalMilliseconds, detail);
    }
}
