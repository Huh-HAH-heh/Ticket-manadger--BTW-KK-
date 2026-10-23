using Microsoft.EntityFrameworkCore;
using Ticket_manadger;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddDbContext<AppDbContext>();
builder.Services.AddSingleton<ApiTokenService>();

var app = builder.Build();

if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TICKET_API_TOKEN_SECRET")))
    throw new InvalidOperationException(
        "Set TICKET_API_TOKEN_SECRET to a long random value before starting the API.");

app.Use(async (context, next) =>
{
    var header = context.Request.Headers.Authorization.ToString();
    if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        var tokens = context.RequestServices.GetRequiredService<ApiTokenService>();
        if (tokens.TryValidate(header["Bearer ".Length..].Trim(), out var claims))
        {
            context.Items["UserId"] = claims.UserId;
            context.Items["UserRole"] = claims.Role;
            context.Items["UserLogin"] = claims.Login;
        }
    }

    await next();
});

app.MapGet("/health", async (AppDbContext db) =>
{
    try
    {
        var canConnect = await db.Database.CanConnectAsync();
        return canConnect
            ? Results.Ok(new { status = "ok", database = "connected" })
            : Results.Problem("PostgreSQL is not reachable.", statusCode: 503);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Database check failed: {ex.Message}", statusCode: 503);
    }
});

app.MapControllers();
app.Run();
