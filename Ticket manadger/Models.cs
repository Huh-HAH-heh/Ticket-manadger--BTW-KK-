using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ticket_manadger
{
    [Table("requests")]
    public class Request {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        [Required]
        [MaxLength(100)]
        [Column("title")]
        public string Title { get; set; }
        [Required]

        [Column("description", TypeName = "text")]
        public string description { get; set; } = null!;
   
        [Column("craatedAt")]
        public DateTime CraatedAt { get; set; } = DateTime.UtcNow;
        

        [Column("statusId")]
        public int StatusId { get; set; }
        [ForeignKey(nameof(StatusId))
            ]
        public RequestStatus Status { get; set; } = null!;
        [Column("clientId")]
        public int ClientId { get; set; }
        [ForeignKey(nameof(ClientId))]
        public User Client { get; set; } = null!;
    }

    [Table("users")]
    public class User
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        [Required]
        [MaxLength(100)]
        [Column("fullName ")]
        public string FullName { get; set; }
        [Required]
        [MaxLength(40)]
        [Column("role")]
        public string Role { get; set; } = null!;
    }

    [Table("statuses")]
    public class RequestStatus
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        [Required]
        [MaxLength(100)]
        [Column("Name")]
        public string Name { get; set; }
        public List<Request> Request { get; set; } = new();

    }
}
