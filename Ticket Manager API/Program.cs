using Microsoft.EntityFrameworkCore;
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

// Apply pending EF Core migrations when this local Docker API starts.
using (var migrationScope = app.Services.CreateScope())
{
    var db = migrationScope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
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
