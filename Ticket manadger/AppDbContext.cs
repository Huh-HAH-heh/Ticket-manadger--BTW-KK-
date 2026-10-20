using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Ticket_manadger
{
    public class AppDbContext:DbContext
    { 
        public DbSet<Request> Requests { get; set; } = null!;
        public DbSet<RequestStatus> RequestStatus { get; set; } = null!;

        public DbSet<User> User { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql("Host=localhost;Port=5433;Database=ticket_db;Username=postgres;Password=my_secret_password_123");
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RequestStatus>().HasData(
                new RequestStatus { Id = 1, Name = "new" },
                new RequestStatus { Id = 2, Name = "InProgress" },
    new RequestStatus { Id =3, Name = "Cumplete" });

            modelBuilder.Entity<User>().HasData(
                new User { Id =1,FullName="IVATEST",Role="Client"});




        }


    }

}

