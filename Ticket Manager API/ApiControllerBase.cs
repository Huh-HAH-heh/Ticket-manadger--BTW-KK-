using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Ticket_manadger;

namespace Ticket_manadger.Api
{
    public abstract class ApiControllerBase : ControllerBase
    {
        protected int UserId => HttpContext.Items.TryGetValue("UserId", out var id) && id is int value ? value : 0;
        protected string Role => HttpContext.Items.TryGetValue("UserRole", out var role) ? role as string ?? string.Empty : string.Empty;

        protected bool IsSignedIn => UserId > 0 && UserRoles.IsKnown(Role);

        protected IActionResult? RequireLogin() => IsSignedIn ? null : Unauthorized(new { error = "Войдите в систему." });

        protected IActionResult? RequireRole(params string[] roles) =>
            !IsSignedIn ? Unauthorized(new { error = "Войдите в систему." }) :
            roles.Contains(Role, StringComparer.Ordinal) ? null :
            StatusCode(StatusCodes.Status403Forbidden, new { error = "Недостаточно прав для этой операции." });
    }
}
