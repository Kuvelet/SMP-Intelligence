namespace SMP;

public record TableResult(string Title, string Source, List<string> Columns, List<List<string>> Rows, bool Truncated);
public record TrainingSource(string Id, string Document, int Page, string Image, string Pdf, string Quote);
public record ApiRequestTrace(string Purpose, DateTimeOffset SubmittedUtc, string Endpoint, string RequestJson, int? HttpStatus = null, string? RequestText = null, string? ResponseBody = null);
public record Answer(string Summary, string Part, List<TableResult> Tables, int InputTokens, int OutputTokens, string QueryDetails = "", List<TrainingSource>? Sources = null, string? GeneratedSql = null, List<ApiRequestTrace>? OpenAiRequests = null);
public sealed class Settings {
    public string Model { get; set; } = "";
    public int DailyLimit { get; set; } = 100;
}
