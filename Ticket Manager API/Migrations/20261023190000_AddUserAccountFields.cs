using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ticket_manadger.Migrations
{
    public partial class AddUserAccountFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "login", table: "users",
                type: "character varying(50)", maxLength: 50, nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "email", table: "users",
                type: "character varying(254)", maxLength: 254, nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "isDeleted", table: "users",
                type: "boolean", nullable: false, defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_users_email", table: "users", column: "email", unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_login", table: "users", column: "login", unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_users_email", table: "users");
            migrationBuilder.DropIndex(name: "IX_users_login", table: "users");
            migrationBuilder.DropColumn(name: "email", table: "users");
            migrationBuilder.DropColumn(name: "login", table: "users");
            migrationBuilder.DropColumn(name: "isDeleted", table: "users");
        }
    }
}
