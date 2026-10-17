using Microsoft.EntityFrameworkCore;
using NewsCalendar.Api.Data;
using NewsCalendar.Api.Endpoints;
using NewsCalendar.Api.Infrastructure;
using NewsCalendar.Api.Models;

var builder = WebApplication.CreateBuilder(args);

// Connection string from env: ConnectionStrings__Default="Host=db;Database=newscal;Username=...;Password=..."
// Resolved lazily from IConfiguration so test hosts can override it.
builder.Services.AddDbContext<AppDbContext>((sp, o) =>
    o.UseNpgsql(sp.GetRequiredService<IConfiguration>().GetConnectionString("Default")));
builder.Services.AddScoped<ApiKeyFilter>();
builder.Services.AddApiRateLimiting();
builder.WebHost.ConfigureKestrel(k => k.AddServerHeader = false);

// Cors__Origins="http://localhost:5173,https://app.example.com"
var origins = (builder.Configuration["Cors:Origins"] ?? "http://localhost:5173")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// Fail fast at startup if the connection string is missing.
if (string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("Default")))
    throw new InvalidOperationException("ConnectionStrings__Default is not set.");

app.UseSecurityHeaders();
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseBodySizeLimit();
app.UseCors();
app.UseRateLimiter();

app.MapGet("/health", async (AppDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct)
        ? Results.Ok(new { status = "ok", db = "up" })
        : Results.Json(new ApiError("Database unreachable.", new { db = "down" }), statusCode: 503));

app.MapEventEndpoints();
app.Run();

// Lets WebApplicationFactory<Program> in the test project reach the entry point.
public partial class Program;
