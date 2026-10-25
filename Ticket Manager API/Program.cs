using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Ticket_manadger;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddDbContext<AppDbContext>();
builder.Services.AddSingleton<ApiTokenService>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<AuthService>();

var app = builder.Build();

if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TICKET_API_TOKEN_SECRET")) ||
    System.Text.Encoding.UTF8.GetByteCount(Environment.GetEnvironmentVariable("TICKET_API_TOKEN_SECRET")!) < 32)
{
    throw new InvalidOperationException(
        "Set TICKET_API_TOKEN_SECRET to a random value containing at least 32 UTF-8 bytes before starting the API.");
}

// Apply migrations at container startup. The local deployment enables a temporary
// model-snapshot compatibility switch until the migration snapshot is reconciled.
var migrationConnection = Environment.GetEnvironmentVariable("TICKET_DB_CONNECTION");
if (string.IsNullOrWhiteSpace(migrationConnection))
    throw new InvalidOperationException("TICKET_DB_CONNECTION must be set before applying migrations.");

var migrationOptionsBuilder = new DbContextOptionsBuilder<AppDbContext>()
    .UseNpgsql(migrationConnection);
if (string.Equals(
        Environment.GetEnvironmentVariable("TICKET_DOCKER_ALLOW_PENDING_MODEL_CHANGES"),
        "true",
        StringComparison.OrdinalIgnoreCase))
{
    migrationOptionsBuilder.ConfigureWarnings(warnings =>
        warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
}

await using (var migrationDb = new AppDbContext(migrationOptionsBuilder.Options))
{
    await migrationDb.Database.MigrateAsync();
}

app.Use(async (context, next) =>
{
    var header = context.Request.Headers["Authorization"].ToString();
    if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        var tokens = context.RequestServices.GetRequiredService<ApiTokenService>();
        if (tokens.TryValidate(header["Bearer ".Length..].Trim(), out var claims))
        {
            var db = context.RequestServices.GetRequiredService<AppDbContext>();
            var user = await db.User.AsNoTracking()
                .FirstOrDefaultAsync(candidate => candidate.Id == claims.UserId && !candidate.IsDeleted);

            // Re-check the current role so role changes and deactivation invalidate old tokens.
            if (user is not null && user.Role == claims.Role)
            {
                context.Items["UserId"] = user.Id;
                context.Items["UserRole"] = user.Role;
                context.Items["UserLogin"] = user.Login;
            }
        }
    }

    await next();
});

app.MapGet("/health", async (AppDbContext db) =>
{
    try
    {
        if (!await db.Database.CanConnectAsync())
            return Results.Problem("PostgreSQL is not reachable.", statusCode: 503);

        var pending = (await db.Database.GetPendingMigrationsAsync()).ToArray();
        if (pending.Length > 0)
            return Results.Problem(
                "Database has pending migrations: " + string.Join(", ", pending),
                statusCode: 503);

        return Results.Ok(new { status = "ok", database = "connected", migrations = "current" });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Database check failed: {ex.Message}", statusCode: 503);
    }
});

app.MapControllers();
app.Run();
