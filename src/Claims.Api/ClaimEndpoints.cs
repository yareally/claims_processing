using Claims.Api.Workflow;

namespace Claims.Api;

public static class ClaimEndpoints
{
    public const string IdempotencyKeyHeader = "Idempotency-Key";

    public static void MapClaimEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/claims", SubmitAsync);
        app.MapGet("/claims/{claimId:guid}", GetAsync);
    }

    /// <summary>Turns a finished claim into the HTTP response the caller receives.</summary>
    public static IResult ToHttpResult(ClaimRecord claim)
    {
        // TODO: return the status codes listed in docs/01_brief.md.
        // Use ClaimResponse.From(claim) as the response body.
        var claimResponse = ClaimResponse.From(claim);
        var statusCode = claim.Status switch
        {
          ClaimStatus.Submitted => 201,
          ClaimStatus.Rejected => 422,
          ClaimStatus.InProgress => 409,
          ClaimStatus.Compensated => 503,
          _ => 500  
        };
      
        return Results.Json(claimResponse, statusCode: statusCode);
    }

    private static async Task<IResult> SubmitAsync(
        SubmitClaimRequest request,
        HttpContext httpContext,
        ClaimSubmissionWorkflow workflow,
        CancellationToken cancellationToken)
    {
        string? idempotencyKey = httpContext.Request.Headers[IdempotencyKeyHeader];
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Results.Problem(
                detail: $"The {IdempotencyKeyHeader} header is required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var errors = request.Validate(DateOnly.FromDateTime(DateTime.UtcNow));
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var claim = await workflow.SubmitAsync(request, idempotencyKey, cancellationToken);
        return ToHttpResult(claim);
    }

    private static async Task<IResult> GetAsync(
        Guid claimId,
        IClaimStore store,
        CancellationToken cancellationToken)
    {
        var claim = await store.FindAsync(claimId, cancellationToken);
        return claim is null ? Results.NotFound() : Results.Ok(ClaimResponse.From(claim));
    }
}
