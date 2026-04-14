using System.Globalization;
using System.Net.Http.Headers;
using CloudflareLogExporter.Options;
using Microsoft.Extensions.Options;

namespace CloudflareLogExporter;

public sealed class CloudflareLogsClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CloudflareLogsClient> _logger;
    private readonly CloudflareOptions _options;

    public CloudflareLogsClient(
        HttpClient httpClient,
        IOptions<CloudflareOptions> options,
        ILogger<CloudflareLogsClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> FetchLogsAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken)
    {
        var baseUrl = _options.BaseUrl.TrimEnd('/');
        var sql = BuildSqlQuery(start, end);
        _logger.LogDebug(
            "Cloudflare SQL window start={StartUtc:o}, end={EndUtc:o}, timeColumn={TimeColumn}",
            start.UtcDateTime,
            end.UtcDateTime,
            _options.TimeColumn);
        var url = $"{baseUrl}/zones/{_options.ZoneId}/logs/explorer/query/sql" +
                  $"?query={Uri.EscapeDataString(sql)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Cloudflare API error {(int)response.StatusCode}: {body}");
        }

        return body;
    }

    private string BuildSqlQuery(DateTimeOffset start, DateTimeOffset end)
    {
        var startUtc = start.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        var endUtc = end.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

        return $"SELECT {_options.SelectColumns} FROM {_options.Dataset} " +
             $"WHERE {_options.TimeColumn} >= '{startUtc}' " +
             $"AND {_options.TimeColumn} < '{endUtc}' " +
                         $"ORDER BY {_options.TimeColumn} DESC " +
               $"LIMIT {_options.MaxRows}";
    }
}
