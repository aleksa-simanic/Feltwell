using System.Text.Json.Serialization;
using Feltwell.Api.Endpoints;
using Feltwell.Api.Errors;
using Feltwell.Api.Repositories;
using Feltwell.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Feltwell",
        Version = "v1",
        Description = "Millinery hat-block catalog and job API."
    });
});
builder.Services.AddProblemDetails();

builder.Services.AddSingleton<InMemoryBlockStore>();
builder.Services.AddSingleton<IBlockStore>(sp => sp.GetRequiredService<InMemoryBlockStore>());
builder.Services.AddSingleton<BlockService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["*"];
        if (origins.Length == 1 && origins[0] == "*")
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.Services.GetRequiredService<IBlockStore>().SeedDemo();
}

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var exception = feature?.Error;

        var (status, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Not Found"),
            ValidationException => (StatusCodes.Status400BadRequest, "Validation Error"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            _ => (StatusCodes.Status500InternalServerError, "Server Error")
        };

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new
        {
            title,
            detail = exception?.Message ?? "An unexpected error occurred.",
            status
        });
    });
});

app.UseCors();

app.MapGet("/", () => Results.Ok(new
{
    service = "Feltwell",
    version = "1.0.0",
    docs = "/swagger",
    health = "/health"
}))
.ExcludeFromDescription();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "Feltwell",
    env = app.Environment.EnvironmentName
}))
.WithTags("Health")
.WithName("Health");

app.MapBlockEndpoints();

app.Run();

public partial class Program;
