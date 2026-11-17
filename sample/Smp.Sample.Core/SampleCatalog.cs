using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Smp.Sample;

public sealed class SampleCatalog : ICatalogExecutor
{
    public const int RowLimit = 100;
    private static readonly Fixture Data = ReadFixture();

    public ResultTable Execute(ValidatedQuery query)
    {
        // Private, in-memory database per execution. No shared connection, company
        // configuration, persistent files, or path supplied by a visitor.
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        Seed(connection);
        using (var mode = connection.CreateCommand())
        {
            mode.CommandText = "PRAGMA query_only = ON;";
            mode.ExecuteNonQuery();
        }

        using var command = connection.CreateCommand();
        command.CommandText = query.ExecutableSql;
        using var reader = command.ExecuteReader();
        var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
        var rows = new List<IReadOnlyList<object?>>();
        var truncated = false;
        while (reader.Read())
        {
            if (rows.Count == RowLimit) { truncated = true; break; }
            rows.Add(Enumerable.Range(0, reader.FieldCount)
                .Select(i => reader.IsDBNull(i) ? null : reader.GetValue(i)).ToArray());
        }
        return new(query.Title, query.Sql, columns, rows, truncated, query.ExecutableSql);
    }

    private static void Seed(SqliteConnection connection)
    {
        using var create = connection.CreateCommand();
        create.CommandText = """
            CREATE TABLE Interchange (
                [Part Number] TEXT PRIMARY KEY, [Interchange Name] TEXT NOT NULL,
                Price REAL NOT NULL, Note TEXT);
            CREATE TABLE ApplicationViO (
                Part TEXT NOT NULL, Make TEXT NOT NULL, Model TEXT NOT NULL,
                Year INTEGER NOT NULL, VIO INTEGER NOT NULL,
                PRIMARY KEY (Part, Make, Model, Year));
            """;
        create.ExecuteNonQuery();
        using var transaction = connection.BeginTransaction();
        foreach (var row in Data.Interchange)
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO Interchange VALUES ($part, $reference, $price, $note)";
            insert.Parameters.AddWithValue("$part", row.PartNumber);
            insert.Parameters.AddWithValue("$reference", row.Reference);
            insert.Parameters.AddWithValue("$price", row.Price);
            insert.Parameters.AddWithValue("$note", (object?)row.Note ?? DBNull.Value);
            insert.ExecuteNonQuery();
        }
        foreach (var row in Data.Applications)
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO ApplicationViO VALUES ($part, $make, $model, $year, $vio)";
            insert.Parameters.AddWithValue("$part", row.Part);
            insert.Parameters.AddWithValue("$make", row.Make);
            insert.Parameters.AddWithValue("$model", row.Model);
            insert.Parameters.AddWithValue("$year", row.Year);
            insert.Parameters.AddWithValue("$vio", row.Vio);
            insert.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    private static Fixture ReadFixture()
    {
        using var source = typeof(SampleCatalog).Assembly.GetManifestResourceStream("Smp.Sample.Core.Data.catalog.json")
            ?? throw new InvalidOperationException("The fictional catalog fixture is missing.");
        return JsonSerializer.Deserialize<Fixture>(source, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("The fictional catalog fixture is invalid.");
    }

    private sealed record Fixture(InterchangeRow[] Interchange, ApplicationRow[] Applications);
    private sealed record InterchangeRow(string PartNumber, string Reference, double Price, string? Note);
    private sealed record ApplicationRow(string Part, string Make, string Model, int Year, int Vio);
}
