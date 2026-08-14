using System;
using System.Text.Json.Serialization;

namespace WeatherApi.Models.OpenWeatherMap;

internal sealed class OwmCurrentResponse
{
    public string Name { get; set; } = null!;
    public OwmMain Main { get; set; } = null!;
    public OwmWeather[] Weather { get; set; } = [];
}

internal sealed class OwmForecastResponse
{
    public OwmForecastEntry[] List { get; set; } = [];
    public OwmCity City { get; set; } = null!;
}

internal sealed class OwmForecastEntry
{
    public long Dt { get; set; }
    public OwmMain Main { get; set; } = null!;
    public OwmWeather[] Weather { get; set; } = [];
}

internal sealed class OwmCity
{
    public string Name { get; set; } = null!;
}
internal sealed class OwmMain
{
    public double Temp { get; set; }
    [JsonPropertyName("temp_min")]
    public double TempMin { get; set; }
    [JsonPropertyName("temp_max")]
    public double TempMax { get; set; }
}

internal sealed class OwmWeather
{
    public string Description { get; set; } = null!;
}