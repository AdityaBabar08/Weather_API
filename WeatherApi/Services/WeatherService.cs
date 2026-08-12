using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using WeatherApi.Configuration;
using WeatherApi.Models;
using WeatherApi.Models.OpenWeatherMap;

namespace WeatherApi.Services;

public sealed class WeatherService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly IDistributedCache _cache;
    private readonly OpenWeatherOptions _options;
    private readonly CacheOptions _cacheOptions;
    private readonly ILogger<WeatherService> _logger;

    public WeatherService(
        HttpClient http,
        IDistributedCache cache,
        IOptions<OpenWeatherOptions> options,
        IOptions<CacheOptions> cacheOptions,
        ILogger<WeatherService> logger)
    {
        _http = http;
        _cache = cache;
        _options = options.Value;
        _cacheOptions = cacheOptions.Value;
        _logger = logger;
    }

    public async Task<CurrentWeather> GetCurrentAsync(string city, CancellationToken ct)
    {
        var key = CacheKey("weather:current", city);

        var cached = await TryReadCacheAsync(key, ct);
        if (cached is not null)
        {
            _logger.LogInformation("cache hit city={City}", city);
            return JsonSerializer.Deserialize<CurrentWeather>(cached, JsonOptions)!;
        }
        _logger.LogInformation("cache miss city={City}", city);

        var owm = await GetFromUpstreamAsync<OwmCurrentResponse>("/weather", city, ct);
        var result = new CurrentWeather(
            owm.Name,
            owm.Main.Temp,
            owm.Weather.FirstOrDefault()?.Description ?? "Unknown");

        await TryWriteCacheAsync(key, result, ct);
        return result;
    }

    public async Task<Forecast> GetForecastAsync(string city, int days, CancellationToken ct)
    {
        var key = CacheKey("weather:forecast", city, days);

        var cached = await TryReadCacheAsync(key, ct);
        if (cached is not null)
        {
            _logger.LogInformation("cache hit city={City} days={Days}", city, days);
            return JsonSerializer.Deserialize<Forecast>(cached, JsonOptions)!;
        }
        _logger.LogInformation("cache miss city={City} days={Days}", city, days);

        var owm = await GetFromUpstreamAsync<OwmForecastResponse>("/forecast", city, ct);
        var daily = owm.List
            .GroupBy(e => DateTimeOffset.FromUnixTimeSeconds(e.Dt).LocalDateTime.Date)
            .OrderBy(g => g.Key)
            .Take(days)
            .Select(g => new DailyForecast(
                DateOnly.FromDateTime(g.Key),
                g.Min(e => e.Main.TempMin),
                g.Max(e => e.Main.TempMax),
                g.First().Weather.FirstOrDefault()?.Description ?? "Unknown"))
            .ToList();

        var result = new Forecast(owm.City.Name, daily);
        await TryWriteCacheAsync(key, result, ct);
        return result;
    }

    private async Task<T> GetFromUpstreamAsync<T>(string path, string city, CancellationToken ct)
    {
        var url = $"{_options.BaseUrl}{path}?q={Uri.EscapeDataString(city)}&units={_options.Units}&appid={_options.ApiKey}";
        using var response = await _http.GetAsync(url, ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("upstream error status={Status} city={City}", response.StatusCode, city);
            throw new HttpRequestException($"Upstream returned {(int)response.StatusCode}", null, response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<T>(ct)
               ?? throw new InvalidOperationException("Upstream returned an empty body");
    }

    private async Task<string?> TryReadCacheAsync(string key, CancellationToken ct)
    {
        try
        {
            return await _cache.GetStringAsync(key, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "cache read failed, falling back to upstream");
            return null;
        }
    }

    private async Task TryWriteCacheAsync(string key, object value, CancellationToken ct)
    {
        try
        {
            var ttl = TimeSpan.FromMinutes(_cacheOptions.TtlMinutes);
            await _cache.SetStringAsync(key, JsonSerializer.Serialize(value, JsonOptions),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }, ct);
            _logger.LogInformation("cache stored key={Key} ttl={Ttl}min", key, ttl.TotalMinutes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "cache write failed, continuing without cache");
        }
    }

    private static string CacheKey(string prefix, string city, int? days = null)
    {
        var normalized = city.Trim().ToLowerInvariant();
        return days is null ? $"{prefix}:{normalized}" : $"{prefix}:{normalized}:{days}";
    }
}