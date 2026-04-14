using System.ComponentModel.DataAnnotations;

namespace CloudflareLogExporter.Options;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    [Required]
    public string OutputPath { get; init; } = "/app/data/cloudflare-logs.ndjson";

    [Required]
    public string TimeZoneId { get; init; } = "UTC";

    [Range(1, 120)]
    public int LookbackMinutes { get; init; } = 5;
}
