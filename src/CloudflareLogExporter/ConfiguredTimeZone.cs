namespace CloudflareLogExporter;

public sealed class ConfiguredTimeZone
{
    public ConfiguredTimeZone(string timeZoneId)
    {
        TimeZoneId = timeZoneId;
        TimeZoneInfo = Resolve(timeZoneId);
    }

    public string TimeZoneId { get; }

    public TimeZoneInfo TimeZoneInfo { get; }

    public static TimeZoneInfo Resolve(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException ex)
        {
            throw new InvalidOperationException($"Configured timezone '{timeZoneId}' was not found.", ex);
        }
        catch (InvalidTimeZoneException ex)
        {
            throw new InvalidOperationException($"Configured timezone '{timeZoneId}' is invalid.", ex);
        }
    }
}