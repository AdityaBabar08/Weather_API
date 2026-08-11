using System;

namespace WeatherApi.Models;

public record DailyForecast(DateOnly Date, double MinTempC, double MaxTempC, string Condition);


public record Forecast(string City, IReadOnlyList<DailyForecast> Days);
