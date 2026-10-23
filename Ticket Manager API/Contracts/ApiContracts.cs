namespace Ticket_manadger.Api.Contracts
{
    public sealed record RegisterDto(string Login, string FullName, string Email, string Password);
    public sealed record LoginDto(string Login, string Password);
    public sealed record BootstrapAdminDto(string BootstrapKey, string Login, string FullName, string Email, string Password);
    public sealed record CreateUserDto(string Login, string FullName, string Email, string Password, string Role);
    public sealed record AuthUserDto(int Id, string? Login, string FullName, string? Email, string Role, bool IsDeleted);
    public sealed record AuthResponse(AuthUserDto User, string Token);
    public sealed record ChangeRoleDto(string Role);
    public sealed record SetExecutorDto(int? ExecutorId);
    public sealed record CreateRequestDto(string Title, string Description, int Priority = 2, int? ClientId = null);
    public sealed record UpdateRequestDto(string Title, string Description, int Priority = 2);
    public sealed record ChangeStatusDto(int StatusId);
    public sealed record CreateCommentDto(string Text);
    public sealed record RequestDto(int Id, string Title, string Description, DateTime CreatedAt,
        int StatusId, string Status, int Priority, int ClientId, string ClientName,
        int? ExecutorId, string? ExecutorName);
    public sealed record CommentDto(int Id, int TicketId, int UserId, string Author, string Text, DateTime CreatedAt);
    public sealed record HistoryDto(int Id, int TicketId, int UserId, string Actor, string Action, string Details, DateTime CreatedAt);
    public sealed record StatusCountDto(string Status, int Count);
    public sealed record ClientCountDto(string Client, int Count);
}
