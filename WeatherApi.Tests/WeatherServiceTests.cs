using System;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WeatherApi.Configuration;
using WeatherApi.Services;

namespace WeatherApi.Tests;

public class WeatherServiceTests
{
    private static WeatherService CreateService(HttpMessageHandler handler, IDistributedCache cache)
        => new(
            new HttpClient(handler),
            cache,
            Options.Create(new OpenWeatherOptions { BaseUrl = "https://api.openweathermap.org/data/2.5", Units = "metric", ApiKey = "test-key" }),
            Options.Create(new CacheOptions { TtlMinutes = 10 }),
            NullLogger<WeatherService>.Instance);
    private const string CurrentJson = """
    {
    "weather": [ { "description": "broken clouds" } ],
    "main": { "temp": 27.45, "temp_min": 25.9, "temp_max": 29.3 },
    "name": "Karachi"
    }
    """;

    // two 3-hour slots at 12:00 and 18:00 UTC — same local date in ANY timezone
    private const string ForecastJson = """
    {
    "list": [
        { "dt": 1723262400, "main": { "temp": 30.0, "temp_min": 26.0, "temp_max": 31.0 }, "weather": [ { "description": "clear sky" } ] },
        { "dt": 1723273200, "main": { "temp": 28.0, "temp_min": 24.0, "temp_max": 29.0 }, "weather": [ { "description": "few clouds" } ] }
    ],
    "city": { "name": "Karachi" }
    }
    """;
    [Fact]
    public async Task GetCurrentAsync_MissThenHit_FetchesOnceThenServesFromCache()
    {
        var upstreamCalls = 0;
        var handler = new FakeHttpMessageHandler(_ => { upstreamCalls++; return FakeHttpMessageHandler.Ok(CurrentJson); });
        var service = CreateService(handler, new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));

        var first = await service.GetCurrentAsync("karachi", CancellationToken.None);
        var second = await service.GetCurrentAsync("Karachi", CancellationToken.None);  // different casing

        Assert.Equal(27.45, first.TemperatureC);
        Assert.Equal("broken clouds", first.Condition);
        Assert.Equal(first, second);        // cache hit returns identical data
        Assert.Equal(1, upstreamCalls);     // second call never reached upstream
    }

    [Fact]
    public async Task GetForecastAsync_AggregatesThreeHourSlotsByDay()
    {
        var service = CreateService(new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Ok(ForecastJson)),
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));

        var forecast = await service.GetForecastAsync("Karachi", 1, CancellationToken.None);

        var day = Assert.Single(forecast.Days);
        Assert.Equal(24.0, day.MinTempC);   // min of the temp_mins
        Assert.Equal(31.0, day.MaxTempC);   // max of the temp_maxes
    }

    [Fact]
    public async Task GetCurrentAsync_RedisDown_StillReturnsUpstreamData()
    {
        var service = CreateService(new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Ok(CurrentJson)),
            new ThrowingCache());

        var result = await service.GetCurrentAsync("Karachi", CancellationToken.None);

        Assert.Equal(27.45, result.TemperatureC);   // graceful degradation works
    }
    [Fact]
    public async Task GetForecastAsync_RespectsDaysLimit()
    {
        var entries = Enumerable.Range(0, 16) // two days × eight 3-hour slots
            .Select(i => $$"""
            {
                "dt": {{1723262400 + i * 10800}},
                "main": { "temp": 30.0, "temp_min": 26.0, "temp_max": 31.0 },
                "weather": [ { "description": "clear sky" } ]
            }
            """);

        var json = $$"""{ "list": [ {{string.Join(",", entries)}} ], "city": { "name": "Karachi" } }""";

        var service = CreateService(
            new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Ok(json)),
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()))
        );

        var forecast = await service.GetForecastAsync("Karachi", 1, CancellationToken.None);

        Assert.Single(forecast.Days); // Take(days) works
    }
}
