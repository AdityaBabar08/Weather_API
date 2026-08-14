using System.Net;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using static WeatherApi.Tests.FakeHttpMessageHandler;

namespace WeatherApi.Tests;

internal sealed class FakeHttpMessageHandler(HttpFunc responder) : HttpMessageHandler
{
    public delegate HttpResponseMessage HttpFunc(HttpRequestMessage request);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(responder(request));

    public static HttpResponseMessage Ok(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
}

internal sealed class ThrowingCache : IDistributedCache
{
    private static readonly Exception RedisDown = new("redis is down");
    public byte[]? Get(string key) => throw RedisDown;
    public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw RedisDown;
    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw RedisDown;
    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => throw RedisDown;
    public void Refresh(string key) => throw RedisDown;
    public Task RefreshAsync(string key, CancellationToken token = default) => throw RedisDown;
    public void Remove(string key) => throw RedisDown;
    public Task RemoveAsync(string key, CancellationToken token = default) => throw RedisDown;
}