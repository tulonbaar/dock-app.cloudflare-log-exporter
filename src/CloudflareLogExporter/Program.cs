using CloudflareLogExporter;
using CloudflareLogExporter.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOptions<CloudflareOptions>()
    .Bind(builder.Configuration.GetSection(CloudflareOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<StorageOptions>()
    .Bind(builder.Configuration.GetSection(StorageOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<ElasticOptions>()
    .Bind(builder.Configuration.GetSection(ElasticOptions.SectionName));

builder.Services.AddSingleton(static serviceProvider =>
{
    var storageOptions = serviceProvider.GetRequiredService<IOptions<StorageOptions>>().Value;
    return new ConfiguredTimeZone(storageOptions.TimeZoneId);
});

builder.Services.AddHttpClient<CloudflareLogsClient>();
builder.Services.AddHostedService<LogPollingWorker>();

var host = builder.Build();
await host.RunAsync();
