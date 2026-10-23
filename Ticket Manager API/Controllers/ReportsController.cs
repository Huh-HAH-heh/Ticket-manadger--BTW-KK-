using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ticket_manadger.Api;
using Ticket_manadger.Api.Contracts;

namespace Ticket_manadger.Api.Controllers
{
    [ApiController]
    [Route("api/reports")]
    public sealed class ReportsController : ApiControllerBase
    {
        private readonly AppDbContext _db;
        public ReportsController(AppDbContext db) => _db = db;

        [HttpGet("statuses")]
        public IActionResult ByStatus()
        {
            var denied = RequireRole(UserRoles.Admin);
            if (denied is not null) return denied;

            var rows = _db.Requests.AsNoTracking()
                .GroupBy(request => request.Status.Name)
                .Select(group => new StatusCountDto(group.Key, group.Count()))
                .OrderBy(row => row.Status)
                .ToList();
            return Ok(rows);
        }

        [HttpGet("clients")]
        public IActionResult ByClient()
        {
            var denied = RequireRole(UserRoles.Admin);
            if (denied is not null) return denied;

            var rows = _db.Requests.AsNoTracking()
                .GroupBy(request => request.Client.FullName)
                .Select(group => new ClientCountDto(group.Key, group.Count()))
                .OrderByDescending(row => row.Count)
                .ThenBy(row => row.Client)
                .ToList();
            return Ok(rows);
        }

        [HttpGet("overdue")]
        public IActionResult Overdue([FromQuery] int olderThanDays = 3)
        {
            var denied = RequireRole(UserRoles.Operator, UserRoles.Admin);
            if (denied is not null) return denied;
            if (olderThanDays is < 1 or > 365)
                return BadRequest(new { error = "Интервал должен быть от 1 до 365 дней." });

            var cutoff = DateTime.UtcNow.AddDays(-olderThanDays);
            var query = _db.Requests.AsNoTracking()
                .Include(request => request.Status)
                .Include(request => request.Client)
                .Include(request => request.Executor)
                .Where(request => request.StatusId != 3 && request.CraatedAt < cutoff);

            if (Role == UserRoles.Operator)
                query = query.Where(request => request.ExecutorId == UserId || request.ExecutorId == null);

            var results = query.OrderBy(request => request.CraatedAt)
                .Take(500)
                .Select(request => new RequestDto(
                    request.Id, request.Title, request.description, request.CraatedAt,
                    request.StatusId, request.Status.Name, request.Priority, request.ClientId,
                    request.Client.FullName, request.ExecutorId, request.Executor == null ? null : request.Executor.FullName))
                .ToList();
            return Ok(results);
        }
    }
}
