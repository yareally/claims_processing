using Claims.Api;
using Claims.Api.Workflow;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Claims.Tests;

/// <summary>Brief item 5. These tests check the method ToHttpResult.</summary>
public sealed class Level1ApiTests
{
    [Theory]
    [InlineData(ClaimStatus.Submitted, 201)]
    [InlineData(ClaimStatus.Rejected, 400)]
    [InlineData(ClaimStatus.InProgress, 409)]
    [InlineData(ClaimStatus.Compensated, 503)]
    [InlineData(ClaimStatus.CompensationFailed, 500)]
    public async Task ToHttpResult_returns_the_status_code(ClaimStatus status, int expectedStatusCode)
    {
        var response = await RunToHttpResultAsync(NewClaim(status));

        Assert.Equal(expectedStatusCode, response.StatusCode);
    }

    [Fact]
    public async Task ToHttpResult_submitted_claim_has_a_location_header()
    {
        var claim = NewClaim(ClaimStatus.Submitted);

        var response = await RunToHttpResultAsync(claim);

        Assert.Equal($"/claims/{claim.ClaimId}", response.Headers.Location.ToString());
    }

    [Fact]
    public void ToHttpResult_rejected_claim_has_the_claim_response_as_its_body()
    {
        // TODO: call ClaimEndpoints.ToHttpResult with a rejected claim that has a RejectionReason.
        // Cast the result to IValueHttpResult. Check that its Value equals ClaimResponse.From(claim).
        Assert.Fail("This test is empty. Follow its TODO.");
    }

    private static ClaimRecord NewClaim(ClaimStatus status) =>
        ClaimRecord.Start(WorkflowTestContext.ValidRequest) with { Status = status };

    private static async Task<HttpResponse> RunToHttpResultAsync(ClaimRecord claim)
    {
        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
        };
        httpContext.Response.Body = new MemoryStream();
        await ClaimEndpoints.ToHttpResult(claim).ExecuteAsync(httpContext);
        return httpContext.Response;
    }
}
