using System.Net;

namespace Claims.Api.Services;

public static class DownstreamServiceSetup
{
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(5);

    public static IServiceCollection AddDownstreamServices(this IServiceCollection services, Uri baseUrl)
    {
        void Configure(HttpClient client)
        {
            client.BaseAddress = baseUrl;
            client.Timeout = CallTimeout;
        }

        services.AddHttpClient<IPolicyService, HttpPolicyService>(Configure);
        services.AddHttpClient<IReserveService, HttpReserveService>(Configure);
        services.AddHttpClient<ICaseService, HttpCaseService>(Configure);
        services.AddHttpClient<IAdjusterService, HttpAdjusterService>(Configure);
        services.AddHttpClient<INotificationService, HttpNotificationService>(Configure);
        return services;
    }
}

public sealed class HttpPolicyService(HttpClient http) : IPolicyService
{
    public Task<CoverageResult> CheckCoverageAsync(string policyNumber, DateOnly lossDate, CancellationToken cancellationToken) =>
        DownstreamHttp.PostForResultAsync<CoverageResult>(http, "/coverage-checks", new { policyNumber, lossDate }, cancellationToken);
}

public sealed class HttpReserveService(HttpClient http) : IReserveService
{
    public async Task<string> CreateReserveAsync(Guid claimId, string policyNumber, decimal amount, CancellationToken cancellationToken)
    {
        var created = await DownstreamHttp.PostForResultAsync<CreatedResource>(
            http, "/reserves", new { claimId, policyNumber, amount }, cancellationToken);
        return created.Id;
    }

    public Task ReleaseReserveAsync(string reserveId, CancellationToken cancellationToken) =>
        DownstreamHttp.DeleteAsync(http, $"/reserves/{reserveId}", cancellationToken);
}

public sealed class HttpCaseService(HttpClient http) : ICaseService
{
    public async Task<string> OpenCaseAsync(Guid claimId, string policyNumber, CancellationToken cancellationToken)
    {
        var created = await DownstreamHttp.PostForResultAsync<CreatedResource>(
            http, "/cases", new { claimId, policyNumber }, cancellationToken);
        return created.Id;
    }

    public Task CancelCaseAsync(string caseId, CancellationToken cancellationToken) =>
        DownstreamHttp.DeleteAsync(http, $"/cases/{caseId}", cancellationToken);
}

public sealed class HttpAdjusterService(HttpClient http) : IAdjusterService
{
    public async Task<string> AssignAdjusterAsync(string caseId, CancellationToken cancellationToken)
    {
        var created = await DownstreamHttp.PostForResultAsync<CreatedResource>(
            http, "/assignments", new { caseId }, cancellationToken);
        return created.Id;
    }
}

public sealed class HttpNotificationService(HttpClient http) : INotificationService
{
    public Task SendClaimReceivedAsync(string email, Guid claimId, CancellationToken cancellationToken) =>
        DownstreamHttp.PostAsync(http, "/notifications", new { email, claimId }, cancellationToken);
}

internal sealed record CreatedResource(string Id);

/// <summary>Sends HTTP calls and turns every failure into a DownstreamServiceException.</summary>
internal static class DownstreamHttp
{
    public static async Task<TResult> PostForResultAsync<TResult>(
        HttpClient http,
        string path,
        object body,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(() => http.PostAsJsonAsync(path, body, cancellationToken), path, cancellationToken);
        ThrowIfFailed(response, path);
        var result = await response.Content.ReadFromJsonAsync<TResult>(cancellationToken);
        return result ?? throw new DownstreamServiceException(path, "the response body was empty");
    }

    public static async Task PostAsync(HttpClient http, string path, object body, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(() => http.PostAsJsonAsync(path, body, cancellationToken), path, cancellationToken);
        ThrowIfFailed(response, path);
    }

    /// <summary>
    /// Deletes a resource. A 404 counts as success because the resource being gone is the
    /// goal. That makes a compensation safe to try again after an unclear failure.
    /// </summary>
    public static async Task DeleteAsync(HttpClient http, string path, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(() => http.DeleteAsync(path, cancellationToken), path, cancellationToken);
        if (response.StatusCode != HttpStatusCode.NotFound)
        {
            ThrowIfFailed(response, path);
        }
    }

    private static async Task<HttpResponseMessage> SendAsync(
        Func<Task<HttpResponseMessage>> send,
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            return await send();
        }
        catch (HttpRequestException exception)
        {
            throw new DownstreamServiceException(path, "the service could not be reached", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient reports its own timeout as a cancellation. The caller did not cancel here.
            throw new DownstreamServiceException(path, "the call timed out", exception);
        }
    }

    private static void ThrowIfFailed(HttpResponseMessage response, string path)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new DownstreamServiceException(path, $"the service returned {(int)response.StatusCode}");
        }
    }
}
