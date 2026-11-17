using System.Text.Json;
using Microsoft.JSInterop;

namespace Smp.Sample.Browser;

// A browser-local SQLite engine replaces Microsoft.Data.Sqlite's native transport.
// Planning, validation, SQL adaptation and result handling remain shared C# source.
internal sealed class BrowserCatalog(IJSInProcessRuntime runtime) : ICatalogExecutor
{
    public ResultTable Execute(ValidatedQuery query)
    {
        using var document = JsonDocument.Parse(runtime.Invoke<string>("smpSql.execute", query.ExecutableSql));
        var root = document.RootElement;
        var columns = root.GetProperty("columns").EnumerateArray().Select(c => c.GetString()!).ToArray();
        var rows = root.GetProperty("rows").EnumerateArray().Select(row => (IReadOnlyList<object?>)
            row.EnumerateArray().Select(Cell).ToArray()).ToArray();
        return new(query.Title, query.Sql, columns, rows, root.GetProperty("truncated").GetBoolean(), query.ExecutableSql);
    }

    private static object? Cell(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.Number => value.TryGetInt64(out var integer) ? (object)integer : value.GetDouble(),
        _ => value.GetString()
    };
}
