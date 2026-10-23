using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Ticket_manadger;

internal sealed record ConsoleUser(int Id, string? Login, string FullName, string? Email, string Role, bool IsDeleted);
internal sealed record AuthReply(ConsoleUser User, string Token);
internal sealed record ConsoleRequest(int Id, string Title, string Description, DateTime CreatedAt,
    int StatusId, string Status, int Priority, int ClientId, string ClientName,
    int? ExecutorId, string? ExecutorName);
internal sealed record ConsoleStatus(int Id, string Name);
internal sealed record ConsoleComment(int Id, int TicketId, int UserId, string Author, string Text, DateTime CreatedAt);
internal sealed record ConsoleHistory(int Id, int TicketId, int UserId, string Actor, string Action, string Details, DateTime CreatedAt);
internal sealed record ConsoleStatusCount(string Status, int Count);
internal sealed record ConsoleClientCount(string Client, int Count);
internal sealed record HealthReply(string Status, string Database);
internal sealed record CreateRequestBody(string Title, string Description, int Priority = 2, int? ClientId = null);
internal sealed record UpdateRequestBody(string Title, string Description, int Priority = 2);
internal sealed record ChangeStatusBody(int StatusId);
internal sealed record LoginBody(string Login, string Password);
internal sealed record RegisterBody(string Login, string FullName, string Email, string Password);
internal sealed record BootstrapBody(string BootstrapKey, string Login, string FullName, string Email, string Password);
internal sealed record CreateUserBody(string Login, string FullName, string Email, string Password, string Role);
internal sealed record ChangeRoleBody(string Role);
internal sealed record CommentBody(string Text);

internal static class RoleNames
{
    public const string Client = "Client";
    public const string Operator = "Operator";
    public const string Admin = "Admin";

    public static string Display(string role) => role switch
    {
        Client => "Клиент",
        Operator => "Оператор",
        Admin => "Администратор",
        _ => "Неизвестная роль"
    };
}

internal sealed class TicketApiClient : IDisposable
{
    private readonly HttpClient _http;

    public TicketApiClient()
    {
        var url = Environment.GetEnvironmentVariable("TICKET_API_URL") ?? "http://localhost:5080";
        _http = new HttpClient
        {
            BaseAddress = new Uri(url.EndsWith('/') ? url : url + "/"),
            Timeout = TimeSpan.FromSeconds(20)
        };
    }

    public void SetToken(string? token)
    {
        _http.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(token)
            ? null
            : new AuthenticationHeaderValue("Bearer", token);
    }

    public Task<HealthReply> CheckHealthAsync() => GetAsync<HealthReply>("health");
    public Task<AuthReply> LoginAsync(string login, string password) =>
        PostAsync<AuthReply>("api/auth/login", new LoginBody(login, password));

    public Task<AuthReply> RegisterAsync(string login, string fullName, string email, string password) =>
        PostAsync<AuthReply>("api/auth/register", new RegisterBody(login, fullName, email, password));

    public Task<AuthReply> BootstrapAdminAsync(string key, string login, string fullName, string email, string password) =>
        PostAsync<AuthReply>("api/auth/bootstrap-admin", new BootstrapBody(key, login, fullName, email, password));

    public Task<List<ConsoleRequest>> GetRequestsAsync(string? search = null, int? statusId = null,
        int? executorId = null, DateTime? from = null, DateTime? to = null, bool overdue = false)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(search)) query.Add("search=" + Uri.EscapeDataString(search));
        if (statusId.HasValue) query.Add("statusId=" + statusId.Value);
        if (executorId.HasValue) query.Add("executorId=" + executorId.Value);
        if (from.HasValue) query.Add("from=" + Uri.EscapeDataString(from.Value.ToUniversalTime().ToString("O")));
        if (to.HasValue) query.Add("to=" + Uri.EscapeDataString(to.Value.ToUniversalTime().ToString("O")));
        if (overdue) query.Add("overdue=true");
        return GetAsync<List<ConsoleRequest>>("api/requests" + (query.Count == 0 ? "" : "?" + string.Join("&", query)));
    }

    public Task<List<ConsoleStatus>> GetStatusesAsync() => GetAsync<List<ConsoleStatus>>("api/statuses");

    public Task<ConsoleRequest> CreateRequestAsync(string title, string description, int priority, int? clientId = null) =>
        PostAsync<ConsoleRequest>("api/requests", new CreateRequestBody(title, description, priority, clientId));

    public Task<ConsoleRequest> UpdateRequestAsync(int id, string title, string description, int priority) =>
        PutAsync<ConsoleRequest>($"api/requests/{id}", new UpdateRequestBody(title, description, priority));

    public Task<ConsoleRequest> ChangeStatusAsync(int id, int statusId) =>
        PatchAsync<ConsoleRequest>($"api/requests/{id}/status", new ChangeStatusBody(statusId));

    public Task<ConsoleRequest> ClaimAsync(int id) =>
        PostAsync<ConsoleRequest>($"api/requests/{id}/claim", new { });

    public Task DeleteRequestAsync(int id) => DeleteAsync($"api/requests/{id}");

    public Task<List<ConsoleComment>> GetCommentsAsync(int id) =>
        GetAsync<List<ConsoleComment>>($"api/requests/{id}/comments");

    public Task<ConsoleComment> AddCommentAsync(int id, string text) =>
        PostAsync<ConsoleComment>($"api/requests/{id}/comments", new CommentBody(text));

    public Task<List<ConsoleHistory>> GetHistoryAsync(int id) =>
        GetAsync<List<ConsoleHistory>>($"api/requests/{id}/history");

    public Task<List<ConsoleUser>> GetUsersAsync() => GetAsync<List<ConsoleUser>>("api/users");

    public Task<ConsoleUser> CreateUserAsync(string login, string fullName, string email, string password, string role) =>
        PostAsync<ConsoleUser>("api/users", new CreateUserBody(login, fullName, email, password, role));

    public Task ChangeUserRoleAsync(int id, string role) => PatchNoContentAsync($"api/users/{id}/role", new ChangeRoleBody(role));
    public Task DeactivateUserAsync(int id) => DeleteAsync($"api/users/{id}");

    public Task<List<ConsoleStatusCount>> GetStatusReportAsync() =>
        GetAsync<List<ConsoleStatusCount>>("api/reports/statuses");

    public Task<List<ConsoleClientCount>> GetClientReportAsync() =>
        GetAsync<List<ConsoleClientCount>>("api/reports/clients");

    public Task<List<ConsoleRequest>> GetOverdueAsync(int days) =>
        GetAsync<List<ConsoleRequest>>($"api/reports/overdue?olderThanDays={days}");

    private async Task<T> GetAsync<T>(string path)
    {
        using var response = await _http.GetAsync(path);
        return await ReadAsync<T>(response);
    }

    private async Task<T> PostAsync<T>(string path, object body)
    {
        using var response = await _http.PostAsJsonAsync(path, body);
        return await ReadAsync<T>(response);
    }

    private async Task<T> PutAsync<T>(string path, object body)
    {
        using var response = await _http.PutAsJsonAsync(path, body);
        return await ReadAsync<T>(response);
    }

    private async Task<T> PatchAsync<T>(string path, object body)
    {
        using var response = await _http.PatchAsJsonAsync(path, body);
        return await ReadAsync<T>(response);
    }

    private async Task PatchNoContentAsync(string path, object body)
    {
        using var response = await _http.PatchAsJsonAsync(path, body);
        await EnsureSuccessAsync(response);
    }

    private async Task DeleteAsync(string path)
    {
        using var response = await _http.DeleteAsync(path);
        await EnsureSuccessAsync(response);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        await EnsureSuccessAsync(response);
        var value = await response.Content.ReadFromJsonAsync<T>();
        return value ?? throw new InvalidOperationException("API вернул пустой ответ.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync();
        var message = $"HTTP {(int)response.StatusCode} ({response.ReasonPhrase})";
        try
        {
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.TryGetProperty("error", out var error))
                message = error.GetString() ?? message;
            else if (json.RootElement.TryGetProperty("title", out var title))
                message = title.GetString() ?? message;
        }
        catch (JsonException)
        {
            if (!string.IsNullOrWhiteSpace(body)) message += ": " + body;
        }

        throw new InvalidOperationException(message);
    }

    public void Dispose() => _http.Dispose();
}
