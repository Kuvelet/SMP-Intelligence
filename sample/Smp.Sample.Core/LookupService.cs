namespace Smp.Sample;

public sealed class LookupService(QueryPipeline pipeline)
{
    public LookupReply Ask(LookupRequest request)
    {
        var plan = ExamplePlanner.Plan(request);
        if (plan.Queries.Count == 0) return new("clarify", plan.Message, [], []);
        var tables = pipeline.Run(plan);
        var parts = tables.SelectMany(table =>
        {
            var partColumn = table.Columns.ToList().FindIndex(c => c is "Part Number" or "Part");
            return partColumn < 0 ? [] : table.Rows.Select(row => row[partColumn]?.ToString() ?? "");
        }).Where(p => p.Length > 0).Distinct().ToArray();
        return new("ok", tables.All(t => t.Rows.Count == 0)
            ? "No matching records in the fictional sample catalog. This does not establish real-world fitment."
            : plan.Message, tables, parts);
    }
}
