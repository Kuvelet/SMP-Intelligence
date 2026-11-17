using System.Data;
namespace SMPClient;
public static class TableFilter {
    public static void Verify() {
        var table = new DataTable(); table.Columns.Add("A]B\\C"); table.Rows.Add("O'Brien [100%]*"); table.Rows.Add("Other");
        foreach (var term in new[] { "O'Brien", "[", "]", "%", "*", "100" }) {
            table.DefaultView.RowFilter = Expression(table, term);
            if (table.DefaultView.Count != 1) throw new Exception("Literal table search failed: " + term);
        }
        table.DefaultView.RowFilter = Expression(table, "");
        if (table.DefaultView.Count != 2) throw new Exception("Search clear failed");
    }
    public static string Expression(DataTable table, string text) {
        if (string.IsNullOrEmpty(text)) return "";
        var escaped = string.Concat(text.Select(c => c switch { '\'' => "''", '[' => "[[]", ']' => "[]]", '%' => "[%]", '*' => "[*]", _ => c.ToString() }));
        return string.Join(" OR ", table.Columns.Cast<DataColumn>().Select(c => "[" + c.ColumnName.Replace("\\", "\\\\").Replace("]", "\\]") + "] LIKE '%" + escaped + "%'"));
    }
}
