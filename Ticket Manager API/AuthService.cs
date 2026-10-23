using System.Net.Mail;
using System.Text.RegularExpressions;

namespace Ticket_manadger
{
    public sealed class AuthService
    {
        private readonly UserRepository _users;
        public AuthService(UserRepository users) => _users = users;

        public User? Authenticate(string login, string password)
        {
            var normalized = (login ?? string.Empty).Trim().ToLowerInvariant();
            if (normalized.Length == 0 || string.IsNullOrEmpty(password)) return null;
            var user = _users.FindActiveByLogin(normalized);
            return user is not null && PasswordHasher.Verify(password, user.PasswordHash) ? user : null;
        }

        public User RegisterClient(string login, string fullName, string email, string password)
        {
            var values = Validate(login, fullName, email, password);
            return _users.CreateAccount(values.Login, values.Name, values.Email, PasswordHasher.Hash(password), UserRoles.Client);
        }

        public User CreateByAdministrator(string login, string fullName, string email, string password, string role)
        {
            if (!UserRoles.IsKnown(role)) throw new ArgumentException("Неизвестная роль.");
            var values = Validate(login, fullName, email, password);
            return _users.CreateAccount(values.Login, values.Name, values.Email, PasswordHasher.Hash(password), role);
        }

        public User BootstrapAdministrator(string login, string fullName, string email, string password)
        {
            var values = Validate(login, fullName, email, password);
            return _users.CreateFirstAdministrator(values.Login, values.Name, values.Email, PasswordHasher.Hash(password));
        }

        private static (string Login, string Name, string Email) Validate(string login, string fullName, string email, string password)
        {
            login = (login ?? string.Empty).Trim().ToLowerInvariant();
            fullName = (fullName ?? string.Empty).Trim();
            email = (email ?? string.Empty).Trim().ToLowerInvariant();
            password ??= string.Empty;

            if (!Regex.IsMatch(login, @"^[a-z0-9_.-]{3,50}$"))
                throw new ArgumentException("Логин должен содержать 3–50 латинских букв, цифр, точек, дефисов или подчёркиваний.");
            if (fullName.Length is < 2 or > 100) throw new ArgumentException("Имя должно содержать 2–100 символов.");
            if (email.Length is < 5 or > 254 || !IsEmail(email)) throw new ArgumentException("Укажите корректный email.");
            if (password.Length is < 8 or > 128) throw new ArgumentException("Пароль должен содержать 8–128 символов.");
            return (login, fullName, email);
        }

        private static bool IsEmail(string email)
        {
            try
            {
                return string.Equals(new MailAddress(email).Address, email, StringComparison.OrdinalIgnoreCase);
            }
            catch (FormatException) { return false; }
        }
    }
}
