namespace Smp.Sample;

public sealed record DataColumnInfo(string Table, string Name, string Type)
{
    public string DisplayName => Name.TrimStart('\uFEFF');
}

public sealed record DataSchema(List<DataColumnInfo> Columns)
{
    public static DataSchema Catalog => new([
        new("Interchange", "Part Number", "text"),
        new("Interchange", "Interchange Name", "text"),
        new("Interchange", "Price", "real"),
        new("Interchange", "Note", "text"),
        new("ApplicationViO", "Part", "text"),
        new("ApplicationViO", "Make", "text"),
        new("ApplicationViO", "Model", "text"),
        new("ApplicationViO", "Year", "integer"),
        new("ApplicationViO", "VIO", "integer")
    ]);
}

public sealed record PlannedQuery(string Title, string Sql);
public sealed record QueryPlan(string Message, IReadOnlyList<PlannedQuery> Queries);
public sealed record ResultTable(string Title, string Sql, IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<object?>> Rows, bool Truncated, string ExecutedSql);
public sealed record LookupRequest(string? Question, string[]? PreviousParts = null);
public sealed record LookupReply(string Status, string Message, IReadOnlyList<ResultTable> Tables,
    IReadOnlyList<string> Parts, string Planner = "Offline example planner");

// Only the validation pipeline can create a query the database will accept.
public sealed class ValidatedQuery
{
    internal ValidatedQuery(string title, string sql, string executableSql)
        => (Title, Sql, ExecutableSql) = (title, sql, executableSql);

    public string Title { get; }
    public string Sql { get; }
    internal string ExecutableSql { get; }
}

public sealed class QueryRejectedException(string stage, string message) : Exception(message)
{
    public string Stage { get; } = stage;
}

public interface ICatalogExecutor
{
    ResultTable Execute(ValidatedQuery query);
}
