using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using SMP;

namespace Smp.Core.Tests;

// These tests call the extracted original methods. Only HTTP responses/schema and
// the connection factory are substituted; no second planner or SQL pipeline exists.
public sealed class OriginalWorkflowTests : IDisposable
{
    readonly string folder = Path.Combine(Path.GetTempPath(), "smp-review-" + Guid.NewGuid().ToString("N"));
    static readonly Settings Settings = new() { Model = "test-model", DailyLimit = 100 };
    static readonly DataSchema Schema = new([
        new("Interchange", "Part Number", "nvarchar"), new("Interchange", "Price", "nvarchar"),
        new("ApplicationViO", "Part", "nvarchar"), new("ApplicationViO", "Make", "nvarchar"),
        new("ApplicationViO", "Model", "nvarchar"), new("ApplicationViO", "Year", "nvarchar")]);
    const string FakeKey = "fictional-test-credential";

    public OriginalWorkflowTests() => Directory.CreateDirectory(folder);
    public void Dispose() => Directory.Delete(folder, true);
    Lookup Planner(HttpMessageHandler handler) => new(folder, handler,
        _ => Task.FromResult(Schema), () => FakeKey, PlanningKnowledge.Build);

    static string Response(QueryPlan plan, string status = "completed") => JsonSerializer.Serialize(new {
        id = "fictional-response", status,
        incomplete_details = new { reason = "max_output_tokens" },
        output = new[] { new { type = "message", content = new[] {
            new { type = "output_text", text = JsonSerializer.Serialize(plan, QueueFiles.Json) }
        } } }, usage = new { input_tokens = 12, output_tokens = 34 }
    });

    [Fact]
    public async Task Planner_sends_original_structured_request_and_preserves_the_full_question()
    {
        const string question = "Which parts match DEMO-OE-1001 under $40?";
        var expected = new QueryPlan("query", "Parts below the requested price", [
            new("Parts", "SELECT [Part Number] FROM dbo.Interchange WHERE TRY_CONVERT(decimal(18,2),Price) < 40", "detail")]);
        using var handler = new FakeTransport(_ => Response(expected));
        using var lookup = Planner(handler);
        var result = await lookup.Plan(question, Settings, CancellationToken.None, ["oldest", "one", "two", "three"]);
        Assert.Equal("https://api.openai.com/v1/responses", handler.Uri);
        using var sent = JsonDocument.Parse(Assert.Single(handler.Bodies));
        var root = sent.RootElement;
        Assert.Equal("test-model", root.GetProperty("model").GetString());
        Assert.False(root.GetProperty("store").GetBoolean());
        Assert.Equal(PlanningKnowledge.Build(), root.GetProperty("instructions").GetString());
        Assert.True(root.GetProperty("text").GetProperty("format").GetProperty("strict").GetBoolean());
        using var input = JsonDocument.Parse(root.GetProperty("input").GetString()!);
        Assert.Equal(question, input.RootElement.GetProperty("current_question").GetString());
        Assert.Equal(new[] { "one", "two", "three" }, input.RootElement.GetProperty("previous_questions").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(new[] { "current_question", "current_schema", "previous_questions" }, input.RootElement.EnumerateObject().Select(x => x.Name).Order());
        Assert.Equal(expected.Queries[0].Sql, Assert.Single(result.Plan.Queries).Sql);
        Assert.Equal((12, 34), (result.InputTokens, result.OutputTokens));
    }

    [Theory]
    [InlineData("clarify")]
    [InlineData("unsupported")]
    public async Task Non_query_actions_return_an_answer_without_entering_database_execution(string action)
    {
        using var lookup = Planner(new FakeTransport(_ => Response(new(action, "Please clarify the request.", []))));
        var answer = await lookup.Ask("A fictional question", Settings, CancellationToken.None);
        Assert.Equal("Please clarify the request.", answer.Summary);
        Assert.Empty(answer.Tables);
        Assert.Null(answer.GeneratedSql);
        Assert.Single(answer.OpenAiRequests!);
    }

    [Theory]
    [InlineData("incomplete")]
    [InlineData("failed")]
    [InlineData("in_progress")]
    public async Task Incomplete_responses_never_produce_an_executable_plan(string status)
    {
        using var lookup = Planner(new FakeTransport(_ => Response(new("query", "Partial", []), status)));
        var error = await Assert.ThrowsAsync<ApiCompletionException>(() => lookup.Plan("DEMO-OE-1001", Settings, CancellationToken.None));
        Assert.Contains(status, error.Diagnostic);
    }

    [Theory]
    [InlineData("clarify")]
    [InlineData("unsupported")]
    [InlineData("unknown-action")]
    public async Task Invalid_action_query_combinations_are_rejected(string action)
    {
        using var lookup = Planner(new FakeTransport(_ => Response(new(action, "Bad plan", [new("Parts", "SELECT Part FROM dbo.ApplicationViO")]))));
        await Assert.ThrowsAsync<InvalidOperationException>(() => lookup.Plan("DEMO-OE-1001", Settings, CancellationToken.None));
    }

    [Fact]
    public async Task Malformed_model_output_is_not_treated_as_a_plan()
    {
        using var lookup = Planner(new FakeTransport(_ => "{not-json"));
        await Assert.ThrowsAnyAsync<JsonException>(() => lookup.Plan("DEMO-OE-1001", Settings, CancellationToken.None));
    }

    [Theory]
    [InlineData("DELETE FROM dbo.Interchange")]
    [InlineData("SELECT * FROM OtherDb.dbo.Interchange")]
    public async Task An_invalid_second_query_prevents_even_creating_a_database_connection(string second)
    {
        int connections = 0;
        var plan = new QueryPlan("query", "Two results", [new("First", "SELECT Part FROM dbo.ApplicationViO"), new("Second", second)]);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Database.Execute(plan, Schema, CancellationToken.None,
            () => { connections++; throw new Exception("Connection must not be requested"); }));
        Assert.Equal(0, connections);
        using var details = JsonDocument.Parse((string)error.Data["SqlDiagnostic"]!);
        Assert.Equal(2, details.RootElement.GetProperty("QueryNumber").GetInt32());
        Assert.False(details.RootElement.GetProperty("Executed").GetBoolean());
    }

    [Fact]
    public async Task Validated_plan_reaches_the_real_execution_connection_boundary()
    {
        int connections = 0;
        var marker = new IOException("Test stops before a real connection");
        var error = await Assert.ThrowsAsync<IOException>(() => Database.Execute(
            new("query", "Parts", [new("Parts", "SELECT Part FROM dbo.ApplicationViO")]), Schema, CancellationToken.None,
            () => { connections++; throw marker; }));
        Assert.Same(marker, error);
        Assert.Equal(1, connections);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void Plan_query_count_limits_are_enforced(int count)
        => Assert.Throws<InvalidOperationException>(() => Database.ValidateQueries(new("query", "Count", Enumerable.Range(0, count)
            .Select(_ => new PlannedQuery("Parts", "SELECT Part FROM dbo.ApplicationViO")).ToList()), Schema));

    [Fact]
    public void Physical_mapping_happens_after_logical_validation()
    {
        var sql = SqlGuard.Validate("SELECT Part FROM dbo.ApplicationViO", Schema with { Warehouse = true });
        Assert.Contains("ApplicationViO_ExampleCatalog", sql);
        Assert.Throws<InvalidOperationException>(() => SqlGuard.Validate("SELECT Part FROM dbo.ApplicationViO_ExampleCatalog", Schema with { Warehouse = true }));
    }

    [Fact]
    public void Display_rules_preserve_rows_and_explicitly_requested_empty_columns()
    {
        var table = new TableResult("Parts", "Fictional fixture", ["Part", "Note", "Empty"],
            [["DEMO-PART-101", "", ""], ["DEMO-PART-101", "", ""]], false);
        var shown = Database.DisplayColumns(table, new("Parts", "unused", "detail", ["Note"]));
        Assert.Equal(new[] { "Part", "Note" }, shown.Columns);
        Assert.Equal(2, shown.Rows.Count);
        Assert.All(shown.Rows, row => Assert.Equal(new[] { "DEMO-PART-101", "" }, row));
        Assert.Same(table, Database.DisplayColumns(table, new("Parts", "unused", "summary")));
        var empty = table with { Rows = [] };
        Assert.Same(empty, Database.DisplayColumns(empty, new("Parts", "unused", "detail")));
    }

    [Fact]
    public async Task Concurrent_traces_remain_separate_and_authentication_headers_are_redacted()
    {
        using var lookup = Planner(new FakeTransport(body => " \n" + body + "\n "));
        var answers = await Task.WhenAll(Enumerable.Range(0, 10).Select(i => lookup.CaptureForAnswer(async () => {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
            request.Headers.Authorization = new("Bearer", FakeKey);
            request.Headers.Add("Cookie", "fictional-cookie");
            request.Content = new StringContent(JsonSerializer.Serialize(new { question = $"fictional-question-{i}", extra = new string('x', 70000) }), Encoding.UTF8, "application/json");
            using var response = await lookup.SendToOpenAi(request, "Test", CancellationToken.None);
            return new Answer("ok", "", [], 0, 0);
        })));
        for (int i = 0; i < answers.Length; i++) {
            var trace = Assert.Single(answers[i].OpenAiRequests!);
            using var request = JsonDocument.Parse(trace.RequestJson);
            Assert.Equal($"fictional-question-{i}", request.RootElement.GetProperty("question").GetString());
            Assert.Equal(70000, request.RootElement.GetProperty("extra").GetString()!.Length);
            Assert.Equal(" \n" + trace.RequestJson + "\n ", trace.ResponseBody);
            Assert.Contains("Authorization: Bearer [REDACTED]", trace.RequestText);
            Assert.DoesNotContain(FakeKey, JsonSerializer.Serialize(trace));
            Assert.DoesNotContain("fictional-cookie", JsonSerializer.Serialize(trace));
        }
    }

    [Fact]
    public async Task HTTP_errors_preserve_response_evidence_without_recording_the_key()
    {
        using var lookup = Planner(new FakeTransport(_ => "fictional-rate-limit", HttpStatusCode.TooManyRequests));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => lookup.Ask("DEMO-OE-1001", Settings, CancellationToken.None));
        var trace = Assert.Single(Assert.IsType<List<ApiRequestTrace>>(error.Data["OpenAiRequests"]));
        Assert.Equal(429, trace.HttpStatus);
        Assert.Equal("fictional-rate-limit", trace.ResponseBody);
        Assert.DoesNotContain(FakeKey, JsonSerializer.Serialize(trace));
    }

    [Fact]
    public async Task Concurrent_usage_reservations_do_not_exceed_the_limit()
    {
        using var lookup = Planner(new FakeTransport(_ => throw new Exception("No API calls expected")));
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(async () => {
            try { return await lookup.RunQuestion(3, () => Task.FromResult(true)); }
            catch (InvalidOperationException) { return false; }
        })));
        Assert.Equal(3, results.Count(x => x));
        Assert.Equal(3, QueueFiles.Read<Lookup.Usage>(Path.Combine(folder, "QuestionUsage.json")).Count);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("empty")]
    [InlineData("wrong-version")]
    [InlineData("oversized")]
    public void Invalid_instruction_files_are_rejected(string kind)
    {
        var path = Path.Combine(folder, "instructions.txt");
        if (kind != "missing") File.WriteAllText(path, kind switch { "empty" => "", "oversized" => new string('x', 131073), _ => "Wrong version\n" + PlanningKnowledge.Build() });
        Assert.Throws<InvalidOperationException>(() => PlanningKnowledge.Load(path));
    }

    [Fact]
    public void Accepted_instruction_file_is_loaded_without_rewriting()
    {
        var path = Path.Combine(folder, "instructions.txt");
        var text = PlanningKnowledge.Build();
        File.WriteAllText(path, text);
        Assert.Equal(text, PlanningKnowledge.Load(path));
    }

    [Fact]
    public void Original_desktop_table_search_treats_wildcards_as_literal_text()
        => SMPClient.TableFilter.Verify();

    sealed class FakeTransport(Func<string, string> response, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];
        public string? Uri { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = await request.Content!.ReadAsStringAsync(ct);
            lock (Bodies) { Bodies.Add(body); Uri = request.RequestUri!.AbsoluteUri; }
            await Task.Yield();
            return new(status) { Content = new StringContent(response(body)) };
        }
    }
}
