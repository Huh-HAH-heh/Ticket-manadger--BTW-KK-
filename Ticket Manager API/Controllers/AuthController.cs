using Ticket_manadger;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using Ticket_manadger.Api.Contracts;

namespace Ticket_manadger.Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public sealed class AuthController : ControllerBase
    {
        private readonly AuthService _auth;
        private readonly UserRepository _users;
        private readonly ApiTokenService _tokens;
        private readonly AppDbContext _db;

        public AuthController(AuthService auth, UserRepository users, ApiTokenService tokens, AppDbContext db)
        {
            _auth = auth;
            _users = users;
            _tokens = tokens;
            _db = db;
        }

        [HttpPost("register")]
        public IActionResult Register(RegisterDto dto)
        {
            try
            {
                var user = _auth.RegisterClient(dto.Login, dto.FullName, dto.Email, dto.Password);
                return Ok(CreateResponse(user));
            }
            catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
            catch (DbUpdateException) { return Conflict(new { error = "Логин или email уже используется." }); }
        }

        [HttpPost("login")]
        public IActionResult Login(LoginDto dto)
        {
            var user = _auth.Authenticate(dto.Login, dto.Password);
            return user is null
                ? Unauthorized(new { error = "Неверный логин или пароль либо учётная запись отключена." })
                : Ok(CreateResponse(user));
        }

        [HttpPost("bootstrap-admin")]
        public IActionResult BootstrapAdministrator(BootstrapAdminDto dto)
        {
            var configuredKey = Environment.GetEnvironmentVariable("TICKET_BOOTSTRAP_KEY");
            if (string.IsNullOrWhiteSpace(configuredKey))
                return StatusCode(503, new { error = "Первичная настройка администратора отключена: TICKET_BOOTSTRAP_KEY не задан." });

            var supplied = Encoding.UTF8.GetBytes(dto.BootstrapKey ?? string.Empty);
            var expected = Encoding.UTF8.GetBytes(configuredKey);
            if (!CryptographicOperations.FixedTimeEquals(supplied, expected))
                return Unauthorized(new { error = "Неверный ключ первичной настройки." });

            try
            {
                var user = _auth.BootstrapAdministrator(dto.Login, dto.FullName, dto.Email, dto.Password);
                return Ok(CreateResponse(user));
            }
            catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
            catch (DbUpdateException) { return Conflict(new { error = "Логин или email уже используется." }); }
        }

        private AuthResponse CreateResponse(User user) =>
            new(new AuthUserDto(user.Id, user.Login, user.FullName, user.Email, user.Role, user.IsDeleted), _tokens.Create(user));
    }
}
