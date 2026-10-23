using Ticket_manadger;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ticket_manadger.Api;
using Ticket_manadger.Api.Contracts;

namespace Ticket_manadger.Api.Controllers
{
    [ApiController]
    [Route("api/users")]
    public sealed class UsersController : ApiControllerBase
    {
        private readonly AppDbContext _db;
        private readonly UserRepository _users;
        private readonly AuthService _auth;

        public UsersController(AppDbContext db, UserRepository users, AuthService auth)
        {
            _db = db;
            _users = users;
            _auth = auth;
        }

        [HttpGet]
        public IActionResult GetAll()
        {
            var denied = RequireRole(UserRoles.Admin);
            if (denied is not null) return denied;
            return Ok(_users.GetAll().Select(ToDto));
        }

        [HttpPost]
        public IActionResult Create(CreateUserDto dto)
        {
            var denied = RequireRole(UserRoles.Admin);
            if (denied is not null) return denied;

            try
            {
                var user = _auth.CreateByAdministrator(dto.Login, dto.FullName, dto.Email, dto.Password, dto.Role);
                return Created($"/api/users/{user.Id}", ToDto(user));
            }
            catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
            catch (DbUpdateException) { return Conflict(new { error = "Логин или email уже используется." }); }
        }

        [HttpPatch("{id:int}/role")]
        public IActionResult ChangeRole(int id, ChangeRoleDto dto)
        {
            var denied = RequireRole(UserRoles.Admin);
            if (denied is not null) return denied;
            try
            {
                if (!_users.ChangeRole(id, dto.Role, UserId)) return NotFound();
                return NoContent();
            }
            catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
        }

        [HttpDelete("{id:int}")]
        public IActionResult Deactivate(int id)
        {
            var denied = RequireRole(UserRoles.Admin);
            if (denied is not null) return denied;
            try
            {
                if (!_users.SoftDelete(id, UserId)) return NotFound();
                return NoContent();
            }
            catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
        }

        private static AuthUserDto ToDto(User user) =>
            new(user.Id, user.Login, user.FullName, user.Email, user.Role, user.IsDeleted);
    }
}
