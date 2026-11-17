using Microsoft.Data.Sqlite;
using Smp.Sample;

var builder = WebApplication.CreateBuilder(args);
// A local review tool. No public hosting, company credentials or model key required.
builder.WebHost.UseUrls("http://127.0.0.1:5080");
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 32_768);
builder.Services.AddSingleton<ICatalogExecutor, SampleCatalog>();
builder.Services.AddSingleton<QueryPipeline>();
builder.Services.AddSingleton<LookupService>();
var app = builder.Build();

app.Use(async (context, next) =>
{
    context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    try { await next(); }
    catch (QueryRejectedException error)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { status = "blocked", stage = error.Stage, message = error.Message, executed = false });
    }
    catch (SqliteException)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { status = "error", stage = "Database execution", message = "The query passed the syntax checks but could not run. Check column names, aliases and function arguments against the sample schema." });
    }
});

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/api/health", () => new { status = "ok", data = "fictional", planner = "offline" });
app.MapGet("/api/schema", () => DataSchema.Catalog.Columns);
app.MapPost("/api/lookup", (LookupRequest request, LookupService service) => service.Ask(request));
app.MapPost("/api/sql", (SqlRequest request, QueryPipeline pipeline) =>
    pipeline.Run(new QueryPlan("SQL sandbox", [new("Query result", request.Sql ?? "")])));

app.Run();

public sealed record SqlRequest(string? Sql);
public partial class Program;
