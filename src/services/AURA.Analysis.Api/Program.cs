using AURA.Analysis.Api.Http;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var analysisHttpEnabled = builder.Configuration.GetValue<bool>("Analysis:EnableHttpEndpoints");
if (analysisHttpEnabled) builder.Services.AddAnalysisRuntime(builder.Configuration,builder.Environment);

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health");
app.MapAnalysisReadiness(analysisHttpEnabled);

app.MapGet("/api/v1/platform/ping", () =>
    Results.Ok(new PlatformPingResponse(
        "AURA.Analysis.Api",
        "Template API is running",
        DateTimeOffset.UtcNow)))
    .WithName("PlatformPing")
    .WithTags("Platform")
    .Produces<PlatformPingResponse>(StatusCodes.Status200OK);

if (analysisHttpEnabled) app.MapAnalysisEndpoints();

app.Run();

public sealed record PlatformPingResponse(
    string Service,
    string Message,
    DateTimeOffset Timestamp);