using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Formatting.Compact;
using ZenGear.Api.Auth;
using ZenGear.Api.Errors;
using ZenGear.Api.Logging;
using ZenGear.Api.OpenApi;
using ZenGear.Services;

// Nothing here connects to PostgreSQL, Redis or Firebase: the build runs this file to write the
// OpenAPI document (GetDocument.Insider, Production environment, no user-secrets).
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((services, logger) =>
{
    logger.ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.With<SensitivePropertyMasker>();

    if (builder.Environment.IsDevelopment())
    {
        logger.WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj} {TraceId} {Properties:j}{NewLine}{Exception}");
    }
    else
    {
        logger.WriteTo.Console(new CompactJsonFormatter());
    }
});
builder.Services.AddSingleton<SecurityLog>();

builder.Services.AddZenGearServices(builder.Configuration);
builder.Services.AddZenGearProblemDetails();
builder.Services.AddFirebaseJwtBearer(builder.Environment);

// Enums as their stored string values, no naming policy; [JsonStringEnumMemberName] where they differ.
builder.Services.AddControllers(options =>
        options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider()))
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddZenGearOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = (context, report) =>
    {
        context.Response.ContentType = "text/plain";
        return context.Response.WriteAsync(report.Status.ToString());
    },
}).AllowAnonymous();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

app.Run();
