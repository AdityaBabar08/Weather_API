using System.Net;
using Microsoft.Extensions.Caching.Distributed;
using WeatherApi.Configuration;
using WeatherApi.Services;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT"); if (!string.IsNullOrEmpty(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.Configure<OpenWeatherOptions>(
    builder.Configuration.GetSection(OpenWeatherOptions.SectionName));
builder.Services.Configure<CacheOptions>(
    builder.Configuration.GetSection(CacheOptions.SectionName));

builder.Services.AddStackExchangeRedisCache(options =>
    options.Configuration = builder.Configuration["Redis:ConnectionString"]);

builder.Services.AddHttpClient<WeatherService>(client =>
    client.Timeout = TimeSpan.FromSeconds(10));

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

if (string.IsNullOrEmpty(port)) app.UseHttpsRedirection();

app.MapGet("/weather/current", async (string? city, WeatherService weather, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(city))
        return Results.Problem("'city' is required.", statusCode: StatusCodes.Status400BadRequest);

    try
    {
        return Results.Ok(await weather.GetCurrentAsync(city, ct));
    }
    catch (HttpRequestException ex)
    {
        return UpstreamError(ex, city);
    }
}).WithName("GetCurrentWeather");

app.MapGet("/weather/forecast", async (string? city, int? days, WeatherService weather, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(city))
        return Results.Problem("'city' is required.", statusCode: StatusCodes.Status400BadRequest);

    var clampedDays = Math.Clamp(days ?? 5, 1, 5);

    try
    {
        return Results.Ok(await weather.GetForecastAsync(city, clampedDays, ct));
    }
    catch (HttpRequestException ex)
    {
        return UpstreamError(ex, city);
    }
}).WithName("GetForecast");

app.MapGet("/health", async (IDistributedCache cache) =>
{
    try
    {
        await cache.GetStringAsync("health-probe");
        return Results.Ok(new { Status = "Healthy" });
    }
    catch
    {
        return Results.Ok(new { Status = "Degraded" });
    }
}).WithName("GetHealth");

static IResult UpstreamError(HttpRequestException ex, string city) =>
    ex.StatusCode switch
    {
        HttpStatusCode.Unauthorized => Results.Problem(
            "Invalid API key configured.", statusCode: StatusCodes.Status401Unauthorized),
        HttpStatusCode.NotFound => Results.Problem(
            $"City '{city}' was not found.", statusCode: StatusCodes.Status404NotFound),
        _ => Results.Problem(
            "Weather service is unavailable.", statusCode: StatusCodes.Status502BadGateway)
    };

app.Run();