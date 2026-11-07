using System.Text.Json.Serialization;
using Claims.Api;
using Claims.Api.Services;
using Claims.Api.Workflow;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var downstreamBaseUrl = builder.Configuration.GetValue<Uri>("Downstream:BaseUrl")
    ?? throw new InvalidOperationException("Set Downstream:BaseUrl in appsettings.json.");

builder.Services.AddDownstreamServices(downstreamBaseUrl);
builder.Services.AddSingleton<IClaimStore, InMemoryClaimStore>();
builder.Services.AddSingleton(new WorkflowOptions());
builder.Services.AddScoped<ClaimSubmissionWorkflow>();

var app = builder.Build();
app.MapClaimEndpoints();
app.Run();

/// <summary>Lets the test project start this app through WebApplicationFactory.</summary>
public sealed class ClaimsApiEntryPoint;
