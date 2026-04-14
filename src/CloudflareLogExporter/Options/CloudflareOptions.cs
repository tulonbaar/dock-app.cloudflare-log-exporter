using System.ComponentModel.DataAnnotations;

namespace CloudflareLogExporter.Options;

public sealed class CloudflareOptions
{
    public const string SectionName = "Cloudflare";

    [Required]
    public string ApiToken { get; init; } = string.Empty;

    [Required]
    public string ZoneId { get; init; } = string.Empty;

    [Required]
    public string BaseUrl { get; init; } = "https://api.cloudflare.com/client/v4";

    [Range(5, 3600)]
    public int QueryIntervalSeconds { get; init; } = 60;

    [Required]
    public string Dataset { get; init; } = "http_requests";

    [Required]
    public string SelectColumns { get; init; } =
        "RayID,ZoneName,EdgeStartTimestamp,EdgeEndTimestamp,ClientIP,ClientCountry,ClientCity,ClientLatitude,ClientLongitude,ClientRequestMethod,ClientRequestScheme,ClientRequestHost,ClientRequestURI,ClientRequestBytes,EdgeResponseStatus,EdgeResponseBytes,EdgeResponseBodyBytes,EdgeResponseContentType,EdgeTimeToFirstByteMs,OriginResponseDurationMs,OriginResponseStatus";

    [Required]
    public string TimeColumn { get; init; } = "EdgeEndTimestamp";

    [Range(1, 10000)]
    public int MaxRows { get; init; } = 5000;

    [Range(0, 3600)]
    public int IngestionDelaySeconds { get; init; } = 180;
}
