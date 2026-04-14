FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/CloudflareLogExporter/ ./src/CloudflareLogExporter/
RUN dotnet restore ./src/CloudflareLogExporter/CloudflareLogExporter.csproj
RUN dotnet publish ./src/CloudflareLogExporter/CloudflareLogExporter.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
	&& apt-get install -y --no-install-recommends tzdata \
	&& rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
RUN mkdir -p /app/data

ENTRYPOINT ["dotnet", "CloudflareLogExporter.dll"]
