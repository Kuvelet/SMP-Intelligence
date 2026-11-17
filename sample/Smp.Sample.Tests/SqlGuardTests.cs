using Smp.Sample;

namespace Smp.Sample.Tests;

// The prototype's 75 parser cases, retained with fictional identifiers.
// These cover the broad T-SQL guard; the SQLite runner has a narrower subset.
public sealed class SqlGuardTests
{
    private static readonly DataSchema Schema = new([
        new("Interchange", "\uFEFFPart Number", "nvarchar"),
        new("Interchange", "Interchange Name", "nvarchar"),
        new("Interchange", "Price", "nvarchar"), new("Interchange", "Note", "nvarchar"),
        new("ApplicationViO", "Part", "nvarchar"), new("ApplicationViO", "Make", "nvarchar"),
        new("ApplicationViO", "Model", "nvarchar"), new("ApplicationViO", "Year", "nvarchar"),
        new("ApplicationViO", "VIO", "nvarchar")]);

    public static IEnumerable<object[]> SupportedQueries()
    {
        string[] valid = [
            "SELECT dbo.ApplicationViO.Part FROM dbo.ApplicationViO",
            "SELECT SampleCatalog.dbo.ApplicationViO.Part FROM SampleCatalog.dbo.ApplicationViO",
            "SELECT [dbo].[Interchange].[Part Number] FROM dbo.Interchange",
            "SELECT dbo.Interchange.[Part Number] FROM dbo.Interchange WHERE dbo.Interchange.[Part Number] IN (SELECT dbo.ApplicationViO.Part FROM dbo.ApplicationViO WHERE Make=N'Example Motors')",
            "SELECT DISTINCT [Year] FROM SampleCatalog.dbo.ApplicationViO WHERE Make=N'Sample Auto' AND Model=N'Model B'",
            "SELECT Part FROM ApplicationViO",
            "SELECT Part FROM [samplecatalog].[DBO].[applicationvio]",
            "SELECT DISTINCT [Part Number] FROM dbo.Interchange WHERE [Interchange Name] LIKE N'%Fictional Supplier%'",
            "SELECT COUNT_BIG(*) AS Total FROM dbo.ApplicationViO WHERE Part = N'DEMO-PART-101'",
            "SELECT TOP (10) Make, COUNT_BIG(DISTINCT Part) AS Parts FROM dbo.ApplicationViO GROUP BY Make HAVING COUNT_BIG(*) > 2 ORDER BY Parts DESC",
            "SELECT COUNT_BIG(*) AS Vehicles FROM (SELECT DISTINCT Make,Model,[Year] FROM dbo.ApplicationViO WHERE Part=N'DEMO-PART-101') v",
            "SELECT DISTINCT i.[Part Number] FROM dbo.Interchange i WHERE EXISTS (SELECT 1 FROM dbo.ApplicationViO a WHERE a.Part=i.[Part Number] AND a.Make=N'Example Motors')",
            "SELECT DISTINCT a.Part FROM dbo.ApplicationViO a WHERE NOT EXISTS (SELECT 1 FROM dbo.Interchange i WHERE i.[Part Number]=a.Part)",
            "SELECT DISTINCT Part FROM dbo.ApplicationViO WHERE TRY_CONVERT(int,[Year]) BETWEEN 2010 AND 2020",
            "SELECT SUM(TRY_CONVERT(decimal(28,4),NULLIF(REPLACE(VIO,',',''),''))) AS Total FROM dbo.ApplicationViO WHERE Part=N'DEMO-PART-101'",
            "SELECT CASE WHEN Note IS NULL OR LTRIM(RTRIM(Note))='' THEN 'Missing' ELSE 'Present' END AS Notes,COUNT_BIG(*) AS N FROM dbo.Interchange GROUP BY CASE WHEN Note IS NULL OR LTRIM(RTRIM(Note))='' THEN 'Missing' ELSE 'Present' END",
            "SELECT DISTINCT Part FROM dbo.ApplicationViO INTERSECT SELECT DISTINCT [Part Number] FROM dbo.Interchange",
            "SELECT i.[Part Number],a.Make FROM dbo.Interchange i INNER JOIN dbo.ApplicationViO a ON a.Part=i.[Part Number]",
            "SELECT Part, ROW_NUMBER() OVER (ORDER BY Part) AS RowNumber FROM dbo.ApplicationViO",
            "SELECT COUNT_BIG(*) AS N FROM dbo.Interchange WHERE [Interchange Name]=N'abc''; DROP TABLE x; --'",
            "SELECT * FROM dbo.ApplicationViO WHERE Part IN(N'DEMO-PART-101',N'DEMO-PART-102')",
            "SELECT TRY_CAST(Price AS decimal(18,2)) AS Price FROM dbo.Interchange",
            "SELECT COALESCE(Note,N'') AS Notes FROM dbo.Interchange"
        ];
        return valid.Select(sql => new object[] { sql });
    }

    public static IEnumerable<object[]> ProhibitedQueries()
    {
        string[] invalid = [
            "SELECT OtherDb.dbo.ApplicationViO.Part FROM dbo.ApplicationViO",
            "SELECT server.SampleCatalog.dbo.ApplicationViO.Part FROM dbo.ApplicationViO",
            "SELECT sales.ApplicationViO.Part FROM dbo.ApplicationViO",
            "SELECT dbo.Other.Part FROM dbo.ApplicationViO",
            "SELECT dbo.Interchange.Part FROM dbo.Interchange",
            "SELECT dbo.Interchange.[Part Number] FROM dbo.ApplicationViO",
            "SELECT dbo.ApplicationViO.Part FROM dbo.ApplicationViO a",
            "SELECT dbo.ApplicationViO.Part FROM dbo.Interchange WHERE EXISTS (SELECT 1 FROM dbo.ApplicationViO)",
            "SELECT Part FROM dbo.ApplicationViO WHERE EXISTS (SELECT dbo.ApplicationViO.Part FROM dbo.Interchange)",
            "SELECT dbo.ApplicationViO.Part FROM dbo.ApplicationViO INNER JOIN dbo.ApplicationViO ON 1=1",
            "SELECT OtherDb..ApplicationViO.Part FROM dbo.ApplicationViO",
            "SELECT * FROM SampleCatalog.dbo.Other", "SELECT * FROM SampleCatalog.sales.ApplicationViO",
            "SELECT * FROM SampleCatalog..ApplicationViO", "SELECT * FROM OtherDb.dbo.ApplicationViO",
            "SELECT * FROM dbo.ApplicationViO UNION SELECT * FROM OtherDb.dbo.ApplicationViO",
            "DELETE FROM dbo.Interchange","UPDATE dbo.Interchange SET Note='x'","DROP TABLE dbo.Interchange",
            "INSERT dbo.Interchange(Note) VALUES('x')","MERGE dbo.Interchange AS t USING dbo.Interchange AS s ON 1=1 WHEN MATCHED THEN DELETE;",
            "SELECT * FROM dbo.Interchange; DELETE FROM dbo.Interchange","SELECT * FROM dbo.Interchange\nGO\nSELECT * FROM dbo.ApplicationViO",
            "SELECT * INTO dbo.Copy FROM dbo.Interchange", "SELECT * INTO #temp FROM dbo.Interchange",
            "SELECT * FROM sys.tables","SELECT * FROM dbo.Other","SELECT * FROM OtherDb.dbo.Interchange",
            "SELECT * FROM server.SampleCatalog.dbo.Interchange","SELECT * FROM OPENQUERY(remote,'SELECT 1')",
            "SELECT * FROM OPENROWSET(BULK 'C:\\secret',SINGLE_CLOB) a","EXEC('SELECT * FROM dbo.Interchange')",
            "SELECT dbo.COUNT(Note) FROM dbo.Interchange","SELECT @@VERSION FROM dbo.Interchange",
            "SELECT SUSER_SNAME() FROM dbo.Interchange","SELECT NEXT VALUE FOR dbo.seq FROM dbo.Interchange",
            "SELECT @x = Note FROM dbo.Interchange", "WITH c AS (SELECT * FROM dbo.Interchange) SELECT * FROM c",
            "SELECT * FROM dbo.Interchange WITH (UPDLOCK)","SELECT * FROM dbo.Interchange OPTION(RECOMPILE)",
            "SELECT * FROM dbo.Interchange FOR XML AUTO","SELECT * FROM dbo.Interchange FOR JSON AUTO",
            "SELECT * FROM dbo.Interchange CROSS JOIN dbo.ApplicationViO",
            "SELECT * FROM dbo.Interchange CROSS APPLY dbo.DangerousFunction() f",
            "SELECT TOP (10) PERCENT * FROM dbo.Interchange","SELECT TOP(1) WITH TIES * FROM dbo.Interchange ORDER BY Note",
            "SELECT MadeUp FROM dbo.Interchange","SELECT DB_NAME() FROM dbo.Interchange",
            "WAITFOR DELAY '00:01'; SELECT * FROM dbo.Interchange",
            "SELECT CAST(Note AS xml).value('(/a)[1]','varchar(100)') FROM dbo.Interchange",
            "SELECT * FROM dbo.Interchange UNION SELECT * FROM dbo.Other",
            "SELECT * FROM dbo.Interchange WHERE EXISTS (SELECT 1 FROM sys.objects)"
        ];
        return invalid.Select(sql => new object[] { sql });
    }

    [Theory]
    [MemberData(nameof(SupportedQueries))]
    public void Accepts_supported_read_queries(string sql) => Assert.NotEmpty(SqlGuard.Validate(sql, Schema));

    [Theory]
    [MemberData(nameof(ProhibitedQueries))]
    public void Rejects_prohibited_operations(string sql) => Assert.Throws<InvalidOperationException>(() => SqlGuard.Validate(sql, Schema));

    [Fact]
    public void Preserves_local_column_binding_and_maps_imported_BOM_name()
    {
        var sql = SqlGuard.Validate("SELECT SampleCatalog.dbo.Interchange.[Part Number] FROM dbo.Interchange", Schema);
        Assert.Contains("[Interchange].[\uFEFFPart Number]", sql);
        Assert.DoesNotContain("SampleCatalog", sql);
    }

    [Fact]
    public void Normalizes_trim_for_legacy_SQL_Server()
        => Assert.Contains("LTRIM(RTRIM(", SqlGuard.Validate("SELECT TRIM(Model) AS Model FROM dbo.ApplicationViO", Schema));

    [Theory]
    [InlineData("")]
    [InlineData("SELECT")]
    public void Rejects_empty_or_malformed_SQL(string sql)
        => Assert.Throws<InvalidOperationException>(() => SqlGuard.Validate(sql, Schema));

    [Fact]
    public void Rejects_oversized_SQL()
        => Assert.Throws<InvalidOperationException>(() => SqlGuard.Validate(new string(' ', 16001), Schema));
}
