namespace Docmon.Server
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using Docmon.Core.Models;

    internal static class Enumeration
    {
        internal static bool IsCollection(ApiOperation operation) => operation.Name.EndsWith("_list") || operation.Name == "metrics_history" || operation.Name == "containers_logs" || operation.Name == "images_tags";
        internal static EnumerationResult<T> Page<T>(IEnumerable<T> source, EnumerationQuery query, string scope)
        {
            Stopwatch watch = Stopwatch.StartNew();
            if (query.Ordering != "Ascending" && query.Ordering != "Descending") throw new ArgumentException("ordering must be Ascending or Descending.");
            if (query.StartUtc > query.EndUtc) throw new ArgumentException("startUtc must precede endUtc.");
            PropertyInfo[] properties = typeof(T).GetProperties().Where(p => p.PropertyType.IsValueType || p.PropertyType == typeof(string)).ToArray();
            PropertyInfo? Field(string name) => properties.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            bool Known(string name) => typeof(T) == typeof(string) ? name == "value" : Field(name) != null;
            object? Value(T item, string name) => typeof(T) == typeof(string) && name == "value" ? item : (Field(name) ?? throw new ArgumentException("Unknown scalar field: " + name)).GetValue(item);
            IEnumerable<T> rows = source;
            if (!string.IsNullOrEmpty(query.Search)) rows = rows.Where(item => (typeof(T) == typeof(string) ? new[] { (object?)item } : properties.Select(p => p.GetValue(item))).Any(v => Convert.ToString(v, CultureInfo.InvariantCulture)?.Contains(query.Search, StringComparison.OrdinalIgnoreCase) == true));
            foreach (KeyValuePair<string, string> filter in query.Filters)
            {
                if (!Known(filter.Key)) throw new ArgumentException("Unknown filter field: " + filter.Key);
                rows = rows.Where(item => Convert.ToString(Value(item, filter.Key), CultureInfo.InvariantCulture)?.Contains(filter.Value, StringComparison.OrdinalIgnoreCase) == true);
            }
            if (!string.IsNullOrEmpty(query.ContainerIdFilter)) rows = rows.Where(item => string.Equals(Convert.ToString(Value(item, "containerId")), query.ContainerIdFilter, StringComparison.Ordinal));
            PropertyInfo? timestamp = Field("atUtc") ?? Field("timeUtc") ?? Field("createdUtc");
            if ((query.StartUtc.HasValue || query.EndUtc.HasValue) && timestamp == null) throw new ArgumentException("This collection has no timestamp field.");
            if (query.StartUtc.HasValue) rows = rows.Where(item => (DateTime)timestamp!.GetValue(item)! >= query.StartUtc.Value.ToUniversalTime());
            if (query.EndUtc.HasValue) rows = rows.Where(item => (DateTime)timestamp!.GetValue(item)! <= query.EndUtc.Value.ToUniversalTime());
            if (!string.IsNullOrEmpty(query.SortBy))
            {
                if (!Known(query.SortBy)) throw new ArgumentException("Unknown sort field: " + query.SortBy);
                rows = query.Ordering == "Descending" ? rows.OrderByDescending(item => Value(item, query.SortBy), ScalarComparer.Instance) : rows.OrderBy(item => Value(item, query.SortBy), ScalarComparer.Instance);
            }
            else if (query.Ordering == "Descending") rows = rows.Reverse();
            List<T> all = rows.ToList();
            // Bind tokens to the resource and filters. Data can change between requests.
            string signature = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(scope + JsonSerializer.Serialize(new { query.MaxResults, query.Search, query.SortBy, query.Ordering, query.ContainerIdFilter, query.StartUtc, query.EndUtc, Filters = query.Filters.OrderBy(f => f.Key).ToArray() }))));
            int offset = 0;
            if (!string.IsNullOrEmpty(query.ContinuationToken))
            {
                try
                {
                    string[] parts = Encoding.UTF8.GetString(Convert.FromBase64String(query.ContinuationToken)).Split(':');
                    if (parts.Length != 2 || parts[0] != signature || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out offset) || offset < 0) throw new FormatException();
                }
                catch (FormatException) { throw new ArgumentException("Invalid continuationToken for this resource and query."); }
            }
            List<T> page = all.Skip(offset).Take(query.MaxResults).ToList();
            long remaining = Math.Max(0L, (long)all.Count - offset - page.Count);
            return new EnumerationResult<T> { MaxResults = query.MaxResults, TotalRecords = all.Count, RecordsRemaining = remaining, EndOfResults = remaining == 0, Objects = page, ContinuationToken = remaining == 0 ? null : Convert.ToBase64String(Encoding.UTF8.GetBytes(signature + ":" + ((long)offset + page.Count).ToString(CultureInfo.InvariantCulture))), TotalMs = watch.Elapsed.TotalMilliseconds };
        }
        private sealed class ScalarComparer : IComparer<object?>
        {
            internal static readonly ScalarComparer Instance = new ScalarComparer();
            public int Compare(object? left, object? right)
            {
                if (left == null) return right == null ? 0 : -1;
                if (right == null) return 1;
                if (left is string text) return StringComparer.OrdinalIgnoreCase.Compare(text, right.ToString());
                return ((IComparable)left).CompareTo(right);
            }
        }
    }
}
