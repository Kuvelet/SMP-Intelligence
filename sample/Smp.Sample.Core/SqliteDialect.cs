using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Smp.Sample;

// The original application executes T-SQL on SQL Server. This sample executes a
// deliberately smaller SQL subset on SQLite to remove database setup for reviewers.
// Only validated AST nodes are adapted; never rewrite arbitrary SQL with regex.
internal static class SqliteDialect
{
    private static readonly HashSet<string> AllowedNodes = new(StringComparer.Ordinal)
    {
        "TSqlScript", "TSqlBatch", "SelectStatement", "QuerySpecification",
        "SelectScalarExpression", "SelectStarExpression", "Identifier", "IdentifierOrValueExpression",
        "MultiPartIdentifier", "SchemaObjectName", "NamedTableReference", "FromClause", "WhereClause",
        "ColumnReferenceExpression", "StringLiteral", "IntegerLiteral", "NumericLiteral", "NullLiteral",
        "BooleanComparisonExpression", "BooleanBinaryExpression", "BooleanParenthesisExpression",
        "BooleanNotExpression", "BooleanIsNullExpression", "LikePredicate", "InPredicate",
        "BooleanTernaryExpression", "QualifiedJoin", "GroupByClause", "ExpressionGroupingSpecification",
        "HavingClause", "OrderByClause", "ExpressionWithSortOrder", "ParenthesisExpression",
        "FunctionCall", "CoalesceExpression", "NullIfExpression"
    };

    private static readonly HashSet<string> Functions = new(StringComparer.OrdinalIgnoreCase)
        { "COUNT", "COUNT_BIG", "SUM", "AVG", "MIN", "MAX", "LOWER", "UPPER", "LTRIM", "RTRIM" };

    public static string Prepare(string validatedSql)
    {
        var tree = new TSql160Parser(true).Parse(new StringReader(validatedSql), out var errors);
        if (errors.Count != 0) throw Rejected("The validated query could not be read.");
        var nodes = SqlGuard.Walk(tree).ToArray();
        if (nodes.OfType<QuerySpecification>().Count() != 1 || nodes.OfType<NamedTableReference>().Count() > 2)
            throw Rejected("The local sample supports one SELECT over at most two table references, without subqueries.");

        foreach (var node in nodes)
        {
            if (!AllowedNodes.Contains(node.GetType().Name))
                throw Rejected($"The local SQLite sample does not support {node.GetType().Name}. See sample/README.md for the supported subset.");
            if (node is QualifiedJoin join && join.QualifiedJoinType is not (QualifiedJoinType.Inner or QualifiedJoinType.LeftOuter))
                throw Rejected("The sample supports INNER JOIN and LEFT JOIN.");
            if (node is FunctionCall function)
            {
                if (!Functions.Contains(function.FunctionName.Value))
                    throw Rejected($"The local sample does not support the function {function.FunctionName.Value}.");
                if (function.FunctionName.Value.Equals("COUNT_BIG", StringComparison.OrdinalIgnoreCase))
                    function.FunctionName.Value = "COUNT";
            }
            // These are known local names: SqlGuard has already rejected remote targets.
            if (node is NamedTableReference table)
            {
                var name = table.SchemaObject.BaseIdentifier;
                table.SchemaObject.Identifiers.Clear();
                table.SchemaObject.Identifiers.Add(name);
            }
            if (node is StringLiteral literal) literal.IsNational = false;
        }

        new Sql160ScriptGenerator().GenerateScript(tree, out var sql);
        return sql;
    }

    private static QueryRejectedException Rejected(string reason) => new("Sample SQL compatibility", reason);
}
