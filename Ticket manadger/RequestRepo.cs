using Microsoft.EntityFrameworkCore;

namespace Ticket_manadger
{
    internal enum RequestSortOrder
    {
        NewestFirst,
        OldestFirst,
        TitleAscending,
        TitleDescending,
        Status
    }

    internal sealed class RequestRepo
    {
        public List<Request> GetAllRequest() =>
            FindRequests(null, null, RequestSortOrder.NewestFirst);

        public Request? GetRequestById(int requestId)
        {
            using var db = new AppDbContext();
            return db.Requests
                .AsNoTracking()
                .Include(r => r.Status)
                .Include(r => r.Client)
                .FirstOrDefault(r => r.Id == requestId);
        }

        public List<User> GetClients()
        {
            using var db = new AppDbContext();
            return db.User
                .AsNoTracking()
                .Where(user => user.Role == "Client")
                .OrderBy(user => user.FullName)
                .ToList();
        }

        public List<RequestStatus> GetStatuses()
        {
            using var db = new AppDbContext();
            return db.RequestStatus.AsNoTracking().OrderBy(status => status.Id).ToList();
        }

        public List<Request> FindRequests(
            string? searchText,
            int? statusId,
            RequestSortOrder sortOrder)
        {
            using var db = new AppDbContext();

            IQueryable<Request> query = db.Requests
                .AsNoTracking()
                .Include(r => r.Status)
                .Include(r => r.Client);

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var pattern = $"%{searchText.Trim()}%";
                query = query.Where(r =>
                    EF.Functions.ILike(r.Title, pattern) ||
                    EF.Functions.ILike(r.description, pattern) ||
                    EF.Functions.ILike(r.Client.FullName, pattern));
            }

            if (statusId.HasValue)
                query = query.Where(r => r.StatusId == statusId.Value);

            query = sortOrder switch
            {
                RequestSortOrder.OldestFirst => query.OrderBy(r => r.CraatedAt),
                RequestSortOrder.TitleAscending => query.OrderBy(r => r.Title),
                RequestSortOrder.TitleDescending => query.OrderByDescending(r => r.Title),
                RequestSortOrder.Status => query.OrderBy(r => r.Status.Name).ThenByDescending(r => r.CraatedAt),
                _ => query.OrderByDescending(r => r.CraatedAt)
            };

            return query.ToList();
        }

        public void AddRequest(string title, string description, int clientId)
        {
            ValidateRequestFields(title, description);

            using var db = new AppDbContext();
            if (!db.User.Any(user => user.Id == clientId && user.Role == "Client"))
                throw new ArgumentException("Клиент с таким ID не найден.");

            db.Requests.Add(new Request
            {
                Title = title.Trim(),
                description = description.Trim(),
                StatusId = 1,
                ClientId = clientId,
                CraatedAt = DateTime.UtcNow
            });

            db.SaveChanges();
        }

        public bool UpdateRequest(int requestId, string title, string description)
        {
            ValidateRequestFields(title, description);

            using var db = new AppDbContext();
            var request = db.Requests.Find(requestId);
            if (request is null)
                return false;

            request.Title = title.Trim();
            request.description = description.Trim();
            db.SaveChanges();
            return true;
        }

        public bool UpdateStatus(int requestId, int statusId)
        {
            using var db = new AppDbContext();

            var request = db.Requests.Find(requestId);
            if (request is null)
                return false;

            if (!db.RequestStatus.Any(status => status.Id == statusId))
                throw new ArgumentException("Статус с таким ID не найден.");

            request.StatusId = statusId;
            db.SaveChanges();
            return true;
        }

        public bool DeleteRequest(int requestId)
        {
            using var db = new AppDbContext();
            var request = db.Requests.Find(requestId);
            if (request is null)
                return false;

            db.Requests.Remove(request);
            db.SaveChanges();
            return true;
        }

        // Оставлено для совместимости с прежним именем метода.
        public bool DelRequest(int requestId) => DeleteRequest(requestId);

        public Dictionary<string, int> GetLog()
        {
            using var db = new AppDbContext();
            return db.Requests
                .AsNoTracking()
                .GroupBy(request => request.Status.Name)
                .Select(group => new { Status = group.Key, Count = group.Count() })
                .ToDictionary(item => item.Status, item => item.Count);
        }

        public List<KeyValuePair<string, int>> GetClientRequestCounts()
        {
            using var db = new AppDbContext();
            return db.Requests
                .AsNoTracking()
                .GroupBy(request => request.Client.FullName)
                .Select(group => new { Client = group.Key, Count = group.Count() })
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.Client)
                .Select(item => new KeyValuePair<string, int>(item.Client, item.Count))
                .ToList();
        }

        private static void ValidateRequestFields(string title, string description)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Название заявки не может быть пустым.");

            if (title.Trim().Length > 100)
                throw new ArgumentException("Название заявки должно содержать не более 100 символов.");

            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException("Описание заявки не может быть пустым.");
        }
    }
}
