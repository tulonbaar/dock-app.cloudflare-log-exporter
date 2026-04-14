# Cloudflare Log Exporter (PoC)

PoC aplikacji w C# (.NET 10), która cyklicznie pobiera **logi** z API Cloudflare Log Explorer (SQL API) i zapisuje je do lokalnego pliku NDJSON.

## Zakres PoC

- Cykliczny polling API Cloudflare (nie metryki, tylko logi).
- Pobieranie przez endpoint `zones/{zone_id}/logs/explorer/query/sql`.
- Konfiguracja przez `.env`.
- Zapis logów do pliku lokalnego (`/app/data/cloudflare-logs.ndjson`).
- Struktura przygotowana pod kolejny krok: bezpośredni zapis do Elasticsearch.

## Wymagania

- Docker + Docker Compose.

## Konfiguracja

1. Skopiuj plik konfiguracyjny:

```bash
cp .env.example .env
```

2. Ustaw wartości:

- `Cloudflare__ApiToken`
- `Cloudflare__ZoneId`
- opcjonalnie: `Cloudflare__IngestionDelaySeconds` (domyślnie `180`)
- `Storage__TimeZoneId` (np. `Europe/Warsaw`, `America/New_York`, `UTC`)
- `Storage__RewriteCloudflareTimestampsToLocal` (`true`/`false`)

Token powinien mieć uprawnienia do odczytu logów dla strefy (zone).

`Cloudflare__IngestionDelaySeconds` pomaga uniknąć pustych odpowiedzi dla zbyt świeżych okien czasowych (opóźnienie indeksowania po stronie Log Explorer).

`Storage__TimeZoneId` steruje tym, jak aplikacja pokazuje lokalny czas w swoich logach i jak wzbogaca rekordy zapisywane do NDJSON. Surowe timestampy z Cloudflare pozostają w UTC, ale obok zapisywany jest ich odpowiednik w skonfigurowanej strefie.

Jeśli `Storage__RewriteCloudflareTimestampsToLocal=true`, pola czasu z Cloudflare (np. `edgestarttimestamp`, `edgeendtimestamp`) są zapisywane w NDJSON już w strefie `Storage__TimeZoneId`, a ich pierwotna wersja UTC trafia do pól z sufiksem `_utc`.

Każdy nowy rekord NDJSON zawiera też kanoniczne pola czasu zdarzenia:
- `_event_timestamp_source` - kolumna Cloudflare użyta do wyznaczenia czasu zdarzenia
- `_event_timestamp_utc` - czas zdarzenia w UTC
- `_event_timestamp_local` - ten sam czas przeliczony do `Storage__TimeZoneId`

Domyślny zestaw `Cloudflare__SelectColumns` zbiera teraz pola przydatne operacyjnie:

- identyfikacja celu: `ZoneName`, `ClientRequestScheme`, `ClientRequestHost`, `ClientRequestURI`
- klient i lokalizacja: `ClientIP`, `ClientCountry`, `ClientCity`, `ClientLatitude`, `ClientLongitude`
- rozmiary i statusy: `ClientRequestBytes`, `EdgeResponseStatus`, `EdgeResponseBytes`, `EdgeResponseBodyBytes`, `EdgeResponseContentType`, `OriginResponseStatus`
- czasy: `EdgeStartTimestamp`, `EdgeEndTimestamp`, `EdgeTimeToFirstByteMs`, `OriginResponseDurationMs`

## Uruchomienie

```bash
docker compose up --build -d
```

Kontener używa strefy z `Storage__TimeZoneId`, dzięki czemu logi aplikacji i lokalne projekcje czasu są zgodne z ustawieniami środowiska.

## Podgląd logów aplikacji

```bash
docker compose logs -f cloudflare-log-exporter
```

## Wynik PoC

Plik z logami pojawi się lokalnie w katalogu:

- `./data/cloudflare-logs.ndjson`

## Zatrzymanie

```bash
docker compose down
```

## Następny krok

W kolejnym etapie zamieniamy warstwę zapisu plikowego na bezpośredni sink do Elasticsearch z autoryzacją.
