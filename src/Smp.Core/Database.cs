using System.Data;
using System.Globalization;
using System.Text;
using Microsoft.Data.SqlClient;
using SMP;

public record DataColumnInfo(string Table, string Name, string Type, string? Collation = null) {
    public string DisplayName => Name.TrimStart('\uFEFF');
}
public record DataSchema(List<DataColumnInfo> Columns, bool Warehouse = false) {
    public string Describe() => string.Join("\n", Columns.GroupBy(c => c.Table).Select(g =>
        "dbo." + g.Key + ": " + string.Join(", ", g.Select(c => "[" + c.DisplayName + "] " + c.Type)) +
        "; collations: " + string.Join(", ", g.Select(c => c.Collation ?? "unknown").Distinct())));
}

public record PlannedQuery(string Title, string Sql, string ResultKind = "summary", List<string>? KeepEmptyColumns = null);
public record QueryPlan(string Action, string Explanation, List<PlannedQuery> Queries);
static class Database {
    public const int QueryTimeoutSeconds = 40;
    public static string FailureDetails(Exception error, string sql, string title, long elapsedMs) {
        var failure = error as SqlException;
        return System.Text.Json.JsonSerializer.Serialize(new {
            Title = title, Sql = sql, ElapsedMilliseconds = elapsedMs, CommandTimeoutSeconds = QueryTimeoutSeconds, LockTimeoutMilliseconds = 5000,
            ExceptionType = error.GetType().Name, Message = error.Message, Number = failure?.Number,
            ClientConnectionId = failure?.ClientConnectionId,
            Errors = failure?.Errors.Cast<SqlError>().Select(e => new { e.Number, e.State, e.Class, e.Message, e.Procedure, e.LineNumber }).ToArray()
        });
    }
    internal static SqlConnection CreateConnection() => new(ReviewConfiguration.Required("SMP_REVIEW_SQL_CONNECTION"));
    public const string SessionOptions = "SET ANSI_NULLS ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET ARITHABORT ON; SET CONCAT_NULL_YIELDS_NULL ON; SET QUOTED_IDENTIFIER ON; SET NUMERIC_ROUNDABORT OFF;";
    public static async Task<DataSchema> Schema(CancellationToken ct) {
        using var connection = CreateConnection();
        using (RequestDiagnostics.Measure("sql_connection_open")) await connection.OpenAsync(ct);
        using var command = new SqlCommand("""
            SELECT REPLACE(t.name,'_ExampleCatalog',''),c.name,ty.name,c.collation_name FROM sys.tables t
            JOIN sys.schemas s ON s.schema_id=t.schema_id
            JOIN sys.columns c ON c.object_id=t.object_id
            JOIN sys.types ty ON ty.user_type_id=c.user_type_id
            WHERE s.name='dbo' AND t.name IN ('Interchange_ExampleCatalog','ApplicationViO_ExampleCatalog')
            ORDER BY t.name,c.column_id
            """, connection) { CommandTimeout = 10 };
        using var reader = await command.ExecuteReaderAsync(ct);
        var columns = new List<DataColumnInfo>();
        while (await reader.ReadAsync(ct)) columns.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3)));
        if (columns.Select(x => x.Table).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 2)
            throw new InvalidOperationException("Could not read the schema of both approved tables.");
        foreach (var key in new[] { ("ApplicationViO", "Catalog_PartKey"), ("ApplicationViO", "Catalog_MakeKey"), ("ApplicationViO", "Catalog_ModelKey"), ("ApplicationViO", "Catalog_YearKey"), ("Interchange", "Catalog_PartKey") })
            if (!columns.Any(c => c.Table == key.Item1 && c.Name == key.Item2)) throw new InvalidOperationException("Warehouse search schema is incomplete.");
        return new(columns, Warehouse: true);
    }
    internal static TableResult DisplayColumns(TableResult table, PlannedQuery query) {
        // Keep headers for an empty result; never drop rows or deduplicate records.
        if (query.ResultKind != "detail" || table.Rows.Count == 0) return table;
        var keep = new HashSet<string>((query.KeepEmptyColumns ?? []).Select(c => c.TrimStart('\uFEFF')), StringComparer.OrdinalIgnoreCase);
        var indices = Enumerable.Range(0, table.Columns.Count).Where(i =>
            keep.Contains("*") || keep.Contains(table.Columns[i]) || table.Rows.Any(r => !string.IsNullOrWhiteSpace(r[i]))).ToArray();
        if (indices.Length == table.Columns.Count) return table;
        return table with { Columns = indices.Select(i => table.Columns[i]).ToList(),
            Rows = table.Rows.Select(r => indices.Select(i => r[i]).ToList()).ToList() };
    }
    internal static PlannedQuery[] ValidateQueries(QueryPlan plan, DataSchema schema) {
        if (plan.Queries is null || plan.Queries.Count is < 1 or > 3)
            throw new InvalidOperationException("Ask for no more than three result tables at a time.");
        var validated = new List<PlannedQuery>();
        for (int i = 0; i < plan.Queries.Count; i++) {
            var query = plan.Queries[i];
            try {
                if (query is null || query.Title is null) throw new InvalidOperationException("Invalid query title.");
                if (query.ResultKind is not ("detail" or "summary") || query.KeepEmptyColumns is { Count: > 100 } ||
                    query.KeepEmptyColumns?.Any(c => string.IsNullOrWhiteSpace(c) || c.Length > 128) == true)
                    throw new InvalidOperationException("Invalid result display instructions.");
                validated.Add(query with { Title = query.Title.Length <= 100 ? query.Title : query.Title[..100], Sql = SqlGuard.Validate(query.Sql, schema) });
            } catch (InvalidOperationException ex) {
                var diagnostic = System.Text.Json.JsonSerializer.Serialize(new {
                    Stage = "SQL validation", QueryNumber = i + 1, Title = query?.Title,
                    RejectedSql = query?.Sql, Reason = ex.Message, ExceptionType = ex.GetType().Name,
                    InstructionVersion = PlanningKnowledge.Version, Executed = false
                });
                ex.Data["SqlDiagnostic"] = diagnostic;
                RequestDiagnostics.Current?.Add("sql_validation_rejected", 0, new { Diagnostic = diagnostic });
                throw;
            }
        }
        return validated.ToArray();
    }
    public static async Task<(List<TableResult> Tables, string Details)> Execute(QueryPlan plan, DataSchema schema, CancellationToken ct, Func<SqlConnection>? connectionFactory = null) {
        try { return await ExecuteCore(plan, schema, ct, connectionFactory); }
        catch (Exception ex) {
            ex.Data["GeneratedSql"] = string.Join("\n\n", plan.Queries?.Select(q => "-- " + q?.Title + "\n" + q?.Sql) ?? []);
            throw;
        }
    }
    static async Task<(List<TableResult> Tables, string Details)> ExecuteCore(QueryPlan plan, DataSchema schema, CancellationToken ct, Func<SqlConnection>? connectionFactory) {
        if (plan.Queries is null || plan.Queries.Count is < 1 or > 3) throw new InvalidOperationException("Ask for no more than three result tables at a time.");
        // Validate all statements before executing any of them. Only parser-generated text reaches SQL.
        PlannedQuery[] queries;
        using (RequestDiagnostics.Measure("sql_validation")) queries = ValidateQueries(plan, schema);
        using var connection = (connectionFactory ?? CreateConnection)();
        using (RequestDiagnostics.Measure("sql_connection_open")) await connection.OpenAsync(ct);
        var results = new List<TableResult>();
        var details = new StringBuilder("Interpretation: " + plan.Explanation + "\n\n");
        foreach (var query in queries) {
            using var queryTiming = RequestDiagnostics.Measure("sql_query_total", new { query.Title, query.Sql, query.ResultKind, query.KeepEmptyColumns });
            using var command = new SqlCommand(SessionOptions + "SET LOCK_TIMEOUT 5000; SET ROWCOUNT 5001;\n" + query.Sql, connection) { CommandTimeout = QueryTimeoutSeconds };
            var timer = System.Diagnostics.Stopwatch.StartNew();
            try {
            SqlDataReader reader;
            using (RequestDiagnostics.Measure("sql_execute_until_reader", new { query.Title }))
                reader = await command.ExecuteReaderAsync(ct);
            using var readerLifetime = reader;
            using var readTiming = RequestDiagnostics.Measure("sql_result_read", new { query.Title });
            var columns = new List<string>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++) {
                var name = reader.GetName(i).TrimStart('\uFEFF'); if (string.IsNullOrWhiteSpace(name)) name = "Value";
                var unique = name; int suffix = 2; while (!seen.Add(unique)) unique = name + " (" + suffix++ + ")";
                columns.Add(unique);
            }
            var rows = new List<List<string>>(); bool truncated = false; int characters = 0;
            while (await reader.ReadAsync(ct)) {
                if (rows.Count >= 5000 || characters >= 4_000_000) { truncated = true; break; }
                var row = new List<string>();
                for (int i = 0; i < reader.FieldCount; i++) {
                    var value = reader.IsDBNull(i) ? "" : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? "";
                    if (value.Length > 20000) { value = value[..20000] + " [cell shortened]"; truncated = true; }
                    row.Add(value); characters += value.Length;
                }
                rows.Add(row);
            }
            var rawTable = new TableResult(query.Title, @"ExampleWarehouse · ExampleCatalog snapshot", columns, rows, truncated);
            if (query.ResultKind == "detail") {
                var visible = Enumerable.Range(0, columns.Count).Where(i => !columns[i].StartsWith("Catalog_", StringComparison.Ordinal)).ToArray();
                rawTable = rawTable with { Columns = visible.Select(i => columns[i]).ToList(), Rows = rows.Select(r => visible.Select(i => r[i]).ToList()).ToList() };
            }
            var displayed = DisplayColumns(rawTable, query);
            results.Add(displayed);
            var hidden = rawTable.Columns.Except(displayed.Columns).ToArray();
            if (hidden.Length > 0) details.AppendLine("Hidden because empty in every returned row: " + string.Join(", ", hidden) + ".");
            RequestDiagnostics.Current?.Add("sql_result", timer.Elapsed.TotalMilliseconds, new { query.Title, Rows = rows.Count, Columns = columns.Count, VisibleColumns = displayed.Columns.Count, HiddenColumns = hidden, Truncated = truncated, Characters = characters });
            details.AppendLine(query.Title).AppendLine(query.Sql).AppendLine();
            } catch (Exception ex) when (ex is SqlException or OperationCanceledException) {
                RequestDiagnostics.Current?.Add("sql_failure", timer.Elapsed.TotalMilliseconds, new { query.Title, Type = ex.GetType().Name });
                ex.Data["SqlDiagnostic"] = FailureDetails(ex, command.CommandText, query.Title, timer.ElapsedMilliseconds);
                throw;
            }
        }
        details.AppendLine($"Limits: 5,000 displayed rows per table; 4 million characters per table; {QueryTimeoutSeconds}-second SQL timeout. Query counts/aggregates are calculated in SQL before the display limit. Numeric values use explicit conversions because these tables store text. Returned detail rows are not total counts. Limits do not eliminate sorting, DISTINCT, join or aggregate work.");
        return (results, details.ToString());
    }
}
