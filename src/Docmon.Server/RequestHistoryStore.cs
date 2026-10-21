namespace Docmon.Server
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class RequestHistoryStore : IDisposable
    {
        private readonly ConcurrentQueue<RequestRecord> _Records = new ConcurrentQueue<RequestRecord>();
        private readonly SemaphoreSlim _Writer = new SemaphoreSlim(1, 1);
        private readonly string _Directory;
        private readonly string _Token;
        private DateTime _LastRetention;
        internal RequestHistoryStore(string directory, string token)
        {
            _Directory = Path.GetFullPath(directory); _Token = token; Directory.CreateDirectory(_Directory);
            foreach (string file in Directory.EnumerateFiles(_Directory, "requests-*.jsonl").OrderBy(f => f))
            {
                if (File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddDays(-30)) continue;
                foreach (string line in File.ReadLines(file))
                {
                    try { RequestRecord? record = JsonSerializer.Deserialize<RequestRecord>(line, DocmonServer.Json); if (record != null) Enqueue(record); }
                    catch (JsonException) { }
                }
            }
        }
        internal RequestRecord[] ReadSummary() => _Records.Reverse().Select(record => new RequestRecord
        { Id = record.Id, AtUtc = record.AtUtc, Method = record.Method, Path = record.Path, Status = record.Status, ElapsedMs = record.ElapsedMs, PrincipalName = record.PrincipalName, Truncated = record.Truncated }).ToArray();
        internal RequestRecord[] Read() => _Records.Reverse().ToArray();
        internal async Task AppendAsync(RequestRecord record, CancellationToken token)
        {
            Enqueue(record);
            await _Writer.WaitAsync(token).ConfigureAwait(false);
            try
            {
                string file = Path.Combine(_Directory, "requests-" + DateTime.UtcNow.ToString("yyyy-MM-dd") + ".jsonl");
                await File.AppendAllTextAsync(file, JsonSerializer.Serialize(record, DocmonServer.Json) + Environment.NewLine, token).ConfigureAwait(false);
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                if (_LastRetention.Date != DateTime.UtcNow.Date)
                {
                    foreach (string stale in Directory.EnumerateFiles(_Directory, "requests-*.jsonl"))
                        if (File.GetLastWriteTimeUtc(stale) < DateTime.UtcNow.AddDays(-30)) File.Delete(stale);
                    _LastRetention = DateTime.UtcNow;
                }
            }
            finally { _Writer.Release(); }
        }
        internal Dictionary<string, string> Headers(NameValueCollection headers)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            foreach (string? key in headers.AllKeys)
            {
                if (key == null) continue;
                result[key] = Regex.IsMatch(key, "authorization|cookie|api.?key|token", RegexOptions.IgnoreCase) ? "[redacted]" : (headers[key] ?? "").Replace(_Token, "[redacted]", StringComparison.Ordinal);
            }
            return result;
        }
        internal string Body(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string redacted = value.Replace(_Token, "[redacted]", StringComparison.Ordinal);
            try { JsonNode? node = JsonNode.Parse(redacted); Redact(node); redacted = node?.ToJsonString() ?? "null"; } catch (JsonException) { redacted = "[non-JSON body omitted]"; }
            return redacted.Length > 4096 ? redacted.Substring(0, 4096) : redacted;
        }
        private static void Redact(JsonNode? node)
        {
            if (node is JsonObject obj)
            {
                foreach (string key in obj.Select(item => item.Key).ToArray())
                {
                    if (Regex.IsMatch(key, "authorization|cookie|api.?key|token|secret|password|^env$|^environment$|^command$", RegexOptions.IgnoreCase)) obj[key] = "[redacted]";
                    else Redact(obj[key]);
                }
            }
            else if (node is JsonArray array) foreach (JsonNode? child in array) Redact(child);
        }
        private void Enqueue(RequestRecord record) { _Records.Enqueue(record); while (_Records.Count > 1000) _Records.TryDequeue(out _); }
        public void Dispose() { _Writer.Dispose(); }
    }
}
