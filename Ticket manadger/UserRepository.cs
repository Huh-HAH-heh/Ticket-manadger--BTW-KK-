using Microsoft.EntityFrameworkCore;

namespace Ticket_manadger
{
    internal sealed class UserRepository
    {
        public bool HasActiveAdministrator()
        {
            using var db = new AppDbContext();
            return db.User.Any(user => !user.IsDeleted && user.Role == UserRoles.Admin);
        }

        public User? FindActiveByLogin(string login)
        {
            using var db = new AppDbContext();
            return db.User.AsNoTracking().FirstOrDefault(user =>
                !user.IsDeleted && user.Login == login);
        }

        public User CreateAccount(string login, string fullName, string email, string passwordHash, string role)
        {
            ValidateRole(role);
            using var db = new AppDbContext();
            EnsureLoginAndEmailAvailable(db, login, email);

            var user = new User
            {
                Login = login, FullName = fullName, Email = email,
                PasswordHash = passwordHash, Role = role, IsDeleted = false
            };
            db.User.Add(user);
            db.SaveChanges();
            return user;
        }

        public User CreateFirstAdministrator(string login, string fullName, string email, string passwordHash)
        {
            using var db = new AppDbContext();
            if (db.User.Any(user => !user.IsDeleted && user.Role == UserRoles.Admin))
                throw new InvalidOperationException("Администратор уже существует. Первичная настройка недоступна.");

            EnsureLoginAndEmailAvailable(db, login, email);
            var user = new User
            {
                Login = login, FullName = fullName, Email = email,
                PasswordHash = passwordHash, Role = UserRoles.Admin, IsDeleted = false
            };
            db.User.Add(user);
            db.SaveChanges();
            return user;
        }

        public List<User> GetUsersForAdministrator()
        {
            using var db = new AppDbContext();
            return db.User.AsNoTracking().OrderBy(user => user.Id).ToList();
        }

        public bool ChangeRole(int userId, string newRole, int actingAdministratorId)
        {
            ValidateRole(newRole);
            using var db = new AppDbContext();
            var user = db.User.FirstOrDefault(item => item.Id == userId);
            if (user is null || user.IsDeleted)
                return false;

            if (user.Id == actingAdministratorId && newRole != UserRoles.Admin)
                throw new InvalidOperationException("Нельзя снять роль администратора с текущей учётной записи.");

            if (user.Role == UserRoles.Admin && newRole != UserRoles.Admin &&
                !db.User.Any(item => !item.IsDeleted && item.Role == UserRoles.Admin && item.Id != user.Id))
                throw new InvalidOperationException("Нельзя снять роль с последнего активного администратора.");

            user.Role = newRole;
            db.SaveChanges();
            return true;
        }

        public bool SoftDelete(int userId, int actingAdministratorId)
        {
            using var db = new AppDbContext();
            var user = db.User.FirstOrDefault(item => item.Id == userId);
            if (user is null || user.IsDeleted)
                return false;

            if (user.Id == actingAdministratorId)
                throw new InvalidOperationException("Нельзя деактивировать собственную учётную запись.");

            if (user.Role == UserRoles.Admin &&
                !db.User.Any(item => !item.IsDeleted && item.Role == UserRoles.Admin && item.Id != user.Id))
                throw new InvalidOperationException("Нельзя деактивировать последнего активного администратора.");

            user.IsDeleted = true;
            db.SaveChanges();
            return true;
        }

        private static void EnsureLoginAndEmailAvailable(AppDbContext db, string login, string email)
        {
            if (db.User.Any(user => user.Login == login))
                throw new InvalidOperationException("Этот логин уже занят.");
            if (db.User.Any(user => user.Email == email))
                throw new InvalidOperationException("Этот email уже зарегистрирован.");
        }

        private static void ValidateRole(string role)
        {
            if (!UserRoles.IsKnown(role))
                throw new ArgumentException("Неизвестная роль пользователя.");
        }
    }
}
