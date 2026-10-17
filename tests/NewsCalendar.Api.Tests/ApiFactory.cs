using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NewsCalendar.Api.Data;

namespace NewsCalendar.Api.Tests;

/// Same model as production, minus the Postgres-only tsvector column (InMemory can't map it).
public class InMemoryAppDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<NewsCalendar.Api.Models.Event>().Ignore(e => e.SearchVector);
    }
}

/// Boots the real app with an in-memory EF database (no Postgres needed).
/// Search is not covered here: tsvector queries need a real Postgres.
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly string? _apiKey;
    private readonly string? _connectionString;
    private readonly bool _useInMemory;
    private readonly Dictionary<string, string?>? _extra;

    public ApiFactory() : this(null, null, true) { }   // parameterless: required by xUnit class fixtures

    internal ApiFactory(string? apiKey = null, string? connectionString = null, bool useInMemory = true, Dictionary<string, string?>? configure = null)
    {
        _extra = configure;
        _apiKey = apiKey;
        _connectionString = connectionString;
        _useInMemory = useInMemory;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            var values = new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _connectionString ?? "Host=localhost;Database=unused",
                ["API_KEY"] = _apiKey
            };
            if (_extra is not null) foreach (var kv in _extra) values[kv.Key] = kv.Value;
            cfg.AddInMemoryCollection(values);
        });

        if (!_useInMemory) return;
        builder.ConfigureServices(services =>
        {
            // Drop production registrations (Npgsql options + context), then register the in-memory one.
            foreach (var d in services.Where(d => d.ServiceType == typeof(AppDbContext)
                                               || d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                                               || d.ServiceType == typeof(DbContextOptions)).ToList())
                services.Remove(d);
            var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_dbName).Options;
            services.AddScoped<AppDbContext>(_ => new InMemoryAppDbContext(options));
        });
    }
}
