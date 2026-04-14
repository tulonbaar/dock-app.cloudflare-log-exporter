namespace CloudflareLogExporter.Options;

public sealed class ElasticOptions
{
    public const string SectionName = "Elastic";

    public bool Enabled { get; init; }

    public string Endpoint { get; init; } = string.Empty;

    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string IndexName { get; init; } = "cloudflare-logs";
}
