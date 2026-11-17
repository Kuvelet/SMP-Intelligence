using Smp.Sample;

namespace Smp.Sample.Tests;

public sealed class PipelineTests
{
    private static LookupService Service() => new(new QueryPipeline(new SampleCatalog()));

    [Fact]
    public void Reference_lookup_returns_two_parts_and_their_different_fitment_notes()
    {
        var reply = Service().Ask(new("Which parts match DEMO-OE-1001?"));
        Assert.Equal("ok", reply.Status);
        Assert.Equal(new[] { "DEMO-PART-101", "DEMO-PART-102" }, reply.Parts);
        Assert.Equal(2, reply.Tables[0].Rows.Count);
        Assert.Contains("heavy-duty", reply.Tables[0].Rows[1][3]!.ToString());
    }

    [Fact]
    public void Follow_up_returns_five_applications_for_the_previous_two_parts()
    {
        var service = Service();
        var first = service.Ask(new("DEMO-OE-1001"));
        var followUp = service.Ask(new("Which vehicles do these parts fit?", first.Parts.ToArray()));
        Assert.Equal(5, followUp.Tables[0].Rows.Count);
        Assert.Equal(2, followUp.Parts.Count);
        Assert.Contains(followUp.Tables[0].Rows, row => Equals(row[2], "Model A HD"));
    }

    [Fact]
    public void Counts_distinct_parts_rather_than_vehicle_year_rows()
    {
        var result = Service().Ask(new("Count distinct parts by make"));
        var counts = result.Tables[0].Rows.ToDictionary(row => (string)row[0]!, row => (long)row[1]!);
        Assert.Equal(3L, counts["Example Motors"]);
        Assert.Equal(3L, counts["Sample Auto"]);
        Assert.Equal(2L, counts["Fictional Vehicles"]);
    }

    [Theory]
    [InlineData("DEMO-OE-9999")]
    [InlineData("DEMO-PART-108")]
    public void Missing_reference_or_fitment_returns_an_empty_result_with_headers(string question)
    {
        var reply = Service().Ask(new(question));
        Assert.Empty(reply.Tables[0].Rows);
        Assert.NotEmpty(reply.Tables[0].Columns);
        Assert.Contains("No matching records", reply.Message);
    }

    [Theory]
    [InlineData("What is the weather?")]
    [InlineData("Which vehicles do these fit?")]
    [InlineData("")]
    public void Unsupported_or_ambiguous_questions_do_not_execute_SQL(string question)
    {
        var executor = new SpyExecutor();
        var result = new LookupService(new QueryPipeline(executor)).Ask(new(question));
        Assert.Equal("clarify", result.Status);
        Assert.Empty(result.Tables);
        Assert.Equal(0, executor.Calls);
    }

    [Fact]
    public void Forged_follow_up_context_cannot_become_SQL()
    {
        var executor = new SpyExecutor();
        var result = new LookupService(new QueryPipeline(executor)).Ask(new("Which vehicles fit?",
            ["DEMO-PART-101'); DROP TABLE Interchange;--"]));
        Assert.Equal("clarify", result.Status);
        Assert.Equal(0, executor.Calls);
    }

    [Theory]
    [InlineData("Which parts match DEMO-OE-1001 under $40?")]
    [InlineData("Under $40, which parts match DEMO-OE-1001?")]
    [InlineData("Which parts match DEMO-OE-1001 sorted by price?")]
    [InlineData("Which parts match DEMO-OE-1001 except DEMO-PART-102?")]
    [InlineData("Do not look up DEMO-OE-1001")]
    [InlineData("DEMO-OE-1001 and DEMO-OE-1002")]
    [InlineData("DEMO-OE-1001 and DEMO-PART-101")]
    [InlineData("DEMO-OE-1001\nunder $40")]
    [InlineData("DEMO-OE-1001xyz")]
    [InlineData("Show applications for DEMO-PART-101 from 2020 onward")]
    [InlineData("DEMO-PART-101 for Example Motors only")]
    [InlineData("DEMO-PART-101 and invalid-part")]
    [InlineData("Which vehicles do these parts fit in Canada?")]
    [InlineData("Which vehicles do these parts fit? Only show the newest year.")]
    [InlineData("What applications does it fit with VIO above 1000?")]
    [InlineData("Count distinct parts by make for Example Motors")]
    [InlineData("Which parts match DEMO-OE-1001? Count distinct parts by make")]
    public void Extra_conditions_or_partial_matches_request_clarification_without_executing(string question)
    {
        var executor = new SpyExecutor();
        var result = new LookupService(new QueryPipeline(executor)).Ask(new(question, ["DEMO-PART-101"]));
        Assert.Equal("clarify", result.Status);
        Assert.Contains("whole question", result.Message);
        Assert.Empty(result.Tables);
        Assert.Empty(result.Parts);
        Assert.Equal(0, executor.Calls);
    }

    [Theory]
    [InlineData("  which parts match demo-oe-1001?  ", 2)]
    [InlineData("DEMO-PART-101", 3)]
    [InlineData("Show applications for DEMO-PART-101?", 3)]
    [InlineData("DEMO-PART-101, DEMO-PART-102", 5)]
    [InlineData("Show applications for DEMO-PART-101 and DEMO-PART-102", 5)]
    [InlineData("Which vehicles do these parts fit?", 5)]
    [InlineData("Which vehicles do these fit?", 5)]
    [InlineData("Which vehicles fit?", 5)]
    [InlineData("what applications does it fit?", 5)]
    [InlineData("Count distinct parts by make?", 3)]
    public void Complete_supported_questions_still_return_expected_records(string question, int rows)
    {
        var reply = Service().Ask(new(question, ["DEMO-PART-101", "DEMO-PART-102"]));
        Assert.Equal("ok", reply.Status);
        Assert.Equal(rows, Assert.Single(reply.Tables).Rows.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Follow_up_context_is_rejected_instead_of_partially_used(bool tooMany)
    {
        var parts = tooMany
            ? Enumerable.Range(101, 9).Select(n => $"DEMO-PART-{n}").ToArray()
            : new[] { "DEMO-PART-101", "invalid-part" };
        var executor = new SpyExecutor();
        var reply = new LookupService(new QueryPipeline(executor)).Ask(new("Which vehicles fit?", parts));
        Assert.Equal("clarify", reply.Status);
        Assert.Empty(reply.Tables);
        Assert.Equal(0, executor.Calls);
    }

    [Theory]
    [InlineData("DELETE FROM dbo.Interchange")]
    [InlineData("SELECT TOP (1) Part FROM dbo.ApplicationViO")]
    public void A_rejected_second_query_prevents_the_entire_plan_from_executing(string second)
    {
        var executor = new SpyExecutor();
        var pipeline = new QueryPipeline(executor);
        Assert.Throws<QueryRejectedException>(() => pipeline.Run(new("Two results", [
            new("First", "SELECT Part FROM dbo.ApplicationViO"), new("Second", second)])));
        Assert.Equal(0, executor.Calls);
    }

    [Theory]
    [InlineData("SELECT TOP (2) * FROM dbo.Interchange")]
    [InlineData("SELECT ROW_NUMBER() OVER (ORDER BY Part) AS N FROM dbo.ApplicationViO")]
    [InlineData("SELECT Part FROM dbo.ApplicationViO WHERE EXISTS (SELECT 1 FROM dbo.Interchange)")]
    [InlineData("SELECT * FROM dbo.Interchange a INNER JOIN dbo.Interchange b ON 1=1 INNER JOIN dbo.Interchange c ON 1=1")]
    public void The_SQLite_adapter_rejects_features_outside_its_smaller_subset(string sql)
    {
        var error = Assert.Throws<QueryRejectedException>(() => new QueryPipeline(new SampleCatalog()).Run(new("Test", [new("Test", sql)])));
        Assert.Equal("Sample SQL compatibility", error.Stage);
    }

    [Fact]
    public void Accepted_join_runs_against_real_sample_records()
    {
        var result = new QueryPipeline(new SampleCatalog()).Run(new("Join", [new("Join", """
            SELECT i.[Part Number], a.Make FROM dbo.Interchange i
            INNER JOIN dbo.ApplicationViO a ON a.Part = i.[Part Number]
            WHERE i.[Interchange Name] = N'DEMO-OE-1001' ORDER BY i.[Part Number], a.Make
            """)]))[0];
        Assert.Equal(5, result.Rows.Count);
    }

    [Fact]
    public void A_quote_inside_a_literal_remains_data()
    {
        var table = new QueryPipeline(new SampleCatalog()).Run(new("Literal", [new("Literal", """
            SELECT COUNT_BIG(*) AS Total FROM dbo.Interchange
            WHERE Note = N'abc''; DROP TABLE Interchange; --'
            """)]))[0];
        Assert.Equal(0L, table.Rows[0][0]);
        Assert.Equal(2, Service().Ask(new("DEMO-OE-1001")).Tables[0].Rows.Count);
    }

    [Fact]
    public void Results_are_capped_and_report_truncation()
    {
        var result = new QueryPipeline(new SampleCatalog()).Run(new("Cap", [new("Cap", """
            SELECT a.Part, b.Part FROM dbo.ApplicationViO a
            INNER JOIN dbo.ApplicationViO b ON 1=1 ORDER BY a.Part, b.Part
            """)]))[0];
        Assert.Equal(SampleCatalog.RowLimit, result.Rows.Count);
        Assert.True(result.Truncated);
    }

    [Fact]
    public async Task Concurrent_lookups_do_not_share_follow_up_state_or_connections()
    {
        var service = Service();
        var replies = await Task.WhenAll(Enumerable.Range(0, 12).Select(i => Task.Run(() =>
            service.Ask(new(i % 2 == 0 ? "DEMO-PART-101" : "DEMO-PART-105")))));
        for (var i = 0; i < replies.Length; i++)
        {
            Assert.Equal(i % 2 == 0 ? 3 : 1, replies[i].Tables[0].Rows.Count);
            Assert.Equal(i % 2 == 0 ? "DEMO-PART-101" : "DEMO-PART-105", Assert.Single(replies[i].Parts));
        }
    }

    private sealed class SpyExecutor : ICatalogExecutor
    {
        public int Calls { get; private set; }
        public ResultTable Execute(ValidatedQuery query)
        {
            Calls++;
            return new(query.Title, query.Sql, [], [], false, "");
        }
    }
}
