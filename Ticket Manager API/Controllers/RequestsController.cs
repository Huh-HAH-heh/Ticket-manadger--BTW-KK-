using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ticket_manadger.Api;
using Ticket_manadger.Api.Contracts;

namespace Ticket_manadger.Api.Controllers
{
    [ApiController]
    [Route("api/requests")]
    public sealed class RequestsController : ApiControllerBase
    {
        private readonly AppDbContext _db;
        public RequestsController(AppDbContext db) => _db = db;

        [HttpGet]
        public IActionResult GetAll(
            [FromQuery] string? search,
            [FromQuery] int? statusId,
            [FromQuery] int? executorId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] bool overdue = false)
        {
            var denied = RequireRole(UserRoles.Client, UserRoles.Operator, UserRoles.Admin);
            if (denied is not null) return denied;

            IQueryable<Request> query = RequestQuery();
            if (Role == UserRoles.Client)
                query = query.Where(request => request.ClientId == UserId);
            else if (Role == UserRoles.Operator)
                query = query.Where(request => request.ExecutorId == UserId || request.ExecutorId == null);

            if (statusId.HasValue) query = query.Where(request => request.StatusId == statusId.Value);
            if (executorId.HasValue && Role == UserRoles.Admin)
                query = query.Where(request => request.ExecutorId == executorId.Value);
            if (from.HasValue) query = query.Where(request => request.CraatedAt >= from.Value);
            if (to.HasValue) query = query.Where(request => request.CraatedAt <= to.Value);
            if (overdue)
            {
                var cutoff = DateTime.UtcNow.AddDays(-3);
                query = query.Where(request => request.StatusId != 3 && request.CraatedAt < cutoff);
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                var pattern = $"%{search.Trim()}%";
                query = query.Where(request =>
                    EF.Functions.ILike(request.Title, pattern) ||
                    EF.Functions.ILike(request.description, pattern) ||
                    EF.Functions.ILike(request.Client.FullName, pattern));
            }

            return Ok(query.OrderByDescending(request => request.CraatedAt).Take(500).ToList().Select(ToDto));
        }

        [HttpGet("{id:int}")]
        public IActionResult Get(int id)
        {
            var denied = RequireRole(UserRoles.Client, UserRoles.Operator, UserRoles.Admin);
            if (denied is not null) return denied;

            var request = RequestQuery().FirstOrDefault(item => item.Id == id);
            if (request is null || !CanAccess(request)) return NotFound();
            return Ok(ToDto(request));
        }

        [HttpPost]
        public IActionResult Create(CreateRequestDto dto)
        {
            var denied = RequireRole(UserRoles.Client, UserRoles.Admin);
            if (denied is not null) return denied;
            if (string.IsNullOrWhiteSpace(dto.Title) || dto.Title.Trim().Length > 100 ||
                string.IsNullOrWhiteSpace(dto.Description))
                return BadRequest(new { error = "Название обязательно (до 100 символов), описание не может быть пустым." });
            if (dto.Priority is < 1 or > 4)
                return BadRequest(new { error = "Приоритет должен быть от 1 (низкий) до 4 (критический)." });

            var clientId = Role == UserRoles.Client ? UserId : dto.ClientId.GetValueOrDefault();
            var client = _db.User.FirstOrDefault(user =>
                user.Id == clientId && !user.IsDeleted && user.Role == UserRoles.Client);
            if (client is null) return BadRequest(new { error = "Активный клиент не найден." });

            var request = new Request
            {
                Title = dto.Title.Trim(),
                description = dto.Description.Trim(),
                StatusId = 1,
                ClientId = clientId,
                Priority = dto.Priority,
                CraatedAt = DateTime.UtcNow
            };
            _db.Requests.Add(request);
            _db.SaveChanges();
            AddHistory(request.Id, "Created", $"Создана заявка с приоритетом {request.Priority}.");
            return Created($"/api/requests/{request.Id}", ToDto(request.Id));
        }

        [HttpPut("{id:int}")]
        public IActionResult Update(int id, UpdateRequestDto dto)
        {
            var denied = RequireRole(UserRoles.Client, UserRoles.Operator, UserRoles.Admin);
            if (denied is not null) return denied;
            if (string.IsNullOrWhiteSpace(dto.Title) || dto.Title.Trim().Length > 100 ||
                string.IsNullOrWhiteSpace(dto.Description) || dto.Priority is < 1 or > 4)
                return BadRequest(new { error = "Проверьте название, описание и приоритет (1–4)." });

            var request = _db.Requests.FirstOrDefault(item => item.Id == id);
            if (request is null || !CanAccess(request)) return NotFound();
            if (Role == UserRoles.Client && request.StatusId != 1)
                return Conflict(new { error = "Клиент может редактировать только новые заявки." });

            var before = $"Название: {request.Title}; приоритет: {request.Priority}";
            request.Title = dto.Title.Trim();
            request.description = dto.Description.Trim();
            request.Priority = dto.Priority;
            _db.SaveChanges();
            AddHistory(id, "Updated", $"{before} -> Название: {request.Title}; приоритет: {request.Priority}");
            return Ok(ToDto(id));
        }

        [HttpPatch("{id:int}/status")]
        public IActionResult ChangeStatus(int id, ChangeStatusDto dto)
        {
            var denied = RequireRole(UserRoles.Operator, UserRoles.Admin);
            if (denied is not null) return denied;
            if (!_db.RequestStatus.Any(status => status.Id == dto.StatusId))
                return BadRequest(new { error = "Неизвестный статус." });

            var request = _db.Requests.FirstOrDefault(item => item.Id == id);
            if (request is null || !CanAccess(request)) return NotFound();
            var oldStatus = request.StatusId;
            request.StatusId = dto.StatusId;
            _db.SaveChanges();
            AddHistory(id, "StatusChanged", $"Статус изменён: {oldStatus} -> {dto.StatusId}.");
            return Ok(ToDto(id));
        }

        [HttpPost("{id:int}/claim")]
        public IActionResult Claim(int id)
        {
            var denied = RequireRole(UserRoles.Operator);
            if (denied is not null) return denied;
            var request = _db.Requests.FirstOrDefault(item => item.Id == id);
            if (request is null) return NotFound();
            if (request.ExecutorId.HasValue && request.ExecutorId != UserId)
                return Conflict(new { error = "Заявка уже назначена другому исполнителю." });

            request.ExecutorId = UserId;
            request.StatusId = 2;
            _db.SaveChanges();
            AddHistory(id, "Claimed", $"Исполнитель назначил заявку на себя (ID пользователя {UserId}).");
            return Ok(ToDto(id));
        }

        [HttpPatch("{id:int}/executor")]
        public IActionResult AssignExecutor(int id, SetExecutorDto dto)
        {
            var denied = RequireRole(UserRoles.Admin);
            if (denied is not null) return denied;

            var request = _db.Requests.FirstOrDefault(item => item.Id == id);
            if (request is null) return NotFound();

            User? executor = null;
            if (dto.ExecutorId.HasValue)
            {
                executor = _db.User.FirstOrDefault(user =>
                    user.Id == dto.ExecutorId.Value && !user.IsDeleted && user.Role == UserRoles.Operator);
                if (executor is null)
                    return BadRequest(new { error = "Исполнитель должен быть активным пользователем с ролью Operator." });
            }

            var previous = request.ExecutorId;
            request.ExecutorId = executor?.Id;
            _db.SaveChanges();
            AddHistory(id, "ExecutorAssigned",
                $"Назначение исполнителя: {previous?.ToString() ?? "не назначен"} -> {executor?.Id.ToString() ?? "не назначен"}.");
            return Ok(ToDto(id));
        }

        [HttpDelete("{id:int}")]
        public IActionResult Delete(int id)
        {
            var denied = RequireRole(UserRoles.Admin);
            if (denied is not null) return denied;
            var request = _db.Requests.FirstOrDefault(item => item.Id == id);
            if (request is null) return NotFound();
            _db.Requests.Remove(request);
            _db.SaveChanges();
            return NoContent();
        }

        [HttpGet("{id:int}/comments")]
        public IActionResult Comments(int id)
        {
            var denied = RequireRole(UserRoles.Client, UserRoles.Operator, UserRoles.Admin);
            if (denied is not null) return denied;
            var request = _db.Requests.FirstOrDefault(item => item.Id == id);
            if (request is null || !CanAccess(request)) return NotFound();

            var comments = _db.Comments.AsNoTracking()
                .Where(comment => comment.TicketId == id)
                .OrderBy(comment => comment.CreatedAt)
                .Select(comment => new CommentDto(comment.Id, comment.TicketId, comment.UserId,
                    comment.Author.FullName, comment.Text, comment.CreatedAt))
                .ToList();
            return Ok(comments);
        }

        [HttpPost("{id:int}/comments")]
        public IActionResult AddComment(int id, CreateCommentDto dto)
        {
            var denied = RequireRole(UserRoles.Client, UserRoles.Operator, UserRoles.Admin);
            if (denied is not null) return denied;
            if (string.IsNullOrWhiteSpace(dto.Text) || dto.Text.Trim().Length > 5000)
                return BadRequest(new { error = "Комментарий должен содержать от 1 до 5000 символов." });

            var request = _db.Requests.FirstOrDefault(item => item.Id == id);
            if (request is null || !CanAccess(request)) return NotFound();
            var comment = new RequestComment
            {
                TicketId = id, UserId = UserId, Text = dto.Text.Trim(), CreatedAt = DateTime.UtcNow
            };
            _db.Comments.Add(comment);
            _db.SaveChanges();
            AddHistory(id, "CommentAdded", "Добавлен комментарий.");
            return Ok(new CommentDto(comment.Id, comment.TicketId, comment.UserId, CurrentUserName(), comment.Text, comment.CreatedAt));
        }

        [HttpGet("{id:int}/history")]
        public IActionResult History(int id)
        {
            var denied = RequireRole(UserRoles.Operator, UserRoles.Admin);
            if (denied is not null) return denied;
            var request = _db.Requests.FirstOrDefault(item => item.Id == id);
            if (request is null || !CanAccess(request)) return NotFound();

            return Ok(_db.RequestHistory.AsNoTracking()
                .Where(item => item.TicketId == id)
                .OrderBy(item => item.CreatedAt)
                .Select(item => new HistoryDto(item.Id, item.TicketId, item.UserId,
                    item.Actor.FullName, item.Action, item.Details, item.CreatedAt))
                .ToList());
        }

        [HttpGet("/api/statuses")]
        public IActionResult Statuses()
        {
            var denied = RequireRole(UserRoles.Client, UserRoles.Operator, UserRoles.Admin);
            if (denied is not null) return denied;
            return Ok(_db.RequestStatus.AsNoTracking().OrderBy(item => item.Id).ToList());
        }

        private IQueryable<Request> RequestQuery() => _db.Requests.AsNoTracking()
            .Include(request => request.Status).Include(request => request.Client).Include(request => request.Executor);

        private bool CanAccess(Request request) => Role switch
        {
            UserRoles.Admin => true,
            UserRoles.Client => request.ClientId == UserId,
            UserRoles.Operator => request.ExecutorId == UserId || request.ExecutorId is null,
            _ => false
        };

        private RequestDto ToDto(int id) =>
            ToDto(RequestQuery().First(item => item.Id == id));

        private static RequestDto ToDto(Request request) =>
            new(request.Id, request.Title, request.description, request.CraatedAt,
                request.StatusId, request.Status.Name, request.Priority, request.ClientId,
                request.Client.FullName, request.ExecutorId, request.Executor?.FullName);

        private void AddHistory(int ticketId, string action, string details)
        {
            _db.RequestHistory.Add(new RequestHistory
            {
                TicketId = ticketId, UserId = UserId, Action = action,
                Details = details, CreatedAt = DateTime.UtcNow
            });
            _db.SaveChanges();
        }

        private string CurrentUserName() =>
            _db.User.Where(user => user.Id == UserId).Select(user => user.FullName).FirstOrDefault() ?? "Пользователь";
    }
}
