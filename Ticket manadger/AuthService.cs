using System.Net.Mail;
using System.Text.RegularExpressions;

namespace Ticket_manadger
{
    internal sealed class AuthService
    {
        private readonly UserRepository _users = new();

        public User? Authenticate(string login, string password)
        {
            var normalizedLogin = (login ?? string.Empty).Trim().ToLowerInvariant();
            if (normalizedLogin.Length == 0 || password.Length == 0)
                return null;

            var user = _users.FindActiveByLogin(normalizedLogin);
            return user is not null && PasswordHasher.Verify(password, user.PasswordHash) ? user : null;
        }

        public User RegisterClient(string login, string fullName, string email, string password)
        {
            var data = ValidateAccount(login, fullName, email, password);
            return _users.CreateAccount(data.Login, data.FullName, data.Email, PasswordHasher.Hash(password), UserRoles.Client);
        }

        public User CreateAccountByAdministrator(string login, string fullName, string email, string password, string role)
        {
            if (!UserRoles.IsKnown(role))
                throw new ArgumentException("Выберите роль клиента, оператора или администратора.");

            var data = ValidateAccount(login, fullName, email, password);
            return _users.CreateAccount(data.Login, data.FullName, data.Email, PasswordHasher.Hash(password), role);
        }

        public User BootstrapFirstAdministrator(string login, string fullName, string email, string password)
        {
            var data = ValidateAccount(login, fullName, email, password);
            return _users.CreateFirstAdministrator(data.Login, data.FullName, data.Email, PasswordHasher.Hash(password));
        }

        private static (string Login, string FullName, string Email) ValidateAccount(
            string login, string fullName, string email, string password)
        {
            login = (login ?? string.Empty).Trim().ToLowerInvariant();
            fullName = (fullName ?? string.Empty).Trim();
            email = (email ?? string.Empty).Trim().ToLowerInvariant();

            if (!Regex.IsMatch(login, @"^[a-z0-9_.-]{3,50}$"))
                throw new ArgumentException("Логин: от 3 до 50 символов, латинские буквы, цифры, точка, дефис или подчёркивание.");
            if (fullName.Length is < 2 or > 100)
                throw new ArgumentException("Имя должно содержать от 2 до 100 символов.");
            if (email.Length is < 5 or > 254 || !IsEmail(email))
                throw new ArgumentException("Укажите корректный email (до 254 символов).");
            if (password.Length is < 8 or > 128)
                throw new ArgumentException("Пароль должен содержать от 8 до 128 символов.");

            return (login, fullName, email);
        }

        private static bool IsEmail(string email)
        {
            try
            {
                var parsed = new MailAddress(email);
                return string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase);
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
