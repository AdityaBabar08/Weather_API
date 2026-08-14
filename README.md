# Weather API

## Purpose

The Weather API is a web API for ASP.NET Core. It gets current weather data and
weather forecasts from OpenWeatherMap. It stores the responses in a Redis cache.
It sends the responses to clients as JSON.

The API has three endpoints:

| Method | Route               | Description                         |
|--------|---------------------|-------------------------------------|
| GET    | `/weather/current`  | Get the current weather for a city. |
| GET    | `/weather/forecast` | Get the daily forecast for a city.  |
| GET    | `/health`           | Get the status of the Redis cache.  |

## Technology

| Component           | Technology                                  |
|---------------------|---------------------------------------------|
| Web framework       | ASP.NET Core (minimal API)                  |
| Language            | C#                                          |
| Weather data source | OpenWeatherMap                              |
| Cache               | Redis                                       |
| Cache client        | `IDistributedCache` (StackExchange.Redis)   |
| Test framework      | xUnit                                       |

## Prerequisites

- Install the .NET 10 SDK.
- Install Docker Desktop.
- Get a free OpenWeatherMap API key.

A new API key can take 1 to 2 hours to activate.

## Setup

### Step 1: Start Redis

Run this command in the repository root:

```powershell
docker compose up -d
```

This command starts a Redis container. The API connects to the container on
port 6379.

### Step 2: Set the API key

The API key is not stored in the repository. Store it with user secrets.

Run these commands in the `WeatherApi` directory:

```powershell
dotnet user-secrets init
dotnet user-secrets set "OpenWeather:ApiKey" "your-key-here"
```

### Step 3: Run the API

Run this command in the repository root:

```powershell
dotnet run --project WeatherApi
```

In the Development environment, the API listens on these addresses:

- `https://localhost:7183`
- `http://localhost:5087`

## Use the API

These examples use curl. Replace `PORT` with the port from your launch profile.

Get the current weather for a city:

```powershell
curl "https://localhost:PORT/weather/current?city=Karachi"
```

Get the daily forecast for a city:

```powershell
curl "https://localhost:PORT/weather/forecast?city=Karachi&days=3"
```

The `days` parameter is optional. The default value is 5. The API limits the
value to the range 1 to 5.

Get the status of the Redis cache:

```powershell
curl "https://localhost:PORT/health"
```

The repository also contains `WeatherApi.http`. It has sample requests for the
VS Code REST Client.

### Test the degraded mode

Stop the Redis container:

```powershell
docker compose stop redis
```

The API still returns weather data. The `/health` endpoint reports `Degraded`.

Start the Redis container again:

```powershell
docker compose start redis
```

## How the cache works

The API uses the cache-aside pattern. It reads the cache before it calls
OpenWeatherMap.

1. The API makes a cache key from the request parameters. The city name is in
   lowercase. Example: `weather:current:karachi`
2. The API reads the key from Redis.
3. If the key exists, the API returns the stored JSON. It does not call
   OpenWeatherMap.
4. If the key does not exist, the API calls OpenWeatherMap. It stores the
   response in Redis for 10 minutes.

The forecast cache key also contains the number of days.
Example: `weather:forecast:karachi:3`

The API works when Redis is down. The API uses try/catch blocks around all
cache operations. If Redis is not available, the API calls OpenWeatherMap
directly. The `/health` endpoint reports the status of Redis:

- `Healthy`: Redis is available.
- `Degraded`: Redis is not available.

## Design decisions

- The cache TTL is 10 minutes. Weather data changes quickly. A 10-minute TTL
  keeps the data new. It also keeps the number of API calls low. The free
  OpenWeatherMap tier permits 60 calls per minute and 1,000,000 calls per
  month.
- The API uses metric units. OpenWeatherMap returns temperatures in Kelvin
  when the units parameter is not set. The API sets `units=metric`.
- The forecast endpoint returns daily data. The free OpenWeatherMap tier has
  no daily forecast endpoint. The API gets the 5-day forecast in 3-hour steps.
  It combines the 3-hour entries by day. It calculates the minimum and the
  maximum temperature for each day.
- The API responses do not contain the OpenWeatherMap response structure. The
  API maps the upstream data to its own DTOs. The API surface is independent
  of upstream changes.
- The API returns errors as JSON problem responses. It never returns an
  unhandled 500 response.

## Errors

| Condition                          | Status code |
|------------------------------------|-------------|
| The `city` parameter is missing or empty | 400 |
| OpenWeatherMap does not know the city | 404 |
| The API key is invalid             | 401 |
| OpenWeatherMap is not available    | 502 |

## Configuration

The `appsettings.json` file has these sections:

| Section                        | Purpose                                 |
|--------------------------------|-----------------------------------------|
| `OpenWeather:BaseUrl`          | The base URL of the OpenWeatherMap API  |
| `OpenWeather:Units`            | The unit system for the temperature     |
| `OpenWeather:ApiKey`           | The API key (set with user secrets)     |
| `Redis:ConnectionString`       | The address of the Redis server         |
| `Weather:CacheTtlMinutes`      | The TTL of the cache in minutes         |

## Tests

Run this command in the repository root:

```powershell
dotnet test
```

The tests cover these behaviors:

- Cache miss: the service calls OpenWeatherMap and stores the response.
- Cache hit: the service returns the cached data. It does not call
  OpenWeatherMap.
- Redis down: the service still returns data from OpenWeatherMap.
- Forecast aggregation: the daily minimum and maximum temperatures are
  correct.
- Days limit: the service limits the number of days.

## Alternative provider

Visual Crossing (Timeline API) is a good alternative to OpenWeatherMap. It has
a similar free tier. The API maps upstream data to its own DTOs. A different
provider can be added without changing the API surface.
