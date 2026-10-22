using Microsoft.EntityFrameworkCore;

namespace Ticket_manadger
{
    public class AppDbContext : DbContext
    {
        public DbSet<Request> Requests => Set<Request>();
        public DbSet<RequestStatus> RequestStatus => Set<RequestStatus>();
        public DbSet<User> User => Set<User>();

        public AppDbContext()
        {
        }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (optionsBuilder.IsConfigured)
                return;

            var connectionString = Environment.GetEnvironmentVariable("TICKET_DB_CONNECTION");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Не задана строка подключения. Установите переменную окружения TICKET_DB_CONNECTION.");
            }

            optionsBuilder.UseNpgsql(connectionString);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RequestStatus>().HasData(
                new RequestStatus { Id = 1, Name = "new" },
                new RequestStatus { Id = 2, Name = "InProgress" },
                new RequestStatus { Id = 3, Name = "Cumplete" });

            modelBuilder.Entity<User>().HasData(
                new User { Id = 1, FullName = "IVATEST", Role = "Client" });
        }
    }
}
