using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Claims.Api;
using Claims.Api.Services;
using Claims.Api.Workflow;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Claims.Tests;

/// <summary>Runs the API in memory with the HTTP service clients replaced by FakeServices.</summary>
public sealed class ApiTestContext : IDisposable
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly WebApplicationFactory<ClaimsApiEntryPoint> _factory;

    public ApiTestContext()
    {
        _factory = new WebApplicationFactory<ClaimsApiEntryPoint>().WithWebHostBuilder(host =>
            host.ConfigureTestServices(services =>
            {
                services.AddSingleton<IPolicyService>(Services);
                services.AddSingleton<IReserveService>(Services);
                services.AddSingleton<ICaseService>(Services);
                services.AddSingleton<IAdjusterService>(Services);
                services.AddSingleton<INotificationService>(Services);
                services.AddSingleton(new WorkflowOptions { CompensationRetryDelay = TimeSpan.Zero });
            }));
        Client = _factory.CreateClient();
    }

    public FakeServices Services { get; } = new();

    public HttpClient Client { get; }

    public static SubmitClaimRequest ValidRequest => WorkflowTestContext.ValidRequest;

    public Task<HttpResponseMessage> PostClaimAsync(SubmitClaimRequest request, string? idempotencyKey = "key-1") =>
        PostClaimAsync(Client, request, idempotencyKey);

    public static Task<HttpResponseMessage> PostClaimAsync(HttpClient client, SubmitClaimRequest request, string? idempotencyKey)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/claims") { Content = JsonContent.Create(request) };
        if (idempotencyKey is not null)
        {
            message.Headers.Add(ClaimEndpoints.IdempotencyKeyHeader, idempotencyKey);
        }

        return client.SendAsync(message);
    }

    public static async Task<ClaimResponse> ReadClaimAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<ClaimResponse>(JsonOptions);
        return Assert.IsType<ClaimResponse>(body);
    }

    public void Dispose()
    {
        Client.Dispose();
        _factory.Dispose();
    }
}
