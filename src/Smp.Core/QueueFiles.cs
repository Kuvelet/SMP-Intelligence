using System.Text.Json;

namespace SMP;

public static class QueueFiles {
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    // Readers keep their current snapshot while the writer atomically replaces the file.
    public static T Read<T>(string path) => FileOperation(path, "read", () => {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return JsonSerializer.Deserialize<T>(stream, Json) ?? throw new InvalidDataException("Empty file.");
    });
    internal static T RetryFileAccess<T>(Func<T> action) {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        for (var attempt = 0; ; attempt++) {
            try { return action(); }
            catch (Exception e) when (IsTransientFileAccess(e) && timer.ElapsedMilliseconds < 5000) {
                Thread.Sleep(Math.Min(50 * (attempt + 1), 250));
            }
        }
    }
    internal static bool IsTransientFileAccess(Exception e) =>
        (e is IOException or UnauthorizedAccessException) &&
        (e.HResult & 0xffff) is 2 or 3 or 5 or 32 or 33 or 53 or 59 or 64 or 121 or 1231;
    static T FileOperation<T>(string path, string operation, Func<T> action) {
        try { return RetryFileAccess(action); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            ex.Data["FilePath"] = path;
            ex.Data["FileOperation"] = operation;
            throw;
        }
    }
    public static void Write<T>(string path, T value) => WriteSerialized(path, JsonSerializer.Serialize(value, Json), path + "." + Guid.NewGuid().ToString("N") + ".tmp");
    internal static void WriteSerialized(string path, string json, string temporary) {
        try {
            // A scanner can lock the staging file too. Retry writing the same snapshot;
            // never rerun the usage increment or submit a second job.
            FileOperation(temporary, "write temporary", () => { File.WriteAllText(temporary, json); return true; });
            // Retry only publishing the SAME file, never submitting another question.
            FileOperation(path, "publish", () => {
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                return true;
            });
        } catch (Exception original) {
            // Cleanup must not replace the useful original error. The queue's existing
            // stale-temp cleanup handles a file that remains locked.
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException) {
                original.Data["TemporaryCleanupError"] = cleanup.Message;
            }
            throw;
        }
    }
}
