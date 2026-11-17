using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Smp.Sample.Tests;

public sealed class HttpTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;
    public HttpTests(WebApplicationFactory<Program> app) => client = app.CreateClient();

    [Fact]
    public async Task Lookup_endpoint_returns_sample_rows()
    {
        var response = await client.PostAsJsonAsync("/api/lookup", new { question = "DEMO-OE-1001" });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("tables")[0].GetProperty("rows").GetArrayLength());
        Assert.Equal("Offline example planner", body.GetProperty("planner").GetString());
    }

    [Theory]
    [InlineData("DELETE FROM dbo.Interchange")]
    [InlineData("SELECT * FROM sys.tables")]
    [InlineData("SELECT * FROM dbo.Interchange; DROP TABLE dbo.Interchange")]
    public async Task Rejected_SQL_returns_a_clear_non_execution_response(string sql)
    {
        var response = await client.PostAsJsonAsync("/api/sql", new { sql });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("blocked", body.GetProperty("status").GetString());
        Assert.False(body.GetProperty("executed").GetBoolean());
    }

    [Fact]
    public async Task Invalid_column_binding_is_reported_as_an_execution_error()
    {
        var response = await client.PostAsJsonAsync("/api/sql", new { sql = "SELECT Price FROM dbo.ApplicationViO" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Database execution", body.GetProperty("stage").GetString());
    }

    [Fact]
    public async Task Malformed_JSON_returns_bad_request()
    {
        var response = await client.PostAsync("/api/lookup", new StringContent("{broken", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
