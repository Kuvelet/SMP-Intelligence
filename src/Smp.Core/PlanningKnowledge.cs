using System.IO;
using System.Text.Json;

public static class PlanningKnowledge {
    public const string Version = "public-review.1";
    public static string Build() => Load(Path.Combine(AppContext.BaseDirectory, "SQL-Instructions.txt"));
    internal static string Load(string path) {
        if (!File.Exists(path)) throw new InvalidOperationException("SQL-Instructions.txt is missing. Restore the approved instructions beside the backend executable.");
        var text = File.ReadAllText(path);
        if (text.Length is < 100 or > 131072 || !text.StartsWith($"SMP SQL planner. Instruction version: {Version}", StringComparison.Ordinal))
            throw new InvalidOperationException("SQL-Instructions.txt is empty, too large, or has an incompatible version. Restore the approved instructions.");
        return text;
    }
    public static string Input(string question, IReadOnlyList<string>? context, DataSchema schema) =>
        JsonSerializer.Serialize(new { previous_questions = context?.TakeLast(3).ToArray() ?? [], current_question = question, current_schema = schema.Describe() });
}
