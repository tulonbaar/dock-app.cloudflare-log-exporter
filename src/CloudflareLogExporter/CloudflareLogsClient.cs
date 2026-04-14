using System.Globalization;
using System.Net.Http.Headers;
using CloudflareLogExporter.Options;
using Microsoft.Extensions.Options;

namespace CloudflareLogExporter;

public sealed class CloudflareLogsClient
{
    private readonly HttpClient _httpClient;
    private readonly CloudflareOptions _options;

    public CloudflareLogsClient(HttpClient httpClient, IOptions<CloudflareOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<string> FetchLogsAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken)
    {
        var baseUrl = _options.BaseUrl.TrimEnd('/');
        var sql = BuildSqlQuery(start, end);
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
               $"LIMIT {_options.MaxRows}";
    }
}
