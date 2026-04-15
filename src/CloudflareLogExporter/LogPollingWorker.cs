using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using CloudflareLogExporter.Options;
using Microsoft.Extensions.Options;

namespace CloudflareLogExporter;

public sealed class LogPollingWorker : BackgroundService
{
    private static readonly JsonSerializerOptions NdjsonSerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    private readonly ILogger<LogPollingWorker> _logger;
    private readonly CloudflareLogsClient _cloudflareClient;
    private readonly CloudflareOptions _cloudflareOptions;
    private readonly StorageOptions _storageOptions;
    private readonly ConfiguredTimeZone _configuredTimeZone;

    public LogPollingWorker(
        ILogger<LogPollingWorker> logger,
        CloudflareLogsClient cloudflareClient,
        IOptions<CloudflareOptions> cloudflareOptions,
        IOptions<StorageOptions> storageOptions,
        ConfiguredTimeZone configuredTimeZone)
    {
        _logger = logger;
        _cloudflareClient = cloudflareClient;
        _cloudflareOptions = cloudflareOptions.Value;
        _storageOptions = storageOptions.Value;
        _configuredTimeZone = configuredTimeZone;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_storageOptions.OutputPath) ?? ".");

        var ingestionDelay = TimeSpan.FromSeconds(_cloudflareOptions.IngestionDelaySeconds);
        var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, _configuredTimeZone.TimeZoneInfo);
        var initialEndLocal = nowLocal - ingestionDelay;
        var lastEndLocal = initialEndLocal.AddMinutes(-_storageOptions.LookbackMinutes);
        var lastEnd = TimeZoneInfo.ConvertTime(lastEndLocal, TimeZoneInfo.Utc);
        var interval = TimeSpan.FromSeconds(_cloudflareOptions.QueryIntervalSeconds);

        _logger.LogInformation(
            "Cloudflare log polling started. Interval: {IntervalSeconds}s, ingestion delay: {DelaySeconds}s, time column: {TimeColumn}, application timezone: {TimeZoneId}, output: {OutputPath}",
            _cloudflareOptions.QueryIntervalSeconds,
            _cloudflareOptions.IngestionDelaySeconds,
            _cloudflareOptions.TimeColumn,
            _configuredTimeZone.TimeZoneId,
            _storageOptions.OutputPath);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, _configuredTimeZone.TimeZoneInfo);
                var endLocal = nowLocal - ingestionDelay;
                var end = TimeZoneInfo.ConvertTime(endLocal, TimeZoneInfo.Utc);

                if (end <= lastEnd)
                {
                    _logger.LogInformation(
                        "Skipping poll. Window not ready yet. LastEndUtc: {LastEndUtc}, LastEndLocal: {LastEndLocal}, CandidateEndUtc: {CandidateEndUtc}, CandidateEndLocal: {CandidateEndLocal}.",
                        FormatUtc(lastEnd),
                        FormatLocal(lastEnd),
                        FormatUtc(end),
                        FormatLocal(end));
                    await Task.Delay(interval, stoppingToken);
                    continue;
                }

                var payload = await _cloudflareClient.FetchLogsAsync(lastEnd, end, stoppingToken);
                var extractedLines = ExtractLogLines(payload, end);

                if (extractedLines.Count > 0)
                {
                    await File.AppendAllLinesAsync(_storageOptions.OutputPath, extractedLines, stoppingToken);
                    _logger.LogInformation(
                        "Saved {Count} log entries for window UTC {StartUtc} - {EndUtc} | {TimeZoneId} {StartLocal} - {EndLocal}.",
                        extractedLines.Count,
                        FormatUtc(lastEnd),
                        FormatUtc(end),
                        _configuredTimeZone.TimeZoneId,
                        FormatLocal(lastEnd),
                        FormatLocal(end));
                }
                else
                {
                    _logger.LogInformation(
                        "No log entries in window UTC {StartUtc} - {EndUtc} | {TimeZoneId} {StartLocal} - {EndLocal}.",
                        FormatUtc(lastEnd),
                        FormatUtc(end),
                        _configuredTimeZone.TimeZoneId,
                        FormatLocal(lastEnd),
                        FormatLocal(end));
                }

                lastEnd = end;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Polling failed. Retrying on next interval.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private List<string> ExtractLogLines(string payload, DateTimeOffset polledAtUtc)
    {
        var lines = new List<string>();

        if (string.IsNullOrWhiteSpace(payload))
        {
            return lines;
        }

        var trimmed = payload.Trim();

        if (trimmed.StartsWith("{", StringComparison.Ordinal) ||
            trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in root.EnumerateArray())
                    {
                        lines.Add(EnrichLogLine(item.GetRawText(), polledAtUtc));
                    }

                    return lines;
                }

                if (root.ValueKind == JsonValueKind.Object &&
                    root.TryGetProperty("result", out var resultElement) &&
                    resultElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in resultElement.EnumerateArray())
                    {
                        lines.Add(EnrichLogLine(item.GetRawText(), polledAtUtc));
                    }

                    return lines;
                }
            }
            catch (JsonException)
            {
                // Fallback to line-based parsing when payload is not valid JSON.
            }
        }

        foreach (var line in payload.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            lines.Add(EnrichLogLine(line, polledAtUtc));
        }

        return lines;
    }

    private static string FormatUtc(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");

    private string FormatLocal(DateTimeOffset value) =>
        TimeZoneInfo.ConvertTime(value, _configuredTimeZone.TimeZoneInfo).ToString("yyyy-MM-dd HH:mm:ss zzz");

    private string EnrichLogLine(string line, DateTimeOffset polledAtUtc)
    {
        try
        {
            var rootNode = JsonNode.Parse(line) as JsonObject;

            if (rootNode is null)
            {
                return line;
            }

            rootNode["_app_timezone"] = _configuredTimeZone.TimeZoneId;
            rootNode["_app_polled_at_utc"] = polledAtUtc.UtcDateTime.ToString("O");
            rootNode["_app_polled_at_local"] = TimeZoneInfo.ConvertTime(polledAtUtc, _configuredTimeZone.TimeZoneInfo).ToString("O");
            rootNode["_event_timestamp_source"] = _cloudflareOptions.TimeColumn;

            AddLocalTimestampProjection(rootNode, "edgestarttimestamp");
            AddLocalTimestampProjection(rootNode, _cloudflareOptions.TimeColumn);
            AddCanonicalEventTimestamps(rootNode);

            if (_storageOptions.RewriteCloudflareTimestampsToLocal)
            {
                RewriteTimestampFieldToLocal(rootNode, "edgestarttimestamp");
                RewriteTimestampFieldToLocal(rootNode, _cloudflareOptions.TimeColumn);
            }

            return rootNode.ToJsonString(NdjsonSerializerOptions);
        }
        catch (JsonException)
        {
            return line;
        }
    }

    private void AddLocalTimestampProjection(JsonObject rootNode, string propertyName)
    {
        foreach (var property in rootNode)
        {
            if (!string.Equals(property.Key, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (property.Value is null)
            {
                return;
            }

            var rawValue = property.Value.GetValue<string>();
            if (!DateTimeOffset.TryParse(rawValue, out var timestamp))
            {
                return;
            }

            rootNode[$"{property.Key}_local"] = TimeZoneInfo.ConvertTime(timestamp, _configuredTimeZone.TimeZoneInfo).ToString("O");
            return;
        }
    }

    private void AddCanonicalEventTimestamps(JsonObject rootNode)
    {
        foreach (var property in rootNode)
        {
            if (!string.Equals(property.Key, _cloudflareOptions.TimeColumn, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (property.Value is null)
            {
                return;
            }

            var rawValue = property.Value.GetValue<string>();
            if (!DateTimeOffset.TryParse(rawValue, out var timestamp))
            {
                return;
            }

            rootNode["_event_timestamp_utc"] = timestamp.UtcDateTime.ToString("O");
            rootNode["_event_timestamp_local"] = TimeZoneInfo.ConvertTime(timestamp, _configuredTimeZone.TimeZoneInfo).ToString("O");
            return;
        }
    }

    private void RewriteTimestampFieldToLocal(JsonObject rootNode, string propertyName)
    {
        foreach (var property in rootNode)
        {
            if (!string.Equals(property.Key, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (property.Value is null)
            {
                return;
            }

            var rawValue = property.Value.GetValue<string>();
            if (!DateTimeOffset.TryParse(rawValue, out var timestamp))
            {
                return;
            }

            rootNode[$"{property.Key}_utc"] = timestamp.UtcDateTime.ToString("O");
            rootNode[property.Key] = TimeZoneInfo.ConvertTime(timestamp, _configuredTimeZone.TimeZoneInfo).ToString("O");
            return;
        }
    }
}
