namespace WeatherApi.Configuration;

public sealed class OpenWeatherOptions
{
    public const string SectionName = "OpenWeather";
    public string BaseUrl { get; init; } = "";
    public string Units { get; init; } = "metric";
    public string ApiKey { get; init; } = "";
}

public sealed class CacheOptions
{
    public const string SectionName = "Weather";
    public int TtlMinutes { get; init; } = 10;
}