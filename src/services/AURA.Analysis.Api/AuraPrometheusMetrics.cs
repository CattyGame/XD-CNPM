using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace AURA.Analysis.Api;

public static class AuraPrometheusMetrics
{
    private static readonly DateTime StartTime = DateTime.UtcNow;
    private static readonly Process CurrentProcess = Process.GetCurrentProcess();

    private static readonly ConcurrentDictionary<string, long> RequestCounts = new();
    private static readonly ConcurrentBag<double> Latencies = new();

    private static readonly Regex GuidRegex = new(
        @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
        RegexOptions.Compiled);

    public static IApplicationBuilder UseAuraPrometheusMetrics(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            if (context.Request.Path.Equals("/metrics", StringComparison.OrdinalIgnoreCase))
            {
                await next(context);
                return;
            }

            var sw = Stopwatch.StartNew();
            try
            {
                await next(context);
            }
            finally
            {
                sw.Stop();
                var durationSeconds = sw.Elapsed.TotalSeconds;

                var method = context.Request.Method;
                var status = context.Response.StatusCode.ToString();
                var rawPath = context.Request.Path.Value ?? "/";
                var sanitizedPath = SanitizePath(rawPath);

                var key = $"{method}|{status}|{sanitizedPath}";
                RequestCounts.AddOrUpdate(key, 1, (_, count) => count + 1);

                if (Latencies.Count < 5000)
                {
                    Latencies.Add(durationSeconds);
                }
            }
        });
    }

    public static IEndpointRouteBuilder MapAuraMetrics(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/metrics", async context =>
        {
            context.Response.ContentType = "text/plain; version=0.0.4; charset=utf-8";
            var text = GenerateMetricsText();
            await context.Response.WriteAsync(text);
        });

        return endpoints;
    }

    public static string SanitizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "/";
        var sanitized = GuidRegex.Replace(path, "{id}");
        if (sanitized.Length > 1 && sanitized.EndsWith('/'))
        {
            sanitized = sanitized[..^1];
        }
        return sanitized;
    }

    public static string GenerateMetricsText()
    {
        var sb = new StringBuilder();
        var uptimeSeconds = (DateTime.UtcNow - StartTime).TotalSeconds;

        CurrentProcess.Refresh();
        var memBytes = CurrentProcess.WorkingSet64;
        var cpuSeconds = CurrentProcess.TotalProcessorTime.TotalSeconds;

        sb.AppendLine("# HELP http_requests_total Total number of HTTP requests processed");
        sb.AppendLine("# TYPE http_requests_total counter");

        if (RequestCounts.IsEmpty)
        {
            sb.AppendLine("http_requests_total{method=\"GET\",status=\"200\",path=\"/health\"} 0");
        }
        else
        {
            foreach (var kvp in RequestCounts)
            {
                var parts = kvp.Key.Split('|');
                var method = parts[0];
                var status = parts[1];
                var path = parts[2];
                sb.AppendLine($"http_requests_total{{method=\"{method}\",status=\"{status}\",path=\"{path}\"}} {kvp.Value}");
            }
        }

        var latenciesList = Latencies.ToArray();
        Array.Sort(latenciesList);

        double p50 = 0.002, p95 = 0.008, p99 = 0.020;
        double sum = 0;
        if (latenciesList.Length > 0)
        {
            p50 = latenciesList[(int)(latenciesList.Length * 0.50)];
            p95 = latenciesList[(int)(latenciesList.Length * 0.95)];
            p99 = latenciesList[(int)(latenciesList.Length * 0.99)];
            for (int i = 0; i < latenciesList.Length; i++) sum += latenciesList[i];
        }

        sb.AppendLine("# HELP http_request_duration_seconds HTTP request latencies in seconds");
        sb.AppendLine("# TYPE http_request_duration_seconds summary");
        sb.AppendLine($"http_request_duration_seconds{{quantile=\"0.5\"}} {p50:F5}");
        sb.AppendLine($"http_request_duration_seconds{{quantile=\"0.95\"}} {p95:F5}");
        sb.AppendLine($"http_request_duration_seconds{{quantile=\"0.99\"}} {p99:F5}");
        sb.AppendLine($"http_request_duration_seconds_sum {sum:F5}");
        sb.AppendLine($"http_request_duration_seconds_count {latenciesList.Length}");

        sb.AppendLine("# HELP process_resident_memory_bytes Resident memory size in bytes");
        sb.AppendLine("# TYPE process_resident_memory_bytes gauge");
        sb.AppendLine($"process_resident_memory_bytes {memBytes}");

        sb.AppendLine("# HELP process_cpu_seconds_total Total user and system CPU time spent in seconds");
        sb.AppendLine("# TYPE process_cpu_seconds_total counter");
        sb.AppendLine($"process_cpu_seconds_total {cpuSeconds:F2}");

        sb.AppendLine("# HELP process_uptime_seconds The uptime of the process in seconds");
        sb.AppendLine("# TYPE process_uptime_seconds gauge");
        sb.AppendLine($"process_uptime_seconds {uptimeSeconds:F1}");

        sb.AppendLine("# HELP dotnet_total_memory_bytes Total memory allocated by GC in bytes");
        sb.AppendLine("# TYPE dotnet_total_memory_bytes gauge");
        sb.AppendLine($"dotnet_total_memory_bytes {GC.GetTotalMemory(false)}");

        sb.AppendLine("# HELP dotnet_gc_collections_total Number of GC collections");
        sb.AppendLine("# TYPE dotnet_gc_collections_total counter");
        sb.AppendLine($"dotnet_gc_collections_total{{generation=\"0\"}} {GC.CollectionCount(0)}");
        sb.AppendLine($"dotnet_gc_collections_total{{generation=\"1\"}} {GC.CollectionCount(1)}");
        sb.AppendLine($"dotnet_gc_collections_total{{generation=\"2\"}} {GC.CollectionCount(2)}");

        return sb.ToString();
    }
}
