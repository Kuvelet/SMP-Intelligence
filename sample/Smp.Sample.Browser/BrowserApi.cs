using System.Text.Json;
using Microsoft.JSInterop;

namespace Smp.Sample.Browser;

public static class BrowserApi
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static QueryPipeline? pipeline;
    private static LookupService? lookup;

    internal static void Initialize(IJSInProcessRuntime runtime)
    {
        pipeline = new(new BrowserCatalog(runtime));
        lookup = new(pipeline);
    }

    [JSInvokable]
    public static string Request(string path, string payload)
    {
        try
        {
            if (pipeline is null || lookup is null) throw new InvalidOperationException("The sample is still loading.");
            object data = path switch
            {
                "/api/lookup" => lookup.Ask(JsonSerializer.Deserialize<LookupRequest>(payload, Json)
                    ?? throw new JsonException("Enter a catalog question.")),
                "/api/sql" => pipeline.Run(new("SQL sandbox", [new("Query result",
                    JsonSerializer.Deserialize<SqlRequest>(payload, Json)?.Sql ?? "")])),
                _ => throw new InvalidOperationException("Unknown sample operation.")
            };
            return JsonSerializer.Serialize(new { ok = true, data }, Json);
        }
        catch (QueryRejectedException error)
        {
            return JsonSerializer.Serialize(new { ok = false, data = new
                { status = "blocked", stage = error.Stage, message = error.Message, executed = false } }, Json);
        }
        catch (JSException)
        {
            return JsonSerializer.Serialize(new { ok = false, data = new
                { status = "error", stage = "Database execution", message = "The query passed the syntax checks but could not run. Check column names, aliases and function arguments against the sample schema." } }, Json);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException)
        {
            return JsonSerializer.Serialize(new { ok = false, data = new
                { status = "error", message = error.Message } }, Json);
        }
    }

    private sealed record SqlRequest(string? Sql);
}
