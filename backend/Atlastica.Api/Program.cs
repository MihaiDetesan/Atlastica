using Atlastica.Infrastructure;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddAtlasticaInfrastructure(builder.Configuration);

// Allowed origins come from config ("Cors:AllowedOrigins") so the deployed
// site's real origin can be set via appsettings.Production.json or the
// Cors__AllowedOrigins__0 env var, instead of the frontend dev URL being
// hardcoded into every environment. The localhost fallback only applies in
// Development - if it's missing in Production, fail fast at startup rather
// than silently opening CORS to nothing (or, worse, someone "fixing" that by
// widening it) at runtime.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
if (allowedOrigins is null or [])
{
    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "Cors:AllowedOrigins is not configured. Set it via appsettings.Production.json " +
            "or the Cors__AllowedOrigins__0 env var before starting in a non-Development environment.");
    }

    allowedOrigins = ["http://localhost:5173"];
}

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Log unhandled exceptions before ASP.NET Core turns them into a bare 500 -
// without this, request failures (e.g. Postgres unreachable) leave no trace.
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogError(exception, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);

    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    context.Response.ContentType = "application/problem+json";
    await context.Response.WriteAsJsonAsync(new { status = 500, title = "An unexpected error occurred." });
}));

app.UseHttpsRedirection();

app.UseCors();

app.UseAuthorization();

app.MapControllers();

app.Run();
