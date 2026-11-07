using System.Net;
using System.Net.Http.Json;
using Claims.Api;
using Claims.Api.Workflow;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Claims.Tests;

/// <summary>
/// Runs the API and the stub services in memory and sends actual HTTP requests between
/// them, so these tests also cover the service clients in HttpServices.cs.
/// </summary>
public sealed class EndToEndTests : IDisposable
{
    private readonly WebApplicationFactory<StubsEntryPoint> _stubs = new();
    private readonly WebApplicationFactory<ClaimsApiEntryPoint> _api;
    private readonly HttpClient _apiClient;

    public EndToEndTests()
    {
        _api = new WebApplicationFactory<ClaimsApiEntryPoint>().WithWebHostBuilder(host =>
            host.ConfigureTestServices(services =>
            {
                services.ConfigureHttpClientDefaults(client =>
                    client.ConfigurePrimaryHttpMessageHandler(() => _stubs.Server.CreateHandler()));
                services.AddSingleton(new WorkflowOptions { CompensationRetryDelay = TimeSpan.Zero });
            }));
        _apiClient = _api.CreateClient();
    }

    public void Dispose()
    {
        _apiClient.Dispose();
        _api.Dispose();
        _stubs.Dispose();
    }

    [Fact]
    public async Task A_good_claim_leaves_the_reserve_and_case_open()
    {
        var response = await SubmitAsync("POL-100");
        var state = await StubStateAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Single(state.OpenReserves);
        Assert.Single(state.OpenCases);
    }

    [Fact]
    public async Task An_inactive_policy_returns_422_and_opens_nothing()
    {
        var response = await SubmitAsync("POL-INACTIVE");
        var state = await StubStateAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(new[] { "POST /coverage-checks" }, state.Calls);
    }

    [Fact]
    public async Task A_failed_case_step_releases_the_reserve()
    {
        var response = await SubmitAsync("POL-FAIL-CASE");
        var state = await StubStateAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Empty(state.OpenReserves);
        Assert.Empty(state.OpenCases);
    }

    [Fact]
    public async Task A_failed_adjuster_step_undoes_the_case_then_the_reserve()
    {
        var response = await SubmitAsync("POL-FAIL-ADJUSTER");
        var state = await StubStateAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Empty(state.OpenReserves);
        Assert.Empty(state.OpenCases);
        Assert.Equal(new[] { "DELETE /cases", "DELETE /reserves" }, state.Calls.TakeLast(2));
    }

    [Fact]
    public async Task A_reserve_that_cannot_be_released_returns_500_and_stays_open()
    {
        var response = await SubmitAsync("POL-FAIL-RELEASE");
        var state = await StubStateAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Single(state.OpenReserves);
        Assert.Empty(state.OpenCases);
    }

    [Fact]
    public async Task A_failed_email_still_returns_201_and_keeps_the_claim()
    {
        var response = await SubmitAsync("POL-FAIL-NOTIFY");
        var claim = await ApiTestContext.ReadClaimAsync(response);
        var state = await StubStateAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.False(claim.NotificationSent);
        Assert.Single(state.OpenReserves);
        Assert.Single(state.OpenCases);
    }

    private Task<HttpResponseMessage> SubmitAsync(string policyNumber) =>
        ApiTestContext.PostClaimAsync(
            _apiClient,
            new SubmitClaimRequest(policyNumber, "pat@example.com", new DateOnly(2026, 1, 15), 2500m),
            idempotencyKey: $"key-{policyNumber}");

    private async Task<StubSnapshot> StubStateAsync()
    {
        using var client = _stubs.CreateClient();
        var state = await client.GetFromJsonAsync<StubSnapshot>("/debug/state");
        return Assert.IsType<StubSnapshot>(state);
    }
}
