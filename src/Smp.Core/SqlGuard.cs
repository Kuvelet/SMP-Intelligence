using System.Collections;
using System.IO;
using System.Reflection;
using Microsoft.SqlServer.TransactSql.ScriptDom;

// Deny-by-default syntax allowlist. Generated SQL is never executed directly.
public static class SqlGuard {
    static readonly HashSet<string> Nodes = new(StringComparer.Ordinal) {
        "TSqlScript","TSqlBatch","SelectStatement","QuerySpecification","QueryParenthesisExpression","BinaryQueryExpression",
        "SelectScalarExpression","SelectStarExpression","Identifier","IdentifierOrValueExpression","MultiPartIdentifier","SchemaObjectName",
        "NamedTableReference","QueryDerivedTable","FromClause","WhereClause","ColumnReferenceExpression",
        "StringLiteral","IntegerLiteral","NumericLiteral","RealLiteral","NullLiteral",
        "BooleanComparisonExpression","BooleanBinaryExpression","BooleanParenthesisExpression","BooleanNotExpression","BooleanIsNullExpression",
        "LikePredicate","InPredicate","BooleanTernaryExpression","ExistsPredicate","ScalarSubquery",
        "QualifiedJoin","JoinParenthesisTableReference","GroupByClause","ExpressionGroupingSpecification","HavingClause",
        "OrderByClause","ExpressionWithSortOrder","TopRowFilter","ParenthesisExpression","FunctionCall",
        "CastCall","TryCastCall","ConvertCall","TryConvertCall","SqlDataTypeReference","BinaryExpression","UnaryExpression",
        "CoalesceExpression","NullIfExpression","SearchedCaseExpression","SearchedWhenClause","SimpleCaseExpression","SimpleWhenClause",
        "OverClause"
    };
    static readonly HashSet<string> Functions = new(StringComparer.OrdinalIgnoreCase) {
        "COUNT","COUNT_BIG","SUM","AVG","MIN","MAX","ISNULL","LTRIM","RTRIM","TRIM","LOWER","UPPER","REPLACE",
        "ROUND","ABS","CEILING","FLOOR","LEN","DATALENGTH","CONCAT","SUBSTRING","LEFT","RIGHT","ROW_NUMBER","RANK","DENSE_RANK"
    };
    static readonly HashSet<string> ForbiddenProperties = new(StringComparer.Ordinal) {
        "Into","On","ForClause","OptimizerHints","WithCtesAndXmlNamespaces","ComputeClauses","TableHints","TemporalClause"
    };
    public static string Validate(string sql, DataSchema schema) {
        if (string.IsNullOrWhiteSpace(sql) || sql.Length > 16000) throw new InvalidOperationException("The proposed query is empty or too long.");
        var parser = new TSql160Parser(true);
        var tree = parser.Parse(new StringReader(sql), out var errors);
        if (errors.Count != 0) throw new InvalidOperationException("The proposed query is not valid SQL Server SELECT syntax.");
        if (tree is not TSqlScript script || script.Batches.Count != 1 || script.Batches[0].Statements.Count != 1 ||
            script.Batches[0].Statements[0] is not SelectStatement) throw new InvalidOperationException("Only one read-only SELECT statement is permitted per result.");
        var nodes = Walk(tree).ToArray();
        if (nodes.Length > 1500) throw new InvalidOperationException("The proposed query is too complex.");
        var tables = nodes.OfType<NamedTableReference>().ToArray();
        if (tables.Length == 0 || tables.Length > 8) throw new InvalidOperationException("A query must read the approved tables with at most eight table references.");
        foreach (var table in tables) {
            var names = table.SchemaObject.Identifiers;
            // Resolve only equivalent spellings of our two fixed local tables.
            // Never strip an arbitrary database/server/schema qualifier.
            var name = names.LastOrDefault()?.Value;
            var approved = new[] { "Interchange", "ApplicationViO" }.FirstOrDefault(x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
            bool local = names.Count == 1 ||
                (names.Count == 2 && names[0].Value.Equals("dbo", StringComparison.OrdinalIgnoreCase)) ||
                (names.Count == 3 && names[0].Value.Equals("ExampleCatalog", StringComparison.OrdinalIgnoreCase) && names[1].Value.Equals("dbo", StringComparison.OrdinalIgnoreCase));
            if (!local || approved is null || !schema.Columns.Any(c => c.Table.Equals(approved, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("The generated query used an unsupported table reference: " + string.Join(".", names.Select(n => n.Value)) + ". Only dbo.Interchange and dbo.ApplicationViO may be queried.");
            names.Clear();
            names.Add(new Identifier { Value = "dbo", QuoteType = QuoteType.SquareBracket });
            names.Add(new Identifier { Value = approved, QuoteType = QuoteType.SquareBracket });
            if (table.TableHints.Count != 0) throw new InvalidOperationException("Table hints are not permitted.");
        }
        var aliases = nodes.OfType<SelectScalarExpression>().Where(x => x.ColumnName is not null).Select(x => x.ColumnName!.Value)
            .Concat(nodes.OfType<QueryDerivedTable>().SelectMany(x => x.Columns.Select(c => c.Value))).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var node in nodes) {
            if (!Nodes.Contains(node.GetType().Name)) throw new InvalidOperationException("Unsupported query feature: " + node.GetType().Name);
            foreach (var property in node.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
                if (ForbiddenProperties.Contains(property.Name) && Populated(property.GetValue(node)))
                    throw new InvalidOperationException("Query feature not permitted: " + property.Name);
            }
            if (node is Identifier identifierNode) {
                var imported = schema.Columns.FirstOrDefault(c => c.Name != c.DisplayName &&
                    c.DisplayName.Equals(identifierNode.Value, StringComparison.OrdinalIgnoreCase));
                if (imported is not null) { identifierNode.Value = imported.Name; identifierNode.QuoteType = QuoteType.SquareBracket; }
            }
            if (node is FunctionCall function && (function.CallTarget is not null || !Functions.Contains(function.FunctionName.Value)))
                throw new InvalidOperationException("This query uses a function outside the allowed calculations.");
            // This SQL Server predates TRIM. Preserve its one-argument semantics.
            if (node is FunctionCall trim && trim.FunctionName.Value.Equals("TRIM", StringComparison.OrdinalIgnoreCase)) {
                if (trim.Parameters.Count != 1) throw new InvalidOperationException("Only single-value trimming is supported.");
                var inner = new FunctionCall { FunctionName = new Identifier { Value = "RTRIM" } };
                inner.Parameters.Add(trim.Parameters[0]);
                trim.FunctionName.Value = "LTRIM";
                trim.Parameters.Clear(); trim.Parameters.Add(inner);
            }
            if (node is TopRowFilter top && (top.Percent || top.WithTies))
                throw new InvalidOperationException("TOP PERCENT and WITH TIES are not supported.");
            if (node is ColumnReferenceExpression column && column.MultiPartIdentifier is { } identifier) {
                if (identifier.Identifiers.Count > 2) NormalizeLocalColumn(identifier, column, nodes, tables, schema);
                var last = identifier.Identifiers.LastOrDefault();
                if (last is null) continue;
                var match = schema.Columns.FirstOrDefault(c => c.Name.Equals(last.Value, StringComparison.OrdinalIgnoreCase) ||
                    c.DisplayName.Equals(last.Value, StringComparison.OrdinalIgnoreCase));
                if (match is null && !aliases.Contains(last.Value)) throw new InvalidOperationException("Unknown column in proposed query: " + last.Value);
                // Normalize the imported BOM-prefixed columns without exposing invisible names to users.
                if (match is not null) { last.Value = match.Name; last.QuoteType = QuoteType.SquareBracket; }
            }
        }
        // Resolve approved logical names only after validation. No user-controlled physical target.
        if (schema.Warehouse) {
            foreach (var table in tables) {
                var name = table.SchemaObject.Identifiers.Last();
                // Preserve existing column/star bindings in each SQL scope, including correlated subqueries.
                table.Alias ??= new Identifier { Value = name.Value, QuoteType = QuoteType.SquareBracket };
                name.Value += "_ExampleCatalog";
            }
        }
        new Sql160ScriptGenerator().GenerateScript(script, out var normalized);
        return normalized;
    }
    static void NormalizeLocalColumn(MultiPartIdentifier identifier, ColumnReferenceExpression column,
        TSqlFragment[] nodes, NamedTableReference[] tables, DataSchema schema) {
        var names = identifier.Identifiers.Select(i => i.Value).ToArray();
        static bool Same(string a, string b) => a.Equals(b, StringComparison.OrdinalIgnoreCase);
        // Only explicit dbo.Table.Column or ExampleCatalog.dbo.Table.Column, never server names.
        var offset = names.Length == 4 && Same(names[0], "ExampleCatalog") ? 1 : 0;
        if (names.Length - offset != 3 || !Same(names[offset], "dbo"))
            throw new InvalidOperationException("Only local dbo column qualification is permitted; other databases and servers are blocked.");
        var tableName = names[offset + 1];
        if (!Same(tableName, "ApplicationViO") && !Same(tableName, "Interchange"))
            throw new InvalidOperationException("Column qualification must name an approved table.");
        QuerySpecification? Scope(TSqlFragment fragment) => nodes.OfType<QuerySpecification>()
            .Where(q => q.StartOffset <= fragment.StartOffset &&
                q.StartOffset + q.FragmentLength >= fragment.StartOffset + fragment.FragmentLength)
            .OrderBy(q => q.FragmentLength).FirstOrDefault();
        var scope = Scope(column);
        // Never guess a binding, rewrite through an alias, or capture a table in a nested/outer query.
        var bindings = tables.Where(t => ReferenceEquals(Scope(t), scope) &&
            Same(t.SchemaObject.BaseIdentifier.Value, tableName)).ToArray();
        if (scope is null || bindings.Length != 1 || bindings[0].Alias is not null)
            throw new InvalidOperationException("Use an explicit table alias for this column; a unique local unaliased binding was not found.");
        var match = schema.Columns.FirstOrDefault(c => Same(c.Table, tableName) &&
            (Same(c.Name, names[^1]) || Same(c.DisplayName, names[^1])));
        if (match is null) throw new InvalidOperationException("Unknown column on qualified table: " + string.Join(".", names));
        identifier.Identifiers.Clear();
        identifier.Identifiers.Add(new Identifier { Value = bindings[0].SchemaObject.BaseIdentifier.Value, QuoteType = QuoteType.SquareBracket });
        identifier.Identifiers.Add(new Identifier { Value = match.Name, QuoteType = QuoteType.SquareBracket });
        RequestDiagnostics.Current?.Add("sql_reference_normalized", 0, new { Original = string.Join(".", names), Table = tableName, Column = match.Name });
    }
    static bool Populated(object? value) => value switch {
        null => false, bool flag => flag, string text => text.Length > 0,
        IEnumerable items => items.Cast<object>().Any(), _ => true
    };
    static IEnumerable<TSqlFragment> Walk(TSqlFragment root) {
        var pending = new Stack<(TSqlFragment Node, int Depth)>(); pending.Push((root, 0));
        var seen = new HashSet<TSqlFragment>(ReferenceEqualityComparer.Instance);
        while (pending.Count != 0) {
            var (node, depth) = pending.Pop();
            if (!seen.Add(node)) continue;
            if (depth > 60 || seen.Count > 1500) throw new InvalidOperationException("The query is too complex.");
            yield return node;
            foreach (var property in node.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
                if (property.Name == "ScriptTokenStream" || property.GetIndexParameters().Length != 0) continue;
                if (typeof(TSqlFragment).IsAssignableFrom(property.PropertyType) && property.GetValue(node) is TSqlFragment child)
                    pending.Push((child, depth + 1));
                else if (property.PropertyType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(property.PropertyType) &&
                    property.GetValue(node) is IEnumerable children)
                    foreach (var item in children) if (item is TSqlFragment fragment) pending.Push((fragment, depth + 1));
            }
        }
    }
}
