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
        var body = ClaimResponse.From(claim);
        return claim.Status switch
        {
            ClaimStatus.Submitted => Results.Created($"/claims/{claim.ClaimId}", body),
            ClaimStatus.Rejected => Results.Json(body, statusCode: StatusCodes.Status422UnprocessableEntity),
            ClaimStatus.InProgress => Results.Json(body, statusCode: StatusCodes.Status409Conflict),
            ClaimStatus.Compensated => Results.Json(body, statusCode: StatusCodes.Status503ServiceUnavailable),
            ClaimStatus.CompensationFailed => Results.Json(body, statusCode: StatusCodes.Status500InternalServerError),
            _ => throw new InvalidOperationException($"Unknown claim status: {claim.Status}"),
        };
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
