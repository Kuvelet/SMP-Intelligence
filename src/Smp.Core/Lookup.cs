using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SMP;

public interface ILookup : IDisposable { Task<Answer> Ask(string question, Settings settings, CancellationToken ct, IReadOnlyList<string>? context = null); }
public sealed partial class Lookup : ILookup {
    readonly HttpClient http;
    readonly Func<CancellationToken, Task<DataSchema>> readSchema;
    readonly Func<string> readKey;
    readonly Func<string> readInstructions;
    readonly string statePath;
    readonly SemaphoreSlim schemaGate = new(1, 1);
    readonly object usageGate = new();
    DataSchema? schemaCache;
    DateTime schemaTime;
    readonly AsyncLocal<List<ApiRequestTrace>?> requestTrace = new();
    internal async Task<Answer> CaptureForAnswer(Func<Task<Answer>> action) {
        var previous = requestTrace.Value;
        var captured = new List<ApiRequestTrace>();
        requestTrace.Value = captured;
        try { return (await action()) with { OpenAiRequests = captured.ToList() }; }
        catch (Exception ex) { ex.Data["OpenAiRequests"] = captured.ToList(); throw; }
        finally { requestTrace.Value = previous; }
    }
    internal async Task<HttpResponseMessage> SendToOpenAi(HttpRequestMessage request, string purpose, CancellationToken ct) {
        var captured = requestTrace.Value;
        var index = captured?.Count ?? -1;
        if (captured is not null) {
            var body = await request.Content!.ReadAsStringAsync(ct);
            // Application-level HTTP representation, captured before transport. Never persist credentials.
            var text = new StringBuilder().Append(request.Method).Append(' ').Append(request.RequestUri!.AbsoluteUri)
                .Append(" HTTP/").Append(request.Version).Append("\r\n");
            if (request.Headers.Host is null) text.Append("Host: ").Append(request.RequestUri.Authority).Append("\r\n");
            foreach (var header in request.Headers) {
                var value = header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase)
                    ? (request.Headers.Authorization?.Scheme + " [REDACTED]")
                    : header.Key.Equals("Proxy-Authorization", StringComparison.OrdinalIgnoreCase) || header.Key.Equals("Cookie", StringComparison.OrdinalIgnoreCase)
                        ? "[REDACTED]" : string.Join(", ", header.Value);
                text.Append(header.Key).Append(": ").Append(value).Append("\r\n");
            }
            // StringContent computes the UTF-8 byte length used by HttpClient.
            _ = request.Content.Headers.ContentLength;
            foreach (var header in request.Content.Headers) text.Append(header.Key).Append(": ").Append(string.Join(", ", header.Value)).Append("\r\n");
            text.Append("\r\n").Append(body);
            captured.Add(new(purpose, DateTimeOffset.UtcNow, request.RequestUri.ToString(), body, RequestText: text.ToString()));
        }
        var response = await http.SendAsync(request, ct);
        if (captured is not null) {
            captured[index] = captured[index] with { HttpStatus = (int)response.StatusCode };
            try {
                // Preserve the entire response before parsing, including non-success/error bodies.
                captured[index] = captured[index] with { ResponseBody = await response.Content.ReadAsStringAsync(ct) };
            } catch { response.Dispose(); throw; }
        }
        return response;
    }
    public Lookup(string backend) : this(backend, new HttpClientHandler(), Database.Schema,
        () => ReviewConfiguration.Required("SMP_REVIEW_OPENAI_API_KEY"), PlanningKnowledge.Build) { }

    // Public extraction seam: tests supply an HTTP transport and schema without using company systems.
    internal Lookup(string backend, HttpMessageHandler handler,
        Func<CancellationToken, Task<DataSchema>> readSchema, Func<string> readKey, Func<string> readInstructions) {
        statePath = Path.Combine(backend, "QuestionUsage.json");
        http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(85) };
        this.readSchema = readSchema;
        this.readKey = readKey;
        this.readInstructions = readInstructions;
    }
    internal async Task<(QueryPlan Plan, DataSchema Schema, int InputTokens, int OutputTokens)> Plan(string question, Settings settings, CancellationToken ct, IReadOnlyList<string>? context = null) {
        DataSchema schema;
        using (RequestDiagnostics.Measure("schema_wait_and_retrieval")) {
        await schemaGate.WaitAsync(ct);
        try {
            if (schemaCache is null || schemaTime < DateTime.UtcNow.AddMinutes(-10)) {
                using (RequestDiagnostics.Measure("schema_database_read")) schemaCache = await readSchema(ct);
                schemaTime = DateTime.UtcNow;
            }
            schema = schemaCache;
        } finally { schemaGate.Release(); }
        }
        var key = readKey();

        var resultSchema = new {
            type = "object", additionalProperties = false,
            properties = new {
                action = new { type = "string", @enum = new[] { "query", "clarify", "unsupported" } },
                explanation = new { type = "string" },
                queries = new { type = "array", items = new {
                    type = "object", additionalProperties = false,
                    properties = new { title = new { type = "string" }, sql = new { type = "string" },
                        resultKind = new { type = "string", @enum = new[] { "detail", "summary" } },
                        keepEmptyColumns = new { type = "array", items = new { type = "string" } } },
                    required = new[] { "title", "sql", "resultKind", "keepEmptyColumns" }
                } }
            }, required = new[] { "action", "explanation", "queries" }
        };
        var instructions = readInstructions();
        var body = new {
            model = settings.Model, store = false, max_output_tokens = 2400, instructions, input = PlanningKnowledge.Input(question, context, schema),
            text = new { format = new { type = "json_schema", name = "data_question", strict = true, schema = resultSchema } }
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        HttpResponseMessage response;
        string responseText;
        using (RequestDiagnostics.Measure("model_request_and_body")) {
            response = await SendToOpenAi(request, "Generate SQL", ct);
            try { responseText = await response.Content.ReadAsStringAsync(ct); }
            catch { response.Dispose(); throw; }
        }
        using var responseLifetime = response;
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"OpenAI returned HTTP {(int)response.StatusCode}. Check API balance and permissions for the configured account.");
        QueryPlan plan;
        int input, outTokens;
        using (RequestDiagnostics.Measure("model_response_parsing")) {
        using var document = JsonDocument.Parse(responseText);
        var root = document.RootElement;
        CheckCompletion(root, response.Headers.TryGetValues("x-request-id", out var requestIds) ? requestIds.FirstOrDefault() : null);
        var output = string.Concat(root.GetProperty("output").EnumerateArray().Where(x => x.GetProperty("type").GetString() == "message")
            .SelectMany(x => x.GetProperty("content").EnumerateArray()).Where(x => x.GetProperty("type").GetString() == "output_text").Select(x => x.GetProperty("text").GetString()));
        plan = JsonSerializer.Deserialize<QueryPlan>(output, QueueFiles.Json) ?? throw new InvalidDataException();
        var tokens = root.GetProperty("usage"); input = tokens.GetProperty("input_tokens").GetInt32(); outTokens = tokens.GetProperty("output_tokens").GetInt32();
        }
        if (plan.Explanation is null || plan.Explanation.Length > 3000) throw new InvalidOperationException("The query explanation was invalid.");
        if (plan.Action is not ("query" or "clarify" or "unsupported")) throw new InvalidOperationException("Unknown query plan.");
        if (plan.Action != "query" && plan.Queries is { Count: > 0 }) throw new InvalidOperationException("A clarification cannot include queries.");
        return (plan, schema, input, outTokens);
    }
    public Task<Answer> Ask(string question, Settings settings, CancellationToken ct, IReadOnlyList<string>? context = null) => RunQuestion(settings.DailyLimit, () => CaptureForAnswer(async () => {
        return await AskDatabase(question, settings, ct, context);
    }));
    internal async Task<T> RunQuestion<T>(int dailyLimit, Func<Task<T>> action) {
        ReserveUsage(dailyLimit);
        return await action();
    }
    internal async Task<Answer> AskDatabase(string question, Settings settings, CancellationToken ct, IReadOnlyList<string>? context = null) {
        var (plan, schema, input, outTokens) = await Plan(question, settings, ct, context);
        if (plan.Action is "clarify" or "unsupported") return new(plan.Explanation, "", [], input, outTokens);
        if (plan.Action != "query") throw new InvalidOperationException("Unknown query plan.");
        var (tables, details) = await Database.Execute(plan, schema, ct);
        var summary = new StringBuilder("Interpretation: " + plan.Explanation + "\n");
        foreach (var table in tables) {
            if (table.Rows.Count == 1 && table.Columns.Count <= 4 && !table.Truncated)
                summary.AppendLine(table.Title + ": " + string.Join("; ", table.Columns.Select((c,i) => c + " = " + (string.IsNullOrEmpty(table.Rows[0][i]) ? "(empty / null)" : table.Rows[0][i]))));
            else summary.AppendLine($"{table.Title}: {table.Rows.Count:N0} rows shown{(table.Truncated ? " — output limit reached; refine the question for full detail" : "")}.");
        }
        summary.Append("Review the calculation details and any fitment notes before using the results.");
        if (context is { Count: > 0 }) details = "Recent questions used for context:\n" + string.Join("\n", context.Select((q, i) => $"{i + 1}. {q}")) + "\n\n" + details;
        return new(summary.ToString(), "", tables, input, outTokens, details, GeneratedSql: string.Join("\n\n", plan.Queries.Select(q => "-- " + q.Title + "\n" + q.Sql)));
    }
    public static void CheckCompletion(JsonElement root, string? requestId = null) {
        static string? Text(JsonElement value, string key) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(key, out var field) && field.ValueKind == JsonValueKind.String ? field.GetString() : null;
        var status = Text(root, "status");
        if (status == "completed") return;
        root.TryGetProperty("incomplete_details", out var incomplete);
        root.TryGetProperty("error", out var error);
        var diagnostic = JsonSerializer.Serialize(new {
            Status = status ?? "not provided", Reason = Text(incomplete, "reason") ?? "not provided",
            ErrorCode = Text(error, "code"), ResponseId = Text(root, "id"), RequestId = requestId,
            MaxOutputTokens = root.TryGetProperty("max_output_tokens", out var max) && max.ValueKind == JsonValueKind.Number && max.TryGetInt32(out var n) ? (int?)n : null
        });
        throw new ApiCompletionException(diagnostic);
    }
    internal void ReserveUsage(int dailyLimit) {
        using var timing = RequestDiagnostics.Measure("usage_reservation");
        // Reserve before sending; overlapping requests must not lose increments or exceed the cap.
        lock (usageGate) {
            var usage = File.Exists(statePath) ? QueueFiles.Read<Usage>(statePath) : new Usage("", 0);
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            if (usage.Day != today) usage = new(today, 0);
            if (usage.Count >= dailyLimit) throw new InvalidOperationException("The daily question limit has been reached. Please try tomorrow.");
            QueueFiles.Write(statePath, usage with { Count = usage.Count + 1 });
        }
    }
    public void Dispose() { http.Dispose(); schemaGate.Dispose(); }
    public record Usage(string Day, int Count);
}



public sealed class ApiCompletionException(string diagnostic) : InvalidOperationException("The query plan was incomplete. Try a more focused question.") {
    public string Diagnostic { get; } = diagnostic;
}
