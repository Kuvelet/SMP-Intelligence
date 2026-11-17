namespace Smp.Sample;

public sealed class QueryPipeline(ICatalogExecutor catalog)
{
    public IReadOnlyList<ResultTable> Run(QueryPlan plan)
    {
        if (plan.Queries.Count is < 1 or > 3)
            throw new QueryRejectedException("Plan validation", "Use between one and three queries per request.");

        // Materialize the entire validated plan BEFORE the first database call.
        // A rejected second query must also prevent execution of the first query.
        var validated = plan.Queries.Select(query =>
        {
            string normalized;
            try { normalized = SqlGuard.Validate(query.Sql, DataSchema.Catalog); }
            catch (InvalidOperationException error)
            {
                throw new QueryRejectedException("SQL validation", error.Message);
            }

            return new ValidatedQuery(query.Title, normalized, SqliteDialect.Prepare(normalized));
        }).ToArray();

        return validated.Select(catalog.Execute).ToArray();
    }
}
