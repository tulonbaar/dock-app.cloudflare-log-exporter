# Cloudflare Log Exporter (PoC)

A C# (.NET 10) proof-of-concept application that periodically fetches **logs** from the Cloudflare Log Explorer API (SQL API) and writes them to a local NDJSON file.

## PoC Scope

- Periodic polling of the Cloudflare API (logs only, not metrics).
- Fetching through the `zones/{zone_id}/logs/explorer/query/sql` endpoint.
- Configuration via `.env`.
- Writing logs to a local file (`/app/data/cloudflare-logs.ndjson`).
- Structure prepared for the next step: direct writes to Elasticsearch.

## Requirements

- Docker + Docker Compose.

## Configuration

1. Copy the configuration file:

```bash
cp .env.example .env
```

2. Set the values:

- `Cloudflare__ApiToken`
- `Cloudflare__ZoneId`
- optional: `Cloudflare__IngestionDelaySeconds` (default: `180`)
- `Storage__EnableDailyRolling` (`true`/`false`, default: `true`)
- `Storage__MaxFileSizeBytes` (default: `104857600`, set `0` to disable size-based rolling)
- `Storage__TimeZoneId` (for example: `Europe/Warsaw`, `America/New_York`, `UTC`)
- `Storage__RewriteCloudflareTimestampsToLocal` (`true`/`false`)

The token should have permissions to read logs for the zone.

`Cloudflare__IngestionDelaySeconds` helps avoid empty responses for very recent time windows (indexing delay on the Log Explorer side).

`Storage__TimeZoneId` controls how the application displays local time in its own logs and how it enriches records written to NDJSON. Raw Cloudflare timestamps stay in UTC, and their equivalent in the configured time zone is stored alongside them.

If `Storage__RewriteCloudflareTimestampsToLocal=true`, Cloudflare time fields (for example `edgestarttimestamp`, `edgeendtimestamp`) are written to NDJSON in the `Storage__TimeZoneId` time zone, while their original UTC values are stored in fields with the `_utc` suffix.

Output file rolling behavior:
- daily active file name is generated from `Storage__OutputPath` and local date, for example: `cloudflare-logs-20260421.ndjson`
- after the active file exceeds `Storage__MaxFileSizeBytes`, it is moved to the next suffix: `cloudflare-logs-20260421-1.ndjson`, then `-2`, `-3`, and so on
- the file without suffix (`cloudflare-logs-YYYYMMDD.ndjson`) is always the current write target

Each new NDJSON record also includes canonical event-time fields:
- `_event_timestamp_source` - Cloudflare column used to determine event time
- `_event_timestamp_utc` - event time in UTC
- `_event_timestamp_local` - the same time converted to `Storage__TimeZoneId`

The default `Cloudflare__SelectColumns` set now collects operationally useful fields:

- target identification: `ZoneName`, `ClientRequestScheme`, `ClientRequestHost`, `ClientRequestURI`
- client and location: `ClientIP`, `ClientCountry`, `ClientCity`, `ClientLatitude`, `ClientLongitude`
- sizes and statuses: `ClientRequestBytes`, `EdgeResponseStatus`, `EdgeResponseBytes`, `EdgeResponseBodyBytes`, `EdgeResponseContentType`, `OriginResponseStatus`
- timing: `EdgeStartTimestamp`, `EdgeEndTimestamp`, `EdgeTimeToFirstByteMs`, `OriginResponseDurationMs`

## Run

```bash
docker compose up --build -d
```

The container uses the time zone from `Storage__TimeZoneId`, so application logs and local time projections are consistent with your environment settings.

## View Application Logs

```bash
docker compose logs -f cloudflare-log-exporter
```

## PoC Output

The log file will appear locally in:

- `./data/cloudflare-logs-YYYYMMDD.ndjson`

## Stop

```bash
docker compose down
```

## Next Step

In the next phase, we will replace the file-based storage layer with a direct, authenticated Elasticsearch sink.
