using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SMP;

namespace SMPClient;
public partial class MainWindow {
    void AddTransparency(Answer answer, string question, MessageView message, bool failed = false) {
        var sql = "QUESTION\n" + question + "\n\n";
        sql += !string.IsNullOrWhiteSpace(answer.GeneratedSql)
            ? "SQL WRITTEN BY OPENAI\n" + answer.GeneratedSql + "\n\nEXECUTION AND SUPPORTING DETAILS\n" + answer.QueryDetails
            : failed ? "This request failed. No successful SQL execution is recorded in this answer.\n"
            : !string.IsNullOrWhiteSpace(answer.QueryDetails) && answer.Tables.Count > 0
                ? "DETAILS FROM AN EARLIER BACKEND\n" + answer.QueryDetails
                : "No SQL was generated for this answer.\n\n" + answer.QueryDetails;
        var sent = FormatRequests(question, answer.OpenAiRequests);
        var received = FormatResponses(answer.OpenAiRequests);
        // Update tabs immediately; each message's buttons can restore its own contents later.
        DetailsText.Text = sql; OpenAiText.Text = sent; OpenAiResponseText.Text = received;
        Add("SQL Query", () => { DetailsText.Text = sql; WorkspaceTabs.SelectedItem = DetailsTab; });
        Add("Sent to OpenAI", () => { OpenAiText.Text = sent; WorkspaceTabs.SelectedItem = OpenAiTab; });
        Add("Received from OpenAI", () => { OpenAiResponseText.Text = received; WorkspaceTabs.SelectedItem = OpenAiResponseTab; });
        void Add(string label, Action action) {
            var button = new Button { Content = label, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(12,7,12,7), FontSize = 12, Margin = new Thickness(0,10,0,0) };
            button.Click += (_,_) => action(); message.Actions.Children.Add(button);
        }
    }
    internal static string FormatRequests(string question, List<ApiRequestTrace>? requests) {
        if (requests is null) return "Request contents were not recorded for this response.";
        if (requests.Count == 0) return "No OpenAI request was submitted for this response.";
        return string.Join("\r\n\r\n", requests.Select(r => r.RequestText
            ?? "Request headers were not recorded for this older response. Saved JSON body:\r\n\r\n" + r.RequestJson));
    }
    internal static string FormatResponses(List<ApiRequestTrace>? requests) {
        if (requests is null) return "OpenAI response contents were not recorded for this answer.";
        if (requests.Count == 0) return "No OpenAI request was submitted for this answer.";
        return string.Join("\r\n\r\n", requests.Select(r => r.ResponseBody
            ?? "No OpenAI response body was recorded for this call."));
    }
}
