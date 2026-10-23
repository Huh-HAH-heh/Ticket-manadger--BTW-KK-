using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ticket_manadger
{
    [Table("requests")]
    public class Request
    {
        [Key, Column("id")]
        public int Id { get; set; }

        [Required, MaxLength(100), Column("title")]
        public string Title { get; set; } = string.Empty;

        [Required, Column("description", TypeName = "text")]
        public string description { get; set; } = string.Empty;

        [Column("CraatedAt")]
        public DateTime CraatedAt { get; set; } = DateTime.UtcNow;

        [Column("statusId")]
        public int StatusId { get; set; }

        [ForeignKey(nameof(StatusId))]
        public RequestStatus Status { get; set; } = null!;

        [Column("clientId")]
        public int ClientId { get; set; }

        [ForeignKey(nameof(ClientId))]
        public User Client { get; set; } = null!;

        [Column("priority")]
        public int Priority { get; set; } = 2;

        [Column("executorId")]
        public int? ExecutorId { get; set; }

        [ForeignKey(nameof(ExecutorId))]
        public User? Executor { get; set; }
    }

    [Table("users")]
    public class User
    {
        [Key, Column("id")]
        public int Id { get; set; }

        [MaxLength(50), Column("login")]
        public string? Login { get; set; }

        [Required, MaxLength(100), Column("fullName")]
        public string FullName { get; set; } = string.Empty;

        [MaxLength(254), Column("email")]
        public string? Email { get; set; }

        [Required, MaxLength(40), Column("role")]
        public string Role { get; set; } = UserRoles.Client;

        [MaxLength(255), Column("passwordHash")]
        public string? PasswordHash { get; set; }

        [Column("isDeleted")]
        public bool IsDeleted { get; set; }
    }

    [Table("statuses")]
    public class RequestStatus
    {
        [Key, Column("id")]
        public int Id { get; set; }

        [Required, MaxLength(100), Column("Name")]
        public string Name { get; set; } = string.Empty;
    }

    [Table("comments")]
    public class RequestComment
    {
        [Key, Column("id")]
        public int Id { get; set; }

        [Column("ticketId")]
        public int TicketId { get; set; }

        [ForeignKey(nameof(TicketId))]
        public Request Ticket { get; set; } = null!;

        [Column("userId")]
        public int UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public User Author { get; set; } = null!;

        [Required, Column("text", TypeName = "text")]
        public string Text { get; set; } = string.Empty;

        [Column("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    [Table("requestHistory")]
    public class RequestHistory
    {
        [Key, Column("id")]
        public int Id { get; set; }

        [Column("ticketId")]
        public int TicketId { get; set; }

        [ForeignKey(nameof(TicketId))]
        public Request Ticket { get; set; } = null!;

        [Column("userId")]
        public int UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public User Actor { get; set; } = null!;

        [Required, MaxLength(80), Column("action")]
        public string Action { get; set; } = string.Empty;

        [Required, Column("details", TypeName = "text")]
        public string Details { get; set; } = string.Empty;

        [Column("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
