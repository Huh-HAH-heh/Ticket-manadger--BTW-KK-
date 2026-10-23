using Microsoft.EntityFrameworkCore;

namespace Ticket_manadger
{
    public class AppDbContext : DbContext
    {
        public DbSet<Request> Requests => Set<Request>();
        public DbSet<RequestStatus> RequestStatus => Set<RequestStatus>();
        public DbSet<User> User => Set<User>();
        public DbSet<RequestComment> Comments => Set<RequestComment>();
        public DbSet<RequestHistory> RequestHistory => Set<RequestHistory>();

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (optionsBuilder.IsConfigured) return;
            var connectionString = Environment.GetEnvironmentVariable("TICKET_DB_CONNECTION");
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Set TICKET_DB_CONNECTION before starting Ticket Manager API.");
            optionsBuilder.UseNpgsql(connectionString);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>().HasIndex(user => user.Login).IsUnique();
            modelBuilder.Entity<User>().HasIndex(user => user.Email).IsUnique();

            modelBuilder.Entity<Request>()
                .HasOne(request => request.Client)
                .WithMany()
                .HasForeignKey(request => request.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Request>()
                .HasOne(request => request.Status)
                .WithMany(status => status.Request)
                .HasForeignKey(request => request.StatusId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Request>()
                .HasOne(request => request.Executor)
                .WithMany()
                .HasForeignKey(request => request.ExecutorId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<RequestComment>()
                .HasOne(comment => comment.Ticket)
                .WithMany()
                .HasForeignKey(comment => comment.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RequestHistory>()
                .HasOne(item => item.Ticket)
                .WithMany()
                .HasForeignKey(item => item.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RequestComment>().HasIndex(comment => new { comment.TicketId, comment.CreatedAt });
            modelBuilder.Entity<RequestHistory>().HasIndex(item => new { item.TicketId, item.CreatedAt });
            modelBuilder.Entity<Request>().HasIndex(request => new { request.StatusId, request.CraatedAt });
            modelBuilder.Entity<Request>().HasIndex(request => new { request.ExecutorId, request.StatusId });

            modelBuilder.Entity<RequestStatus>().HasData(
                new RequestStatus { Id = 1, Name = "new" },
                new RequestStatus { Id = 2, Name = "InProgress" },
                new RequestStatus { Id = 3, Name = "Cumplete" });

            modelBuilder.Entity<User>().HasData(
                new User { Id = 1, FullName = "IVATEST", Role = UserRoles.Client, IsDeleted = false });
        }
    }
}
