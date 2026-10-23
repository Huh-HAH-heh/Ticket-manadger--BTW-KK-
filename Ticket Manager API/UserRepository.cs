using Microsoft.EntityFrameworkCore;

namespace Ticket_manadger
{
    public sealed class UserRepository
    {
        private readonly AppDbContext _db;
        public UserRepository(AppDbContext db) => _db = db;

        public bool HasActiveAdministrator() =>
            _db.User.Any(user => !user.IsDeleted && user.Role == UserRoles.Admin);

        public User? FindActiveByLogin(string login) =>
            _db.User.FirstOrDefault(user => !user.IsDeleted && user.Login == login);

        public User CreateAccount(string login, string fullName, string email, string hash, string role)
        {
            if (!UserRoles.IsKnown(role)) throw new ArgumentException("Неизвестная роль.");
            if (_db.User.Any(user => user.Login == login)) throw new InvalidOperationException("Логин уже занят.");
            if (_db.User.Any(user => user.Email == email)) throw new InvalidOperationException("Email уже зарегистрирован.");

            var user = new User
            {
                Login = login, FullName = fullName, Email = email,
                PasswordHash = hash, Role = role, IsDeleted = false
            };
            _db.User.Add(user);
            _db.SaveChanges();
            return user;
        }

        public User CreateFirstAdministrator(string login, string fullName, string email, string hash)
        {
            if (HasActiveAdministrator())
                throw new InvalidOperationException("Первый администратор уже создан.");

            return CreateAccount(login, fullName, email, hash, UserRoles.Admin);
        }

        public List<User> GetAll() =>
            _db.User.AsNoTracking().OrderBy(user => user.Id).ToList();

        public bool ChangeRole(int id, string role, int actorId)
        {
            if (!UserRoles.IsKnown(role)) throw new ArgumentException("Неизвестная роль.");
            var user = _db.User.FirstOrDefault(item => item.Id == id);
            if (user is null || user.IsDeleted) return false;

            if (user.Id == actorId && role != UserRoles.Admin)
                throw new InvalidOperationException("Нельзя снять роль с текущего администратора.");

            if (user.Role == UserRoles.Admin && role != UserRoles.Admin &&
                !_db.User.Any(item => !item.IsDeleted && item.Role == UserRoles.Admin && item.Id != id))
                throw new InvalidOperationException("Нельзя снять роль с последнего активного администратора.");

            user.Role = role;
            _db.SaveChanges();
            return true;
        }

        public bool SoftDelete(int id, int actorId)
        {
            var user = _db.User.FirstOrDefault(item => item.Id == id);
            if (user is null || user.IsDeleted) return false;
            if (user.Id == actorId) throw new InvalidOperationException("Нельзя отключить свою учётную запись.");

            if (user.Role == UserRoles.Admin &&
                !_db.User.Any(item => !item.IsDeleted && item.Role == UserRoles.Admin && item.Id != id))
                throw new InvalidOperationException("Нельзя отключить последнего активного администратора.");

            user.IsDeleted = true;
            _db.SaveChanges();
            return true;
        }
    }
}
